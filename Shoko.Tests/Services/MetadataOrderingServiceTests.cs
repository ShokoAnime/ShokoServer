using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="MetadataOrderingService"/> against in-memory tables: the
/// default ordering every series has, global orderings saved whole under a
/// plugin's source, users' local orderings with the IDs the core gives them,
/// the ordering chosen for a series, hidden episodes, and removing a series' orderings.
/// </summary>
public class MetadataOrderingServiceTests
{
    #region Helpers

    /// <summary>
    /// Series and episodes the service can find, with the service over
    /// in-memory tables.
    /// </summary>
    private sealed class World
    {
        private readonly Dictionary<MetadataGuid, IMetadata> _entries = [];

        public Mock<IMetadataService> Metadata { get; } = new();

        public OrderingTables Tables { get; }

        public MetadataOrderingService Service { get; }

        public World(ICoreOrderingSource[]? coreSources = null, params AnimeEpisode[] shokoEpisodes)
        {
            Tables = new(shokoEpisodes);
            Metadata.Setup(metadata => metadata.GetSeries(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) => _entries.GetValueOrDefault(id) as ISeries);
            Metadata.Setup(metadata => metadata.GetEpisode(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) => _entries.GetValueOrDefault(id) as IEpisode);
            Service = Tables.Build(() => Metadata.Object, coreSources ?? []);
        }

        /// <summary>
        /// Adds a series with its seasons, each holding some episodes, named
        /// <c>&lt;id&gt;-s&lt;season&gt;</c> and <c>&lt;id&gt;-e&lt;number&gt;</c>.
        /// </summary>
        public ISeries AddSeries(MetadataSource source, string id, params int[] episodesPerSeason)
        {
            var seriesID = new MetadataGuid(source, MetadataEntityType.Series, id);
            var series = new Mock<ISeries>();
            var seasons = new List<ISeason>();
            var episodes = new List<IEpisode>();
            var number = 0;
            for (var seasonNumber = 1; seasonNumber <= episodesPerSeason.Length; seasonNumber++)
            {
                var seasonID = new MetadataGuid(source, MetadataEntityType.Season, $"{id}-s{seasonNumber}");
                var season = new Mock<ISeason>();
                season.SetupGet(value => value.ID).Returns(seasonID);
                season.SetupGet(value => value.SeriesID).Returns(seriesID);
                season.SetupGet(value => value.SeasonNumber).Returns(seasonNumber);
                seasons.Add(season.Object);
                for (var index = 1; index <= episodesPerSeason[seasonNumber - 1]; index++)
                {
                    number++;
                    var episode = new Mock<IEpisode>();
                    episode.SetupGet(value => value.ID).Returns(new MetadataGuid(source, MetadataEntityType.Episode, $"{id}-e{number}"));
                    episode.SetupGet(value => value.SeriesID).Returns(seriesID);
                    episode.SetupGet(value => value.SeasonID).Returns(seasonID);
                    episode.SetupGet(value => value.SeasonNumber).Returns(seasonNumber);
                    episode.SetupGet(value => value.EpisodeNumber).Returns(index);
                    episode.SetupGet(value => value.Series).Returns(() => series.Object);
                    episodes.Add(episode.Object);
                    _entries[episode.Object.ID] = episode.Object;
                }
            }

            series.SetupGet(value => value.ID).Returns(seriesID);
            // Handed out in reverse, so the default ordering has to put them in order itself.
            series.SetupGet(value => value.Seasons).Returns(seasons);
            series.SetupGet(value => value.Episodes).Returns(Enumerable.Reverse(episodes).ToList());
            _entries[seriesID] = series.Object;
            return series.Object;
        }
    }

    private static MetadataGuid Episode(MetadataSource source, string id)
        => new(source, MetadataEntityType.Episode, id);

    private static MetadataGuid Group(MetadataSource source, string id)
        => new(source, MetadataEntityType.Season, id);

    private static MetadataGuid Ordering(MetadataSource source, string id)
        => new(source, MetadataEntityType.Ordering, id);

    private static MetadataOrderingData Global(string id, MetadataGuid series, params (string ID, string[] Episodes)[] groups)
        => new()
        {
            ID = Ordering(TestSources.Plugin, id),
            SeriesID = series,
            Name = $"Ordering {id}",
            Overview = "About it.",
            Type = OrderingType.DVD,
            Groups = [.. groups.Select(group => new MetadataOrderingGroupData
            {
                ID = Group(TestSources.Plugin, group.ID),
                Name = $"Group {group.ID}",
                Episodes = [.. group.Episodes.Select(episode => Episode(series.Source, episode))],
            })],
        };

    private static MetadataLocalOrderingData Local(MetadataGuid series, params (MetadataGuid? ID, string[] Episodes)[] groups)
        => new()
        {
            SeriesID = series,
            Name = "Mine",
            Groups = [.. groups.Select((group, index) => new MetadataLocalOrderingGroupData
            {
                ID = group.ID,
                Name = $"Part {index + 1}",
                Episodes = [.. group.Episodes.Select(episode => Episode(series.Source, episode))],
            })],
        };

    #endregion

    #region Default Ordering

    [Fact]
    public void EverySeriesHasADefaultOrderingMadeFromItsSeasons()
    {
        var world = new World();
        var series = world.AddSeries(TestSources.Plugin, "a", 2, 1);

        var ordering = Assert.Single(world.Service.GetOrderings(series));

        Assert.True(ordering.IsDefault);
        Assert.True(ordering.IsPreferred);
        Assert.Equal(OrderingType.Default, ordering.Type);
        Assert.Equal(MetadataOrderingService.DefaultOrderingID(series.ID), ordering.ID);
        Assert.Equal(series.ID, ordering.SeriesID);
        Assert.Equal(2, ordering.SeasonCount);
        Assert.Equal(3, ordering.EpisodeCount);
        Assert.Equal(["a-e1", "a-e2", "a-e3"], ordering.Episodes.Select(episode => episode.ID.ID));
    }

    [Fact]
    public void ADefaultOrderingIDThatWouldBeTooLongIsHashed()
    {
        var longID = new string('x', MetadataGuid.MaxIDLength);

        var id = MetadataOrderingService.DefaultOrderingID(new(TestSources.Plugin, MetadataEntityType.Series, longID));

        Assert.StartsWith("default/#", id.ID);
        Assert.InRange(id.ID.Length, 1, MetadataGuid.MaxIDLength);
        Assert.Equal(id, MetadataOrderingService.DefaultOrderingID(new(TestSources.Plugin, MetadataEntityType.Series, longID)));
    }

    [Fact]
    public void ADefaultOrderingIsFoundByItsID()
    {
        var world = new World();
        var series = world.AddSeries(TestSources.Plugin, "a", 1);

        var ordering = world.Service.GetOrdering(MetadataOrderingService.DefaultOrderingID(series.ID));

        Assert.NotNull(ordering);
        Assert.True(ordering.IsDefault);
        Assert.Same(series, ordering.Series);
        Assert.Null(world.Service.GetOrdering(MetadataOrderingService.DefaultOrderingID(new(TestSources.Plugin, MetadataEntityType.Series, "missing"))));
        Assert.Null(world.Service.GetOrdering(Ordering(TestSources.Plugin, "a")));
    }

    #endregion

    #region Global Orderings

    [Fact]
    public void AGlobalOrderingIsStoredWholeAndReadsBackAsSeasons()
    {
        var world = new World();
        var series = world.AddSeries(MetadataSource.AniDB, "7", 3);

        var stored = world.Service.SaveOrdering(Global("dvd", series.ID, ("g1", ["7-e2", "7-e1"]), ("g2", ["7-e3", "7-e1"])));

        Assert.Equal(Ordering(TestSources.Plugin, "dvd"), stored.ID);
        Assert.Equal(series.ID, stored.SeriesID);
        Assert.False(stored.IsDefault);
        Assert.False(stored.IsPreferred);
        Assert.Equal(2, stored.SeasonCount);
        Assert.Equal(3, stored.EpisodeCount);
        Assert.Equal(["7-e2", "7-e1", "7-e3"], stored.Episodes.Select(episode => episode.ID.ID));
        var groups = stored.Seasons;
        Assert.Equal([Group(TestSources.Plugin, "g1"), Group(TestSources.Plugin, "g2")], groups.Select(group => group.ID));
        Assert.Equal([1, 2], groups.Select(group => group.SeasonNumber));
        Assert.Equal(["7-e3", "7-e1"], groups[1].Episodes.Select(episode => episode.ID.ID));

        var orderings = world.Service.GetOrderings(series);
        Assert.Equal(2, orderings.Count);
        Assert.True(orderings[0].IsDefault);
        Assert.Equal(stored.ID, orderings[1].ID);
        Assert.Equal(stored.ID, world.Service.GetOrdering(stored.ID)?.ID);
        Assert.Equal(stored.ID, Assert.Single(world.Service.GetStoredOrderings(TestSources.Plugin)).ID);
    }

    [Fact]
    public void SavingAGlobalOrderingAgainReplacesItAndAnUnchangedSaveWritesNothing()
    {
        var world = new World();
        var series = world.AddSeries(TestSources.AniList, "s", 4);
        world.Service.SaveOrdering(Global("o", series.ID, ("g1", ["s-e1", "s-e2"]), ("g2", ["s-e3"])));
        var writes = world.Tables.Writer.Writes;

        world.Service.SaveOrdering(Global("o", series.ID, ("g1", ["s-e1", "s-e2"]), ("g2", ["s-e3"])));
        Assert.Equal(writes, world.Tables.Writer.Writes);

        var replaced = world.Service.SaveOrdering(Global("o", series.ID, ("g3", ["s-e4", "s-e1"]), ("g1", ["s-e2"])));

        Assert.Equal(writes + 1, world.Tables.Writer.Writes);
        Assert.Equal(["g3", "g1"], replaced.Seasons.Select(group => group.ID.ID));
        Assert.Equal(["s-e4", "s-e1", "s-e2"], replaced.Episodes.Select(episode => episode.ID.ID));
        Assert.Null(world.Tables.Groups.GetByProviderID(TestSources.Plugin, "g2"));
        Assert.Equal(3, world.Tables.Entries.GetAll().Count);
    }

    [Fact]
    public void AGlobalOrderingIsRefusedWhenItBreaksTheRules()
    {
        var world = new World();
        var series = world.AddSeries(TestSources.AniList, "s", 2);
        world.AddSeries(TestSources.AniList, "t", 1);
        world.Service.SaveOrdering(Global("taken", series.ID, ("g1", ["s-e1"])));
        var valid = Global("o", series.ID, ("g2", ["s-e1"]));

        Assert.Throws<ArgumentException>(() => world.Service.SaveOrdering(valid with { ID = Ordering(MetadataSource.AniDB, "o") }));
        Assert.Throws<ArgumentException>(() => world.Service.SaveOrdering(valid with { ID = Ordering(MetadataSource.User, "o") }));
        Assert.Throws<ArgumentException>(() => world.Service.SaveOrdering(valid with { ID = Ordering(TestSources.Plugin, "default/s") }));
        Assert.Throws<ArgumentException>(() => world.Service.SaveOrdering(valid with { Type = OrderingType.Default }));
        Assert.Throws<ArgumentException>(() => world.Service.SaveOrdering(valid with { Type = OrderingType.User }));
        Assert.Throws<ArgumentException>(() => world.Service.SaveOrdering(valid with { Name = " " }));
        Assert.Throws<ArgumentException>(() => world.Service.SaveOrdering(valid with { SeriesID = new(TestSources.AniList, MetadataEntityType.Series, "missing") }));
        Assert.Throws<ArgumentException>(() => world.Service.SaveOrdering(Global("o", series.ID, ("g2", ["t-e1"]))));
        Assert.Throws<ArgumentException>(() => world.Service.SaveOrdering(Global("o", series.ID, ("g1", ["s-e1"]))));
        Assert.Throws<ArgumentException>(() => world.Service.SaveOrdering(Global("o", series.ID, ("g2", ["s-e1"]), ("g2", ["s-e2"]))));
        Assert.Throws<ArgumentException>(() => world.Service.SaveOrdering(valid with
        {
            Groups = [new() { ID = Group(TestSources.AniList, "g2"), Name = "Elsewhere" }],
        }));
        Assert.Single(world.Service.GetStoredOrderings(TestSources.Plugin));
    }

    [Fact]
    public void TheSpecialGroupIsSeasonZeroAndTheRestAreNumberedByPlace()
    {
        var world = new World();
        var series = world.AddSeries(TestSources.AniList, "s", 3);
        var data = Global("o", series.ID, ("g1", ["s-e1"]), ("specials", ["s-e3"]), ("g2", ["s-e2"]));
        data = data with { Groups = [data.Groups[0], data.Groups[1] with { IsSpecial = true }, data.Groups[2]] };

        var stored = world.Service.SaveOrdering(data);

        Assert.True(world.Tables.Groups.GetByProviderID(TestSources.Plugin, "specials")?.IsSpecial);
        var special = series.Episodes.Single(item => item.ID.ID == "s-e3");
        var place = world.Service.GetEpisodeOrderings(special).Skip(1).Single();
        Assert.Equal((0, 1, EpisodeType.Special), (place.SeasonNumber, place.EpisodeNumber, place.EpisodeType));
        var normal = series.Episodes.Single(item => item.ID.ID == "s-e2");
        place = world.Service.GetEpisodeOrderings(normal).Skip(1).Single();
        Assert.Equal((2, 1, EpisodeType.Episode), (place.SeasonNumber, place.EpisodeNumber, place.EpisodeType));

        // Moving only the flag is a change, and the numbers follow it.
        var writes = world.Tables.Writer.Writes;
        world.Service.SaveOrdering(data with { Groups = [data.Groups[0], data.Groups[1] with { IsSpecial = false }, data.Groups[2] with { IsSpecial = true }] });
        Assert.Equal(writes + 1, world.Tables.Writer.Writes);
        Assert.Equal([1, 2, 0], world.Service.GetOrdering(stored.ID)!.Seasons.Select(group => group.SeasonNumber));
    }

    [Fact]
    public void AGlobalOrderingWithTwoSpecialGroupsIsRefused()
    {
        var world = new World();
        var series = world.AddSeries(TestSources.AniList, "s", 2);
        var data = Global("o", series.ID, ("g1", ["s-e1"]), ("g2", ["s-e2"]));

        Assert.Throws<ArgumentException>(() => world.Service.SaveOrdering(data with
        {
            Groups = [data.Groups[0] with { IsSpecial = true }, data.Groups[1] with { IsSpecial = true }],
        }));
        Assert.Empty(world.Service.GetStoredOrderings(TestSources.Plugin));
    }

    [Theory]
    [InlineData(new bool[0], new int[0])]
    [InlineData(new[] { false, false, false }, new[] { 1, 2, 3 })]
    [InlineData(new[] { true, false, false }, new[] { 0, 1, 2 })]
    [InlineData(new[] { false, true, false }, new[] { 1, 0, 2 })]
    [InlineData(new[] { false, false, true }, new[] { 1, 2, 0 })]
    public void TheSpecialGroupIsZeroAndTheOthersAreNumberedByTheirPlaceAmongThemselves(bool[] isSpecial, int[] expected)
        => Assert.Equal(expected, MetadataOrderingService.NumberGroups(isSpecial));

    [Fact]
    public void AGroupTakenWhileAGlobalOrderingIsCheckedIsStillRefused()
    {
        var world = new World();
        var series = world.AddSeries(TestSources.AniList, "s", 2);
        var raced = false;
        // Another writer takes the group while the series is being looked up, before the write.
        world.Metadata.Setup(metadata => metadata.GetSeries(series.ID)).Returns(() =>
        {
            if (!raced)
            {
                raced = true;
                world.Service.SaveOrdering(Global("other", series.ID, ("g1", ["s-e2"])));
            }

            return series;
        });

        Assert.Throws<ArgumentException>(() => world.Service.SaveOrdering(Global("o", series.ID, ("g1", ["s-e1"]))));

        Assert.Equal(["other"], world.Service.GetStoredOrderings(TestSources.Plugin).Select(ordering => ordering.ID.ID));
        Assert.Equal("other", world.Tables.Groups.GetByProviderID(TestSources.Plugin, "g1")?.OrderingID);
    }

    [Fact]
    public void RemovingAGlobalOrderingTakesItsGroupsAndItsChoiceAlong()
    {
        var world = new World();
        var series = world.AddSeries(TestSources.AniList, "s", 2);
        var ordering = world.Service.SaveOrdering(Global("o", series.ID, ("g1", ["s-e1", "s-e2"])));
        world.Service.SetPreferredOrdering(series.ID, ordering.ID);

        Assert.True(world.Service.RemoveOrdering(ordering.ID));

        Assert.False(world.Service.RemoveOrdering(ordering.ID));
        Assert.Empty(world.Tables.Groups.GetAll());
        Assert.Empty(world.Tables.Entries.GetAll());
        Assert.Empty(world.Tables.RowState.Preferred);
        Assert.True(world.Service.GetPreferredOrdering(series).IsDefault);
        Assert.Throws<ArgumentException>(() => world.Service.RemoveOrdering(Ordering(MetadataSource.User, "x")));
    }

    #endregion

    #region Local Orderings

    [Fact]
    public void ALocalOrderingIsMadeUnderTheUserSourceWithIDsTheCoreGives()
    {
        var world = new World();
        var series = world.AddSeries(MetadataSource.Shoko, "3", 3);

        var ordering = world.Service.CreateLocalOrdering(Local(series.ID, (null, ["3-e3"]), (null, ["3-e1", "3-e2"])));

        Assert.Equal(MetadataSource.User, ordering.ID.Source);
        Assert.Equal(MetadataEntityType.Ordering, ordering.ID.EntityType);
        Assert.Equal(OrderingType.User, ordering.Type);
        Assert.Equal(string.Empty, ordering.Overview);
        Assert.All(ordering.Seasons, group => Assert.Equal(MetadataSource.User, group.ID.Source));
        Assert.Equal(2, ordering.Seasons.Select(group => group.ID).Distinct().Count());
        Assert.Equal(["3-e3", "3-e1", "3-e2"], ordering.Episodes.Select(episode => episode.ID.ID));
        Assert.Equal(ordering.ID, world.Service.GetOrderings(series)[1].ID);
        Assert.Throws<ArgumentException>(() => world.Service.CreateLocalOrdering(Local(series.ID, (Group(MetadataSource.User, "mine"), ["3-e1"]))));
    }

    [Fact]
    public void AnUpdateKeepsTheGroupsItNamesAndGivesTheOthersNewIDs()
    {
        var world = new World();
        var series = world.AddSeries(MetadataSource.Shoko, "3", 3);
        var ordering = world.Service.CreateLocalOrdering(Local(series.ID, (null, ["3-e1"]), (null, ["3-e2"])));
        var kept = ordering.Seasons[1].ID;

        var updated = world.Service.UpdateLocalOrdering(ordering.ID, Local(series.ID, (kept, ["3-e2", "3-e3"]), (null, ["3-e1"])));

        Assert.NotNull(updated);
        Assert.Equal(ordering.ID, updated.ID);
        Assert.Equal(kept, updated.Seasons[0].ID);
        Assert.NotEqual(ordering.Seasons[0].ID, updated.Seasons[1].ID);
        Assert.Equal(["3-e2", "3-e3", "3-e1"], updated.Episodes.Select(episode => episode.ID.ID));
        Assert.Null(world.Tables.Groups.GetByProviderID(MetadataSource.User, ordering.Seasons[0].ID.ID));

        Assert.Throws<ArgumentException>(() => world.Service.UpdateLocalOrdering(ordering.ID, Local(series.ID, (Group(MetadataSource.User, "unknown"), ["3-e1"]))));
        Assert.Throws<ArgumentException>(() => world.Service.UpdateLocalOrdering(ordering.ID, Local(world.AddSeries(MetadataSource.Shoko, "4", 1).ID)));
        Assert.Throws<ArgumentException>(() => world.Service.UpdateLocalOrdering(Ordering(TestSources.Plugin, "o"), Local(series.ID)));
        Assert.Null(world.Service.UpdateLocalOrdering(Ordering(MetadataSource.User, "gone"), Local(series.ID)));
    }

    [Fact]
    public void ALocalOrderingKeepsItsSpecialGroupAndRefusesASecondOne()
    {
        var world = new World();
        var series = world.AddSeries(MetadataSource.Shoko, "3", 3);
        var data = Local(series.ID, (null, ["3-e3"]), (null, ["3-e1", "3-e2"]));
        data = data with { Groups = [data.Groups[0] with { IsSpecial = true }, data.Groups[1]] };

        var ordering = world.Service.CreateLocalOrdering(data);

        Assert.Equal([0, 1], ordering.Seasons.Select(group => group.SeasonNumber));
        Assert.Equal([true, false], ordering.Seasons.Select(group => group.IsSpecial));
        var special = series.Episodes.Single(item => item.ID.ID == "3-e3");
        Assert.Equal(EpisodeType.Special, world.Service.GetEpisodeOrderings(special).Skip(1).Single().EpisodeType);

        var kept = ordering.Seasons[1].ID;
        var update = Local(series.ID, (kept, ["3-e1", "3-e2"]), (null, ["3-e3"]));
        var updated = world.Service.UpdateLocalOrdering(ordering.ID, update with { Groups = [update.Groups[0] with { IsSpecial = true }, update.Groups[1]] });

        Assert.NotNull(updated);
        Assert.Equal([0, 1], updated.Seasons.Select(group => group.SeasonNumber));
        Assert.Equal(kept, updated.Seasons[0].ID);
        Assert.True(world.Tables.Groups.GetByProviderID(MetadataSource.User, kept.ID)?.IsSpecial);

        var twoSpecial = data with { Groups = [.. data.Groups.Select(group => group with { IsSpecial = true })] };
        Assert.Throws<ArgumentException>(() => world.Service.CreateLocalOrdering(twoSpecial));
        Assert.Throws<ArgumentException>(() => world.Service.UpdateLocalOrdering(ordering.ID, twoSpecial));
        Assert.Single(world.Service.GetStoredOrderings(MetadataSource.User));
        Assert.Equal([0, 1], world.Service.GetOrdering(ordering.ID)!.Seasons.Select(group => group.SeasonNumber));
    }

    [Fact]
    public void DeletingALocalOrderingForgetsIt()
    {
        var world = new World();
        var series = world.AddSeries(MetadataSource.Shoko, "3", 1);
        var ordering = world.Service.CreateLocalOrdering(Local(series.ID, (null, ["3-e1"])));
        world.Service.SetPreferredOrdering(series.ID, ordering.ID);

        Assert.True(world.Service.DeleteLocalOrdering(ordering.ID));

        Assert.False(world.Service.DeleteLocalOrdering(ordering.ID));
        Assert.Null(world.Service.GetOrdering(ordering.ID));
        Assert.True(world.Service.GetPreferredOrdering(series).IsDefault);
        Assert.Throws<ArgumentException>(() => world.Service.DeleteLocalOrdering(Ordering(TestSources.Plugin, "o")));
    }

    [Fact]
    public void ALocalOrderingTakesTheImageLinksOfWhatItDropsAlong()
    {
        var world = new World();
        var series = world.AddSeries(MetadataSource.Shoko, "3", 3);
        var ordering = world.Service.CreateLocalOrdering(Local(series.ID, (null, ["3-e1"]), (null, ["3-e2"])));
        var kept = ordering.Seasons[1].ID;
        var orderingLink = world.Tables.AddImageLink(ordering.ID);
        world.Tables.AddImageLink(ordering.Seasons[0].ID);
        var keptLink = world.Tables.AddImageLink(kept);

        // An update drops the first group, and with it the group's images.
        world.Service.UpdateLocalOrdering(ordering.ID, Local(series.ID, (kept, ["3-e2"]), (null, ["3-e3"])));

        Assert.Equal([orderingLink, keptLink], world.Tables.ImageLinks);

        Assert.True(world.Service.DeleteLocalOrdering(ordering.ID));

        Assert.Empty(world.Tables.ImageLinks);
    }

    [Fact]
    public void ALocalOrderingRemovedWhileAnUpdateIsCheckedStaysRemoved()
    {
        var world = new World();
        var series = world.AddSeries(MetadataSource.Shoko, "3", 2);
        var ordering = world.Service.CreateLocalOrdering(Local(series.ID, (null, ["3-e1"])));
        var raced = false;
        // Another request removes the ordering while the series is being looked up, before the write.
        world.Metadata.Setup(metadata => metadata.GetSeries(series.ID)).Returns(() =>
        {
            if (!raced)
            {
                raced = true;
                world.Service.DeleteLocalOrdering(ordering.ID);
            }

            return series;
        });

        Assert.Null(world.Service.UpdateLocalOrdering(ordering.ID, Local(series.ID, (null, ["3-e2"]))));

        Assert.True(raced);
        Assert.Null(world.Service.GetOrdering(ordering.ID));
        Assert.Empty(world.Tables.Orderings.GetAll());
        Assert.Empty(world.Tables.Groups.GetAll());
        Assert.Empty(world.Tables.Entries.GetAll());
    }

    #endregion

    #region Preferred Ordering

    [Fact]
    public void TheChosenOrderingIsPreferredAndTheDefaultOneIsNot()
    {
        var world = new World();
        var series = world.AddSeries(TestSources.AniList, "s", 2);
        var ordering = world.Service.SaveOrdering(Global("o", series.ID, ("g1", ["s-e2", "s-e1"])));

        Assert.True(world.Service.SetPreferredOrdering(series.ID, ordering.ID));
        Assert.False(world.Service.SetPreferredOrdering(series.ID, ordering.ID));

        Assert.Equal(ordering.ID, world.Service.GetPreferredOrdering(series).ID);
        var orderings = world.Service.GetOrderings(series);
        Assert.False(orderings[0].IsPreferred);
        Assert.True(orderings[1].IsPreferred);

        Assert.True(world.Service.SetPreferredOrdering(series.ID, null));
        Assert.True(world.Service.GetPreferredOrdering(series).IsDefault);
        Assert.False(world.Service.SetPreferredOrdering(series.ID, MetadataOrderingService.DefaultOrderingID(series.ID)));
    }

    [Fact]
    public void AnOrderingOfAnotherSeriesCanNotBeChosen()
    {
        var world = new World();
        var series = world.AddSeries(TestSources.AniList, "s", 1);
        var other = world.AddSeries(TestSources.AniList, "t", 1);
        var ordering = world.Service.SaveOrdering(Global("o", other.ID, ("g1", ["t-e1"])));

        Assert.Throws<ArgumentException>(() => world.Service.SetPreferredOrdering(series.ID, ordering.ID));
        Assert.Throws<ArgumentException>(() => world.Service.SetPreferredOrdering(series.ID, Ordering(TestSources.Plugin, "missing")));
        Assert.Throws<ArgumentException>(() => world.Service.SetPreferredOrdering(new(TestSources.AniList, MetadataEntityType.Series, "missing"), null));
        Assert.Throws<ArgumentException>(() => world.Service.SetPreferredOrdering(Episode(TestSources.AniList, "s-e1"), null));
    }

    [Fact]
    public void NoOrderingCanBeChosenForASeriesWithoutARow()
    {
        var world = new World();
        var series = world.AddSeries(TestSources.AniList, "s", 1);
        var ordering = world.Service.SaveOrdering(Global("o", series.ID, ("g1", ["s-e1"])));
        world.Tables.RowState.NotStored.Add(series.ID);

        Assert.Throws<ArgumentException>(() => world.Service.SetPreferredOrdering(series.ID, ordering.ID));
        Assert.Throws<ArgumentException>(() => world.Service.SetPreferredOrdering(series.ID, null));
        Assert.True(world.Service.GetPreferredOrdering(series).IsDefault);
        Assert.Empty(world.Tables.RowState.Preferred);
    }

    [Fact]
    public void AChoiceOfAnOrderingThatIsGoneFallsBackToTheDefaultOne()
    {
        var world = new World();
        var series = world.AddSeries(TestSources.AniList, "s", 1);
        world.Tables.RowState.Preferred[series.ID] = Ordering(TestSources.Plugin, "gone");

        var preferred = world.Service.GetPreferredOrdering(series);

        Assert.True(preferred.IsDefault);
        Assert.True(preferred.IsPreferred);
    }

    [Fact]
    public void ACoreSourcesOwnOrderingsAreReadAndCanBeChosen()
    {
        var coreOrdering = new Mock<IOrdering>();
        var source = new Mock<ICoreOrderingSource>();
        var world = new World([source.Object]);
        var series = world.AddSeries(MetadataSource.TMDB, "10", 1);
        var orderingID = Ordering(MetadataSource.TMDB, "5f1a2b3c4d5e6f7a8b9c0d1e");
        coreOrdering.SetupGet(ordering => ordering.ID).Returns(orderingID);
        coreOrdering.SetupGet(ordering => ordering.SeriesID).Returns(series.ID);
        source.SetupGet(core => core.Source).Returns(MetadataSource.TMDB);
        source.Setup(core => core.GetOrderings(series)).Returns([coreOrdering.Object]);
        source.Setup(core => core.GetOrdering(orderingID)).Returns(coreOrdering.Object);
        source.Setup(core => core.GetEpisodeOrderings(It.IsAny<IEpisode>())).Returns([]);

        Assert.Equal([MetadataOrderingService.DefaultOrderingID(series.ID), orderingID], world.Service.GetOrderings(series).Select(ordering => ordering.ID));
        Assert.True(world.Service.SetPreferredOrdering(series.ID, orderingID));
        Assert.Same(coreOrdering.Object, world.Service.GetPreferredOrdering(series));
        Assert.Same(coreOrdering.Object, world.Service.GetOrdering(orderingID));
    }

    #endregion

    #region Episode Orderings

    [Fact]
    public void AnEpisodeHasAPlaceInTheDefaultOrderingAndOneForEachGroupItIsIn()
    {
        var world = new World();
        var series = world.AddSeries(TestSources.AniList, "s", 2, 2);
        var ordering = world.Service.SaveOrdering(Global("o", series.ID, ("g1", ["s-e4", "s-e3"]), ("g2", ["s-e1", "s-e2", "s-e3"])));
        world.Service.SetPreferredOrdering(series.ID, ordering.ID);
        var episode = series.Episodes.Single(item => item.ID.ID == "s-e3");
        Mock.Get(episode).SetupGet(value => value.Type).Returns(EpisodeType.Other);

        var places = world.Service.GetEpisodeOrderings(episode);

        Assert.Equal(3, places.Count);
        Assert.True(places[0].IsDefault);
        Assert.False(places[0].IsPreferred);
        Assert.Equal((2, 1), (places[0].SeasonNumber, places[0].EpisodeNumber));
        // The default ordering keeps the episode's own type; the others know no specials here.
        Assert.Equal([EpisodeType.Other, EpisodeType.Episode, EpisodeType.Episode], places.Select(place => place.EpisodeType));
        Assert.Equal(Group(TestSources.AniList, "s-s2"), places[0].SeasonID);
        Assert.Equal(MetadataOrderingService.DefaultOrderingID(series.ID), places[0].OrderingID);
        Assert.Equal((1, 2), (places[1].SeasonNumber, places[1].EpisodeNumber));
        Assert.Equal((2, 3), (places[2].SeasonNumber, places[2].EpisodeNumber));
        Assert.All(places.Skip(1), place =>
        {
            Assert.Equal(ordering.ID, place.OrderingID);
            Assert.True(place.IsPreferred);
            Assert.False(place.IsDefault);
            Assert.Same(episode, place.Episode);
            Assert.Equal(place.SeasonID, place.Season?.ID);
        });
    }

    [Fact]
    public void AGroupIsFoundAsASeason()
    {
        var world = new World();
        var series = world.AddSeries(MetadataSource.Shoko, "3", 2);
        var ordering = world.Service.CreateLocalOrdering(Local(series.ID, (null, ["3-e2"])));
        var groupID = ordering.Seasons[0].ID;

        var group = world.Service.GetGroup(groupID);

        Assert.NotNull(group);
        Assert.Equal(ordering.ID, group.OrderingID);
        Assert.Equal(["3-e2"], group.Episodes.Select(episode => episode.ID.ID));
        Assert.Null(world.Service.GetGroup(Group(MetadataSource.User, "missing")));
    }

    #endregion

    #region Hidden Episodes

    [Fact]
    public void AnyEpisodeCanBeHiddenAndTheOrderingsCountIt()
    {
        var world = new World();
        var series = world.AddSeries(TestSources.AniList, "s", 3);
        var ordering = world.Service.SaveOrdering(Global("o", series.ID, ("g1", ["s-e1", "s-e2"])));
        var hidden = Episode(TestSources.AniList, "s-e2");

        Assert.True(world.Service.SetEpisodeHidden(hidden, true));
        Assert.False(world.Service.SetEpisodeHidden(hidden, true));

        Assert.True(world.Service.IsEpisodeHidden(hidden));
        Assert.False(world.Service.IsEpisodeHidden(Episode(TestSources.AniList, "s-e1")));
        Assert.Equal(1, world.Service.GetDefaultOrdering(series).HiddenEpisodeCount);
        Assert.Equal(1, world.Service.GetOrdering(ordering.ID)?.HiddenEpisodeCount);

        Assert.True(world.Service.SetEpisodeHidden(hidden, false));
        Assert.False(world.Service.SetEpisodeHidden(hidden, false));
        Assert.False(world.Service.IsEpisodeHidden(hidden));
        Assert.Throws<ArgumentException>(() => world.Service.IsEpisodeHidden(new(TestSources.AniList, MetadataEntityType.Series, "s")));
    }

    [Fact]
    public void OnlyAnAvailableEpisodeCanBeHidden()
    {
        var world = new World();
        world.AddSeries(TestSources.AniList, "s", 1);
        var missing = Episode(TestSources.AniList, "missing");
        var unregistered = Episode(MetadataSource.Parse("unregistered-test-source"), "1");

        Assert.Throws<ArgumentException>(() => world.Service.SetEpisodeHidden(missing, true));
        Assert.Throws<ArgumentException>(() => world.Service.SetEpisodeHidden(unregistered, true));
        Assert.False(world.Service.SetEpisodeHidden(missing, false));
        Assert.False(world.Service.SetEpisodeHidden(unregistered, false));
        Assert.Empty(world.Tables.RowState.Hidden);

        // An episode without a row of its own, as one only a resolver serves.
        var served = Episode(TestSources.AniList, "s-e1");
        world.Tables.RowState.NotStored.Add(served);
        Assert.Throws<ArgumentException>(() => world.Service.SetEpisodeHidden(served, true));
        Assert.False(world.Service.SetEpisodeHidden(served, false));
        Assert.False(world.Service.IsEpisodeHidden(served));
        Assert.Empty(world.Tables.RowState.Hidden);
    }

    [Fact]
    public void AHiddenFlagGoesWithItsEpisode()
    {
        var world = new World();
        var series = world.AddSeries(TestSources.AniList, "s", 2);
        var gone = Episode(TestSources.AniList, "s-e1");
        Assert.True(world.Service.SetEpisodeHidden(gone, true));
        Assert.True(world.Service.SetEpisodeHidden(Episode(TestSources.AniList, "s-e2"), true));

        // The episode's row goes, as when a refresh drops it.
        world.Metadata.Setup(metadata => metadata.GetEpisode(gone)).Returns((IEpisode?)null);

        Assert.False(world.Service.IsEpisodeHidden(gone));
        Assert.False(world.Service.SetEpisodeHidden(gone, false));
        Assert.True(world.Service.IsEpisodeHidden(Episode(TestSources.AniList, "s-e2")));
        Assert.Equal(1, world.Service.GetDefaultOrdering(series).HiddenEpisodeCount);
    }

    [Fact]
    public void AShokoEpisodeIsHiddenByItsOwnFlag()
    {
        var world = new World(null, new AnimeEpisode { AnimeEpisodeID = 4, IsHidden = true }, new AnimeEpisode { AnimeEpisodeID = 5 });

        Assert.True(world.Service.IsEpisodeHidden(Episode(MetadataSource.Shoko, "4")));
        Assert.False(world.Service.IsEpisodeHidden(Episode(MetadataSource.Shoko, "5")));
        Assert.False(world.Service.IsEpisodeHidden(Episode(MetadataSource.Shoko, "6")));
        Assert.Throws<ArgumentException>(() => world.Service.SetEpisodeHidden(Episode(MetadataSource.Shoko, "6"), true));
        Assert.False(world.Service.SetEpisodeHidden(Episode(MetadataSource.Shoko, "4"), true));
        Assert.Empty(world.Tables.RowState.Hidden);
    }

    #endregion

    #region Series Removal

    [Fact]
    public void RemovingASeriesTakesEveryOrderingOfIt()
    {
        var world = new World();
        var series = world.AddSeries(TestSources.AniList, "s", 3);
        var other = world.AddSeries(TestSources.AniList, "t", 2);
        var global = world.Service.SaveOrdering(Global("o", series.ID, ("g1", ["s-e1", "s-e2"])));
        var local = world.Service.CreateLocalOrdering(Local(series.ID, (null, ["s-e3"])));
        var kept = world.Service.SaveOrdering(Global("p", other.ID, ("g2", ["t-e1"])));
        Assert.True(world.Service.SetPreferredOrdering(series.ID, local.ID));
        Assert.True(world.Service.SetPreferredOrdering(other.ID, kept.ID));
        Assert.True(world.Service.SetEpisodeHidden(Episode(TestSources.AniList, "s-e2"), true));
        Assert.True(world.Service.SetEpisodeHidden(Episode(TestSources.AniList, "t-e2"), true));

        var removed = world.Service.RemoveForSeries(series.ID);

        // The choice and the hidden flags go with the rows, which the caller removes.
        Assert.Equal(2, removed);
        Assert.Null(world.Service.GetOrdering(global.ID));
        Assert.Null(world.Service.GetOrdering(local.ID));
        Assert.All(world.Tables.Groups.GetAll(), group => Assert.Equal("p", group.OrderingID));
        Assert.All(world.Tables.Entries.GetAll(), entry => Assert.Equal("p", entry.OrderingID));

        Assert.NotNull(world.Service.GetOrdering(kept.ID));
        Assert.Equal(kept.ID, world.Service.GetPreferredOrdering(other).ID);
        Assert.True(world.Service.IsEpisodeHidden(Episode(TestSources.AniList, "t-e2")));

        Assert.Equal(0, world.Service.RemoveForSeries(series.ID));
    }

    [Fact]
    public void RemovingASeriesThatIsAlreadyGoneStillTakesItsOrderings()
    {
        var world = new World();
        var series = new MetadataGuid(TestSources.AniList, MetadataEntityType.Series, "gone");
        world.Tables.Orderings.Cache.Update(new Metadata_Ordering
        {
            Metadata_OrderingID = 1,
            Source = MetadataSource.User,
            ProviderID = "mine",
            SeriesSource = series.Source,
            SeriesID = series.ID,
            Type = OrderingType.User,
            Name = "Mine",
        });
        world.Tables.Groups.Cache.Update(new Metadata_Ordering_Group { Metadata_Ordering_GroupID = 1, Source = MetadataSource.User, ProviderID = "part", OrderingID = "mine", Name = "Part" });

        Assert.Equal(1, world.Service.RemoveForSeries(series));

        Assert.Empty(world.Tables.Orderings.GetAll());
        Assert.Empty(world.Tables.Groups.GetAll());
    }

    [Fact]
    public void RemovingASeriesWithoutOrderingsRemovesNothingAndRefusesAnotherKind()
    {
        var world = new World(null, new AnimeEpisode { AnimeEpisodeID = 4, IsHidden = true });

        Assert.Equal(0, world.Service.RemoveForSeries(new(MetadataSource.Shoko, MetadataEntityType.Series, "1")));
        Assert.True(world.Service.IsEpisodeHidden(Episode(MetadataSource.Shoko, "4")));
        Assert.Throws<ArgumentException>(() => world.Service.RemoveForSeries(Episode(MetadataSource.Shoko, "4")));
        Assert.Throws<ArgumentNullException>(() => world.Service.RemoveForSeries(null!));
    }

    [Fact]
    public void RemovingASeriesTakesTheImageLinksOfItsOrderingsAndTheirGroups()
    {
        var world = new World();
        var series = world.AddSeries(TestSources.AniList, "s", 2);
        var other = world.AddSeries(TestSources.AniList, "t", 1);
        var local = world.Service.CreateLocalOrdering(Local(series.ID, (null, ["s-e1"]), (null, ["s-e2"])));
        var kept = world.Service.CreateLocalOrdering(Local(other.ID, (null, ["t-e1"])));
        world.Tables.AddImageLink(local.ID);
        world.Tables.AddImageLink(local.Seasons[1].ID);
        var keptLink = world.Tables.AddImageLink(kept.Seasons[0].ID);

        world.Service.RemoveForSeries(series.ID);

        Assert.Equal([keptLink], world.Tables.ImageLinks);
    }

    #endregion
}
