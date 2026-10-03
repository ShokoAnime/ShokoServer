using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.Options;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.API.v3.Models.Ordering;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Services;
using Shoko.Server.Services.Ordering;
using Shoko.Tests.API.Metadata;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers placed specials across the orderings: a special in the special
/// group and a regular one stays a special with one place, which says where
/// it airs, in stored orderings, in a series' default ordering (AniDB titles
/// and a plugin's airing places) and in the APIv3 groups.
/// </summary>
public class PlacedSpecialsTests
{
    #region Helpers

    /// <summary>
    /// Series and episodes the service can find, with the service over
    /// in-memory tables.
    /// </summary>
    private sealed class World
    {
        private readonly Dictionary<MetadataGuid, IMetadata> _entries = [];

        public OrderingTables Tables { get; } = new();

        public MetadataOrderingService Service { get; }

        public World()
        {
            var metadata = new Mock<IMetadataService>();
            metadata.Setup(service => service.GetSeries(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) => _entries.GetValueOrDefault(id) as ISeries);
            metadata.Setup(service => service.GetEpisode(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) => _entries.GetValueOrDefault(id) as IEpisode);
            Service = Tables.Build(() => metadata.Object);
        }

        /// <summary>
        /// Adds a series whose episodes are named <c>&lt;id&gt;-&lt;name&gt;</c>,
        /// in seasons named <c>&lt;id&gt;-s&lt;number&gt;</c> in the order they
        /// first come; season 0 holds the specials.
        /// </summary>
        public ISeries Add(MetadataSource source, string id, params Ep[] episodes)
        {
            var seriesID = new MetadataGuid(source, MetadataEntityType.Series, id);
            var series = new Mock<ISeries>();
            var seasons = episodes.Select(episode => episode.Season).Distinct().Select(number => Season(source, seriesID, id, number)).ToList();
            var built = episodes.Select(episode =>
            {
                var mock = new Mock<IEpisode>();
                mock.SetupGet(value => value.ID).Returns(new MetadataGuid(source, MetadataEntityType.Episode, $"{id}-{episode.Name}"));
                mock.SetupGet(value => value.SeriesID).Returns(seriesID);
                mock.SetupGet(value => value.SeasonID).Returns(new MetadataGuid(source, MetadataEntityType.Season, $"{id}-s{episode.Season}"));
                mock.SetupGet(value => value.SeasonNumber).Returns(episode.Season);
                mock.SetupGet(value => value.EpisodeNumber).Returns(episode.Number);
                mock.SetupGet(value => value.Type).Returns(episode.Season is 0 ? EpisodeType.Special : EpisodeType.Episode);
                mock.SetupGet(value => value.Titles).Returns(episode.Title is null ? [] : [new FakeMetadataEntries.FakeTitle(episode.Title, source: MetadataSource.AniDB)]);
                mock.SetupGet(value => value.Series).Returns(() => series.Object);
                return mock.Object;
            }).ToList();
            return Finish(series, seriesID, seasons, built);
        }

        /// <summary>
        /// Adds a plugin source's series made of stored episodes, in seasons
        /// in the order they first come.
        /// </summary>
        public ISeries AddStored(string id, params Metadata_Episode[] episodes)
        {
            var seriesID = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, id);
            var series = new Mock<ISeries>();
            foreach (var episode in episodes)
            {
                episode.Source = TestSources.Plugin;
                episode.SeriesID = id;
                episode.SeasonID = $"{id}-s{episode.SeasonNumber}";
            }

            var seasons = episodes.Select(episode => episode.SeasonNumber!.Value).Distinct().Select(number => Season(TestSources.Plugin, seriesID, id, number)).ToList();
            return Finish(series, seriesID, seasons, [.. episodes]);
        }

        private ISeries Finish(Mock<ISeries> series, MetadataGuid seriesID, List<ISeason> seasons, List<IEpisode> episodes)
        {
            series.SetupGet(value => value.ID).Returns(seriesID);
            series.SetupGet(value => value.Seasons).Returns(seasons);
            series.SetupGet(value => value.Episodes).Returns(episodes);
            _entries[seriesID] = series.Object;
            foreach (var episode in episodes)
                _entries[episode.ID] = episode;
            return series.Object;
        }

        private static ISeason Season(MetadataSource source, MetadataGuid seriesID, string id, int number)
        {
            var season = new Mock<ISeason>();
            season.SetupGet(value => value.ID).Returns(new MetadataGuid(source, MetadataEntityType.Season, $"{id}-s{number}"));
            season.SetupGet(value => value.SeriesID).Returns(seriesID);
            season.SetupGet(value => value.SeasonNumber).Returns(number);
            season.SetupGet(value => value.IsSpecial).Returns(number is 0);
            return season.Object;
        }
    }

    /// <summary>
    /// An episode to add: its name, season, own number and AniDB title.
    /// </summary>
    private sealed record Ep(string Name, int Season, int Number, string? Title = null);

    private static MetadataGuid EpisodeID(ISeries series, string name)
        => new(series.ID.Source, MetadataEntityType.Episode, $"{series.ID.ID}-{name}");

    private static IEpisode Episode(ISeries series, string name)
        => series.Episodes.Single(episode => episode.ID == EpisodeID(series, name));

    private static string[] Names(IEnumerable<IEpisode> episodes)
        => [.. episodes.Select(episode => episode.ID.ID[(episode.ID.ID.IndexOf('-') + 1)..])];

    private static (int? Before, int? Number, int? After, string? AfterID, string? BeforeID) Airs(IEpisodeOrderingInformation place)
        => (place.AirsBeforeSeasonNumber, place.AirsBeforeEpisodeNumber, place.AirsAfterSeasonNumber, place.AirsAfterEpisodeID?.ID, place.AirsBeforeEpisodeID?.ID);

    private static (int?, int) Numbers(World world, ISeries series, string name)
    {
        var place = world.Service.GetEpisodeOrderings(Episode(series, name)).Single(place => !place.IsDefault);
        return (place.SeasonNumber, place.EpisodeNumber);
    }

    private static MetadataLocalOrderingData Local(ISeries series, params (bool IsSpecial, string[] Episodes)[] groups)
        => new()
        {
            SeriesID = series.ID,
            Name = "Mine",
            Groups = [.. groups.Select((group, index) => new MetadataLocalOrderingGroupData
            {
                Name = $"Part {index + 1}",
                IsSpecial = group.IsSpecial,
                Episodes = [.. group.Episodes.Select(name => EpisodeID(series, name))],
            })],
        };

    /// <summary>
    /// A user's ordering of a Shoko-like series with four regular episodes
    /// and one special, the special group last, the special airing between
    /// <c>e1</c> and <c>e2</c>.
    /// </summary>
    private static (World World, ISeries Series, IOrdering Ordering) SpecialGroupLast()
    {
        var world = new World();
        var series = world.Add(TestSources.Plugin, "a", new("e1", 1, 1), new("e2", 1, 2), new("e3", 1, 3), new("e4", 2, 1), new("sp", 0, 1));
        var ordering = world.Service.CreateLocalOrdering(Local(series, (false, ["e1", "sp", "e2", "e3"]), (false, ["e4"]), (true, ["sp"])));
        return (world, series, ordering);
    }

    /// <summary>
    /// A season mock standing for one of an ordering's groups in the APIv3 model.
    /// </summary>
    private static ISeason GroupModel(ISeason group)
    {
        var season = new Mock<ISeason>();
        season.SetupGet(value => value.ID).Returns(group.ID);
        season.SetupGet(value => value.Title).Returns(group.Title);
        season.SetupGet(value => value.SeasonNumber).Returns(group.SeasonNumber);
        season.SetupGet(value => value.IsSpecial).Returns(group.IsSpecial);
        season.As<IWithImages>().Setup(value => value.GetImages(It.IsAny<ImageFilteringOptions?>())).Returns([]);
        return season.Object;
    }

    #endregion

    #region Stored Orderings

    [Fact]
    public void APlacedSpecialStaysInTheSpecialGroupAndOnlyAirsInTheRegularOne()
    {
        var (world, series, ordering) = SpecialGroupLast();

        Assert.Equal([1, 2, 0], ordering.Seasons.Select(group => group.SeasonNumber));
        Assert.Equal(["e1", "e2", "e3"], Names(ordering.Seasons[0].Episodes));
        Assert.Equal(["sp"], Names(ordering.Seasons[2].Episodes));
        Assert.Equal(["e1", "sp", "e2", "e3", "e4"], Names(ordering.Episodes));
        Assert.Equal(5, ordering.EpisodeCount);

        var special = world.Service.GetEpisodeOrderings(Episode(series, "sp"));
        var place = Assert.Single(special, place => !place.IsDefault);
        Assert.Equal((0, 1, EpisodeType.Special), (place.SeasonNumber, place.EpisodeNumber, place.EpisodeType));
        Assert.Equal(ordering.Seasons[2].ID, place.SeasonID);
        Assert.Equal((1, 2, null, "a-e1", "a-e2"), Airs(place));
        Assert.Equal((1, 2), Numbers(world, series, "e2"));
        Assert.Equal((1, 3), Numbers(world, series, "e3"));
    }

    [Fact]
    public void AChosenOrderingsPlaceOfAPlacedSpecialIsItsSpecialGroupPlace()
    {
        var (world, series, ordering) = SpecialGroupLast();
        world.Tables.RowState.Preferred[series.ID] = ordering.ID;

        var preferred = world.Service.GetEpisodeOrderings(Episode(series, "sp")).First(place => place.IsPreferred);

        Assert.Equal(ordering.ID, preferred.OrderingID);
        Assert.Equal((0, 1, EpisodeType.Special), (preferred.SeasonNumber, preferred.EpisodeNumber, preferred.EpisodeType));
        Assert.Equal((1, 2, null, "a-e1", "a-e2"), Airs(preferred));
    }

    [Fact]
    public void ASpecialGroupFirstWithASpecialAfterTheLastEpisodeOfAGroupAirsAfterThatSeason()
    {
        var world = new World();
        var series = world.Add(TestSources.Plugin, "b", new("e1", 1, 1), new("e2", 1, 2), new("e3", 2, 1), new("sp", 0, 1), new("x", 0, 2));
        var ordering = world.Service.SaveOrdering(new()
        {
            ID = new(TestSources.Plugin, MetadataEntityType.Ordering, "dvd"),
            SeriesID = series.ID,
            Name = "DVD",
            Type = OrderingType.DVD,
            Groups =
            [
                new() { ID = new(TestSources.Plugin, MetadataEntityType.Season, "g0"), Name = "Specials", IsSpecial = true, Episodes = [EpisodeID(series, "x"), EpisodeID(series, "sp")] },
                new() { ID = new(TestSources.Plugin, MetadataEntityType.Season, "g1"), Name = "One", Episodes = [EpisodeID(series, "e1"), EpisodeID(series, "e2"), EpisodeID(series, "sp")] },
                new() { ID = new(TestSources.Plugin, MetadataEntityType.Season, "g2"), Name = "Two", Episodes = [EpisodeID(series, "e3")] },
            ],
        });

        Assert.Equal([0, 1, 2], ordering.Seasons.Select(group => group.SeasonNumber));
        Assert.Equal(["x", "e1", "e2", "sp", "e3"], Names(ordering.Episodes));
        Assert.Equal(5, ordering.EpisodeCount);
        var place = Assert.Single(world.Service.GetEpisodeOrderings(Episode(series, "sp")), place => !place.IsDefault);
        Assert.Equal((0, 2), (place.SeasonNumber, place.EpisodeNumber));
        Assert.Equal((null, null, 1, "b-e2", "b-e3"), Airs(place));
        var unplaced = Assert.Single(world.Service.GetEpisodeOrderings(Episode(series, "x")), place => !place.IsDefault);
        Assert.Equal((null, null, null, null, null), Airs(unplaced));
        Assert.Equal((2, 1), Numbers(world, series, "e3"));
    }

    #endregion

    #region API

    [Fact]
    public void TheApiListsAPlacedSpecialInBothGroupsAndOnlyTheSpecialGroupSaysWhereItAirs()
    {
        var (_, series, ordering) = SpecialGroupLast();
        var placement = ((IPlacedOrdering)ordering).Placement;

        var regular = new SeriesOrdering.OrderingGroup(GroupModel(ordering.Seasons[0]), placement);
        var specials = new SeriesOrdering.OrderingGroup(GroupModel(ordering.Seasons[2]), placement);

        Assert.Equal(
            [("a-e1", 1, false), ("a-sp", 1, true), ("a-e2", 2, false), ("a-e3", 3, false)],
            regular.Episodes.Select(episode => (MetadataGuid.Parse(episode.ID, null).ID, episode.EpisodeNumber, episode.IsSpecial))
        );
        var json = JObject.FromObject(regular.Episodes[1]);
        Assert.False(json.ContainsKey(nameof(SeriesOrdering.OrderingEpisode.AirsBeforeEpisodeNumber)));
        var special = Assert.Single(specials.Episodes);
        Assert.True(special.IsSpecial);
        Assert.Equal((1, 1, 2, (int?)null), (special.EpisodeNumber, special.AirsBeforeSeasonNumber, special.AirsBeforeEpisodeNumber, special.AirsAfterSeasonNumber));
        Assert.Equal((EpisodeID(series, "e1").ToString(), EpisodeID(series, "e2").ToString()), (special.AirsAfterEpisodeID, special.AirsBeforeEpisodeID));
    }

    [Fact]
    public void TheGroupsTheApiListsRoundTripThroughAnUpdate()
    {
        var (world, series, ordering) = SpecialGroupLast();
        var placement = ((IPlacedOrdering)ordering).Placement;
        var listed = ordering.Seasons.Select(group => (group.ID, group.IsSpecial, Episodes: placement.Listed(group.ID).Select(place => place.EpisodeID).ToList())).ToList();
        var before = world.Tables.Entries.GetAll().Select(entry => (entry.GroupID, entry.EpisodeID, entry.Position)).Order().ToList();

        var updated = world.Service.UpdateLocalOrdering(ordering.ID, new()
        {
            SeriesID = series.ID,
            Name = ordering.Name,
            Groups = [.. listed.Select(group => new MetadataLocalOrderingGroupData { ID = group.ID, Name = "Part", IsSpecial = group.IsSpecial, Episodes = group.Episodes })],
        });

        Assert.NotNull(updated);
        Assert.Equal(before, world.Tables.Entries.GetAll().Select(entry => (entry.GroupID, entry.EpisodeID, entry.Position)).Order());
        Assert.Equal(["e1", "sp", "e2", "e3", "e4"], Names(updated.Episodes));
    }

    #endregion

    #region Default Ordering

    [Fact]
    public void AnAnidbSpecialIsPlacedByItsTitle()
    {
        var world = new World();
        var series = world.Add(
            MetadataSource.AniDB,
            "9",
            new("e1", 1, 1),
            new("e2", 1, 2),
            new("e3", 1, 3),
            new("s1", 0, 1, "Episode 1.5"),
            new("s2", 0, 2, "Recap"),
            new("s3", 0, 3, "Episode 0")
        );

        var ordering = world.Service.GetDefaultOrdering(series);
        var place = world.Service.GetEpisodeOrderings(Episode(series, "s1"))[0];

        Assert.Equal([1, 0], ordering.Seasons.Select(season => season.SeasonNumber));
        Assert.Equal((0, 1, EpisodeType.Special), (place.SeasonNumber, place.EpisodeNumber, place.EpisodeType));
        Assert.Equal(["s3", "e1", "s1", "e2", "e3", "s2"], Names(ordering.Episodes));
        Assert.Equal((1, 2, null, "9-e1", "9-e2"), Airs(place));
        Assert.Equal((1, 1, null, null, "9-e1"), Airs(world.Service.GetEpisodeOrderings(Episode(series, "s3"))[0]));

        Assert.Equal((null, null, null, null, null), Airs(world.Service.GetEpisodeOrderings(Episode(series, "s2"))[0]));
        Assert.Equal(6, ordering.EpisodeCount);
    }

    [Fact]
    public void TheDefaultPlacementIsKeptUntilTheSeriesIsUpdated()
    {
        var world = new World();
        var special = Stored("sp", 0, 1, new() { AirsAfterSeasonNumber = 1 });
        var series = world.AddStored($"kept-{Guid.NewGuid():N}", Stored("e1", 1, 1), Stored("e2", 2, 1), special);
        Assert.Equal(["e1", "sp", "e2"], Names(world.Service.GetDefaultOrdering(series).Episodes));

        special.ExtraData = new() { AirsAfterSeasonNumber = 2 };
        Assert.Equal(["e1", "sp", "e2"], Names(world.Service.GetDefaultOrdering(series).Episodes));

        // What the series and episode update events call; raising them would reach every other test's services.
        world.Service.ForgetDefaultPlacement(series.ID);
        Assert.Equal(["e1", "e2", "sp"], Names(world.Service.GetDefaultOrdering(series).Episodes));
    }

    [Fact]
    public void TheApiListsAnAnidbSpecialPlacedByItsTitleInTheRegularSeasonToo()
    {
        var world = new World();
        var series = world.Add(MetadataSource.AniDB, "9", new("e1", 1, 1), new("e2", 1, 2), new("s1", 0, 1, "Episode 1.5"));
        var ordering = world.Service.GetDefaultOrdering(series);
        var placement = ((IPlacedOrdering)ordering).Placement;

        var regular = new SeriesOrdering.OrderingGroup(GroupModel(ordering.Seasons[0]), placement);
        var specials = new SeriesOrdering.OrderingGroup(GroupModel(ordering.Seasons[1]), placement);

        Assert.Equal([("9-e1", 1, false), ("9-s1", 1, true), ("9-e2", 2, false)], regular.Episodes.Select(episode => (MetadataGuid.Parse(episode.ID, null).ID, episode.EpisodeNumber, episode.IsSpecial)));
        Assert.Equal((1, 2), (Assert.Single(specials.Episodes).AirsBeforeSeasonNumber, specials.Episodes[0].AirsBeforeEpisodeNumber));
    }

    [Fact]
    public void APluginSeriesPlacesItsSpecialsWhereItsProviderSaidTheyAirAndIgnoresMissingTargets()
    {
        var world = new World();
        var series = world.AddStored(
            "p",
            Stored("e1", 1, 1),
            Stored("e2", 1, 2),
            Stored("e3", 2, 1),
            Stored("s1", 0, 1, new() { AirsBeforeSeasonNumber = 1, AirsBeforeEpisodeNumber = 2 }),
            Stored("s2", 0, 2, new() { AirsAfterSeasonNumber = 1 }),
            Stored("s3", 0, 3, new() { AirsBeforeSeasonNumber = 5, AirsBeforeEpisodeNumber = 1 }),
            Stored("s4", 0, 4, new() { AirsBeforeSeasonNumber = 2, AirsBeforeEpisodeNumber = 9, AirsAfterSeasonNumber = 2 })
        );

        var ordering = world.Service.GetDefaultOrdering(series);

        Assert.Equal(["e1", "s1", "e2", "s2", "e3", "s4", "s3"], Names(ordering.Episodes));
        Assert.Equal(7, ordering.EpisodeCount);
        Assert.Equal((1, 2, null, "p-e1", "p-e2"), Airs(DefaultPlace("s1")));
        Assert.Equal((null, null, 1, "p-e2", "p-e3"), Airs(DefaultPlace("s2")));
        Assert.Equal((null, null, null, null, null), Airs(DefaultPlace("s3")));
        Assert.Equal((null, null, 2, "p-e3", null), Airs(DefaultPlace("s4")));
        Assert.Equal((0, 4, EpisodeType.Special), (DefaultPlace("s4").SeasonNumber, DefaultPlace("s4").EpisodeNumber, DefaultPlace("s4").EpisodeType));

        // A stored episode finds its series through the repositories, so its place is built here.
        IEpisodeOrderingInformation DefaultPlace(string name)
            => new DefaultEpisodeOrdering(Episode(series, name), series, world.Service);
    }

    private static Metadata_Episode Stored(string name, int season, int number, Metadata_EpisodeExtra? extra = null)
        => new()
        {
            ProviderID = $"p-{name}",
            SeasonNumber = season,
            EpisodeNumber = number,
            Type = season is 0 ? EpisodeType.Special : EpisodeType.Episode,
            ExtraData = extra,
        };

    #endregion
}
