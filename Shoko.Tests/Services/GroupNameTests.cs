using System;
using System.Collections.Generic;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers a group's name: the one a user gave it, else its main series' title, and that reading it
/// throws for a group with no series and no name of its own. Also that its sort name and
/// plugin-facing titles follow.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class GroupNameTests
{
    #region World

    /// <summary>
    /// Groups, series and anime put in place for the models to read, with their titles in one
    /// cache-only store.
    /// </summary>
    private sealed class World : IDisposable
    {
        private readonly RepoFactoryScope _scope;

        public MetadataTextStore Store { get; } = new(new TextCache(), new CacheOnlyRowWriter());

        public MetadataTextManager Manager { get; }

        public World(IReadOnlyList<AnimeGroup> groups, IReadOnlyList<AnimeSeries> series, IReadOnlyList<AniDB_Anime> anime)
        {
            var service = new Mock<IMetadataService>();
            service.Setup(s => s.GetSeriesCrossReferences(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
            service.Setup(s => s.GetEpisodeCrossReferences(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
            service.Setup(s => s.GetMovieCrossReferences(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
            service.Setup(s => s.GetMovieCrossReferencesForSeries(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
            service.Setup(s => s.GetSeasonCrossReferences(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
            service.Setup(s => s.GetEpisodeCrossReferencesForSeries(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
            service.Setup(s => s.GetCollectionsWith(It.IsAny<MetadataGuid>())).Returns([]);
            Manager = TestTextManager.Build(Store, service.Object);

            _scope = new RepoFactoryScope()
                .Set(Manager)
                .With<AnimeGroupRepository, int, AnimeGroup>(group => group.AnimeGroupID, groups)
                .With<AnimeSeriesRepository, int, AnimeSeries>(entry => entry.AnimeSeriesID, series)
                .With<AniDB_AnimeRepository, int, AniDB_Anime>(entry => entry.AniDB_AnimeID, anime)
                .With<AniDB_EpisodeRepository, int, AniDB_Episode>(episode => episode.AniDB_EpisodeID)
                .With<AnimeEpisodeRepository, int, AnimeEpisode>(episode => episode.AnimeEpisodeID);

            foreach (var entry in anime)
                SetAnimeTitle(entry.AnimeID, entry.MainTitle);
        }

        public void SetAnimeTitle(int animeID, string value)
            => Store.SetTitles(
                new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, animeID.ToString()),
                MetadataSource.AniDB,
                [new TitleStub { Source = MetadataSource.AniDB, Value = value, Language = TitleLanguage.Romaji, LanguageCode = "x-jat", Type = TitleType.Main }]
            );

        public void Dispose()
            => _scope.Dispose();
    }

    private static AniDB_Anime Anime(int id, string title, int? year = null)
        => new() { AniDB_AnimeID = id, AnimeID = id, MainTitle = title, AirDate = year is { } value ? new PartialDateOnly(value, 1, 1) : null };

    #endregion

    [Fact]
    public void AGroupIsNamedAfterItsMainSeriesUntilAUserNamesIt()
    {
        var group = new AnimeGroup { AnimeGroupID = 1 };
        using var world = new World([group], [new AnimeSeries { AnimeSeriesID = 2, AniDB_ID = 3, AnimeGroupID = 1 }], [Anime(3, "Alpha")]);
        Assert.Equal("Alpha", group.GroupName);

        Assert.True(world.Manager.SetCustomTitle(((IMetadata)group).ID, "Named"));
        Assert.Equal("Named", group.GroupName);

        Assert.True(world.Manager.SetCustomTitle(((IMetadata)group).ID, "   "));
        Assert.Equal("Alpha", group.GroupName);
    }

    [Fact]
    public void TheMainSeriesOfSeriesThatAiredTogetherIsTheOneWithTheLowestID()
    {
        var group = new AnimeGroup { AnimeGroupID = 1 };
        using var world = new World(
            [group],
            [
                new AnimeSeries { AnimeSeriesID = 8, AniDB_ID = 80, AnimeGroupID = 1 },
                new AnimeSeries { AnimeSeriesID = 4, AniDB_ID = 40, AnimeGroupID = 1 },
            ],
            [Anime(80, "Later ID", 2015), Anime(40, "Earlier ID", 2015)]
        );

        Assert.Equal(4, group.MainSeries?.AnimeSeriesID);
        Assert.Equal("Earlier ID", group.GroupName);
    }

    [Fact]
    public void AGroupWithNoSeriesAndNoNameOfItsOwnThrows()
    {
        var group = new AnimeGroup { AnimeGroupID = 9 };
        var child = new AnimeGroup { AnimeGroupID = 10, AnimeGroupParentID = 9 };
        using var world = new World([group, child], [], []);
        world.Manager.SetCustomTitle(((IMetadata)child).ID, "Named Child");

        Assert.Throws<InvalidOperationException>(() => group.GroupName);
        Assert.Throws<InvalidOperationException>(() => ((IWithTitles)group).DefaultTitle);
        Assert.Throws<InvalidOperationException>(() => ((IWithTitles)group).PreferredTitle);
        Assert.Equal("Named Child", child.GroupName);
    }

    [Fact]
    public void AGroupWithNoSeriesKeepsTheNameAUserGaveIt()
    {
        var group = new AnimeGroup { AnimeGroupID = 9 };
        using var world = new World([group], [], []);
        world.Manager.SetCustomTitle(((IMetadata)group).ID, "Named");

        Assert.Equal("Named", group.GroupName);
        Assert.Equal("Named", ((IWithTitles)group).DefaultTitle.Value);
    }

    [Fact]
    public void TheSortNameFollowsTheName()
    {
        var group = new AnimeGroup { AnimeGroupID = 1 };
        using var world = new World([group], [new AnimeSeries { AnimeSeriesID = 2, AniDB_ID = 3, AnimeGroupID = 1 }], [Anime(3, "The Alpha")]);
        Assert.Equal("ALPHA", group.SortName);

        world.Manager.SetCustomTitle(((IMetadata)group).ID, "1st Group");
        Assert.Equal("#1ST GROUP", group.SortName);
        Assert.Equal("1st Group", ((IWithTitles)group).Title);
    }
}
