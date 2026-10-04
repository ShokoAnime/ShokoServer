using System;
using System.Linq;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Services;
using Shoko.Server.Utilities;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the titles and overviews of an ordering and its groups: kept in the text store under
/// their own IDs, without a group's generic name for its own season number, and synthesized when
/// there are none.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class OrderingTextTests
{
    #region Helpers

    /// <summary>
    /// The ordering service over in-memory tables, with a series of four episodes and a text manager
    /// over the tables' text store put in place for the models.
    /// </summary>
    private sealed class World : IDisposable
    {
        private readonly RepoFactoryScope _scope;

        public OrderingTables Tables { get; } = new();

        public MetadataOrderingService Service { get; }

        public MetadataGuid SeriesID { get; } = new(TestSources.AniList, MetadataEntityType.Series, "s");

        public World()
        {
            var episodes = Enumerable.Range(1, 4)
                .Select(number => Mock.Of<IEpisode>(episode => episode.ID == Episode(number) && episode.SeriesID == SeriesID))
                .ToList();
            var series = Mock.Of<ISeries>(value => value.ID == SeriesID && value.Episodes == episodes);
            var metadata = new Mock<IMetadataService>();
            metadata.Setup(service => service.GetSeries(SeriesID)).Returns(series);
            metadata.Setup(service => service.GetEpisode(It.IsAny<MetadataGuid>()))
                .Returns((MetadataGuid id) => episodes.FirstOrDefault(episode => episode.ID == id));
            Service = Tables.Build(() => metadata.Object);
            _scope = new RepoFactoryScope().Set(TestTextManager.Build(Tables.TextStore));
        }

        public static MetadataGuid Episode(int number)
            => new(TestSources.AniList, MetadataEntityType.Episode, $"e{number}");

        public void Dispose()
            => _scope.Dispose();
    }

    private static MetadataGuid Group(string id)
        => new(TestSources.Plugin, MetadataEntityType.Season, id);

    private static MetadataOrderingData Global(MetadataGuid seriesID, params MetadataOrderingGroupData[] groups)
        => new() { ID = new(TestSources.Plugin, MetadataEntityType.Ordering, "o"), SeriesID = seriesID, Type = OrderingType.DVD, Groups = groups };

    private static string[] Synthesized(int seasonNumber)
    {
        var languages = Languages.PreferredEpisodeNamingLanguages.Select(language => language.Language).Where(language => language is not TitleLanguage.Main);
        return [.. GenericEpisodeTitles.SynthesizeSeasonAll(seasonNumber, languages).Select(title => title.Value)];
    }

    #endregion

    [Fact]
    public void AnOrderingsAndItsGroupsTextsAreStoredUnderTheirOwnIDsAndGoWithThem()
    {
        using var world = new World();
        var data = Global(
            world.SeriesID,
            new() { ID = Group("g1"), Titles = TestTexts.Named("Disc 1"), Overviews = TestTexts.Described("The first disc."), Episodes = [World.Episode(1)] },
            new() { ID = Group("g2"), Titles = TestTexts.Named("Disc 2"), Episodes = [World.Episode(2)] }
        ) with
        {
            Titles = TestTexts.Named("DVD Order"),
            Overviews = TestTexts.Described("As on the discs."),
        };

        var ordering = world.Service.SaveOrdering(data);

        Assert.Equal(("DVD Order", "As on the discs."), (ordering.Title, ordering.DefaultOverview?.Value));
        Assert.Equal(["Disc 1", "Disc 2"], ordering.Seasons.Select(group => group.Title));
        Assert.Equal("The first disc.", ordering.Seasons[0].DefaultOverview?.Value);
        Assert.All(world.Tables.TextStore.GetTitles(ordering.ID), title => Assert.Equal(TestSources.Plugin, title.Source));

        // A group left out takes its texts along, and removing the ordering takes the rest.
        world.Service.SaveOrdering(data with { Groups = [data.Groups[0]] });
        Assert.Empty(world.Tables.Texts.GetRows(Group("g2")));
        world.Service.RemoveOrdering(ordering.ID);
        Assert.Empty(world.Tables.Texts.GetRows(ordering.ID));
        Assert.Empty(world.Tables.Texts.GetRows(Group("g1")));
    }

    [Theory]
    [InlineData("Season 1|Season 1|Specials|Staffel 5", "|Season 1||")]
    [InlineData("Act I|Season 3|Extras|Season 11", "Act I|Season 3|Extras|Season 11")]
    public void AGroupsGenericNameIsStoredOnlyWhenItCarriesAnotherNumber(string names, string stored)
    {
        using var world = new World();
        var given = names.Split('|');

        var ordering = world.Service.SaveOrdering(Global(
            world.SeriesID,
            new() { ID = Group("g1"), Titles = TestTexts.Named(given[0]), Episodes = [World.Episode(1)] },
            new() { ID = Group("g2"), Titles = TestTexts.Named(given[1]), Episodes = [World.Episode(2)] },
            new() { ID = Group("sp"), Titles = TestTexts.Named(given[2]), IsSpecial = true, Episodes = [World.Episode(3)] },
            new() { ID = Group("g5"), Titles = TestTexts.Named(given[3]), SeasonNumber = 5, Episodes = [World.Episode(4)] }
        ));

        Assert.Equal(
            stored.Split('|'),
            ordering.Seasons.Select(group => world.Tables.TextStore.GetTitles(group.ID).SingleOrDefault()?.Value ?? string.Empty)
        );
    }

    [Fact]
    public void AnUntitledOrderingAndItsGroupsReadWithSynthesizedNames()
    {
        using var world = new World();

        var global = world.Service.SaveOrdering(Global(
            world.SeriesID,
            new() { ID = Group("g1"), Episodes = [World.Episode(1)] },
            new() { ID = Group("sp"), IsSpecial = true, Episodes = [World.Episode(2)] }
        ));
        var local = world.Service.CreateLocalOrdering(new() { SeriesID = world.SeriesID, Groups = [new() { Episodes = [World.Episode(3)] }] });

        Assert.Equal(Synthesized(1), global.Seasons[0].Titles.Select(title => title.Value));
        Assert.Equal(Synthesized(0), global.Seasons[1].Titles.Select(title => title.Value));
        Assert.Equal(Synthesized(1)[0], local.Seasons[0].Title);
        foreach (var ordering in new[] { global, local })
        {
            Assert.Equal(GenericEpisodeTitles.SynthesizeName(ordering.ID).Value, ordering.Title);
            Assert.True(ordering.DefaultTitle.IsSynthesized);
            Assert.All(ordering.Seasons, group => Assert.True(group.DefaultTitle.IsSynthesized));
        }
    }
}
