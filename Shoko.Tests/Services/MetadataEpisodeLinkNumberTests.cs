using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the season and numbers an episode link records, and the season
/// links worked out from them. A TMDB link reads what it does not record from
/// TMDB's own tables through <c>RepoFactory</c>, so these tests share its
/// collection.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class MetadataEpisodeLinkNumberTests
{
    #region Helpers

    private static CrossRef_AniDB_Metadata_Episode Link(
        int rowID,
        MetadataSource source,
        int anidbEpisodeID,
        string providerID,
        string? seasonID = null,
        int? seasonNumber = null,
        string parentID = "show"
    )
        => new()
        {
            CrossRef_AniDB_Metadata_EpisodeID = rowID,
            Source = source,
            AnidbAnimeID = 10,
            AnidbEpisodeID = anidbEpisodeID,
            ProviderID = providerID,
            ProviderParentID = parentID,
            ProviderSeasonID = seasonID,
            SeasonNumber = seasonNumber,
            EpisodeNumber = seasonNumber is null ? null : anidbEpisodeID % 100,
        };

    private static MetadataCrossReferenceStore Store(params CrossRef_AniDB_Metadata_Episode[] links)
        => Store([], links);

    private static MetadataCrossReferenceStore Store(Metadata_Episode[] stored, params CrossRef_AniDB_Metadata_Episode[] links)
        => new(
            CachedRepo.Build<CrossRef_AniDB_Metadata_SeriesRepository, int, CrossRef_AniDB_Metadata_Series>(x => x.CrossRef_AniDB_Metadata_SeriesID),
            CachedRepo.Build<CrossRef_AniDB_Metadata_MovieRepository, int, CrossRef_AniDB_Metadata_Movie>(x => x.CrossRef_AniDB_Metadata_MovieID),
            CachedRepo.Build<CrossRef_AniDB_Metadata_EpisodeRepository, int, CrossRef_AniDB_Metadata_Episode>(x => x.CrossRef_AniDB_Metadata_EpisodeID, links),
            CachedRepo.Build<Metadata_EpisodeRepository, int, Metadata_Episode>(row => row.Metadata_EpisodeID, stored)
        );

    private static RepoFactoryScope TmdbEpisodes(params TMDB_Episode[] episodes)
    {
        var shows = episodes.Select(episode => episode.TmdbShowID).Distinct().Select(showID => new TMDB_Show(showID));
        return new RepoFactoryScope()
            .With<TMDB_ShowRepository, int, TMDB_Show>(show => show.TmdbShowID, shows)
            .With<TMDB_EpisodeRepository, int, TMDB_Episode>(episode => episode.TMDB_EpisodeID, episodes);
    }

    #endregion

    #region Season Links

    [Fact]
    public void SeasonLinksAreReadOffTheSeasonEachEpisodeLinkRecords()
    {
        using var scope = TmdbEpisodes();
        var store = Store(
            Link(1, TestSources.AniList, 101, "e1", "s2", 2),
            Link(2, TestSources.AniList, 102, "e2", "s2", 2),
            Link(3, TestSources.AniList, 103, "e3", "s1", 1)
        );

        var seasons = store.GetAllSeasonLinks(TestSources.AniList);

        Assert.Equal(["s1", "s2"], seasons.Select(season => season.ProviderID!.ID));
        Assert.Equal([1, 2], seasons.Select(season => season.SeasonNumber));
        Assert.Equal([0, 1], seasons.Select(season => season.Ordering));
        Assert.All(seasons, season => Assert.Equal(new MetadataGuid(TestSources.AniList, MetadataEntityType.Series, "show"), season.ProviderParentID));
        Assert.All(seasons, season => Assert.Equal(MetadataEntityType.Season, season.ProviderID!.EntityType));
    }

    [Fact]
    public void AnEpisodeLinkWithNoSeasonAddsNone()
    {
        using var scope = TmdbEpisodes();
        var store = Store(
            Link(1, TestSources.AniList, 101, "e1"),
            Link(2, TestSources.AniList, 102, "e2", "s1", seasonNumber: null),
            Link(3, TestSources.AniList, 103, "e3", "s3", 3, parentID: string.Empty)
        );

        Assert.Empty(store.GetAllSeasonLinks());
    }

    [Fact]
    public void ALinkNamingOnlyItsSeasonReadsTheRestFromTheSeriesStore()
    {
        using var scope = TmdbEpisodes();
        var stored = new Metadata_Episode
        {
            Metadata_EpisodeID = 1,
            Source = TestSources.AniList,
            ProviderID = "stored-e1",
            SeriesID = "stored-show",
            SeasonID = "s4",
            SeasonNumber = 4,
            EpisodeNumber = 1,
        };
        var store = Store([stored], Link(1, TestSources.AniList, 101, "e1", "s4", seasonNumber: null, parentID: string.Empty));

        var season = Assert.Single(store.GetAllSeasonLinks(TestSources.AniList));
        Assert.Equal(new MetadataGuid(TestSources.AniList, MetadataEntityType.Season, "s4"), season.ProviderID);
        Assert.Equal(4, season.SeasonNumber);
        Assert.Equal(new MetadataGuid(TestSources.AniList, MetadataEntityType.Series, "stored-show"), season.ProviderParentID);
    }

    #endregion

    #region TMDB

    [Fact]
    public void ATmdbLinkReadsWhatItDoesNotRecordFromTheTmdbEpisode()
    {
        using var scope = TmdbEpisodes(new TMDB_Episode
        {
            TMDB_EpisodeID = 1,
            TmdbEpisodeID = 500,
            TmdbSeasonID = 50,
            TmdbShowID = 5,
            SeasonNumber = 3,
            EpisodeNumber = 9,
        });
        IMetadataEpisodeCrossReference link = Link(1, MetadataSource.TMDB, 101, "500", parentID: "5");

        Assert.Equal(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Season, "50"), link.SeasonID);
        Assert.Equal(3, link.SeasonNumber);
        Assert.Equal(9, link.EpisodeNumber);
        Assert.Equal([3], Store((CrossRef_AniDB_Metadata_Episode)link).GetAllSeasonLinks(MetadataSource.TMDB).Select(season => season.SeasonNumber));
    }

    [Fact]
    public void WhatALinkRecordsIsReadAheadOfTheTmdbEpisode()
    {
        using var scope = TmdbEpisodes(new TMDB_Episode { TMDB_EpisodeID = 1, TmdbEpisodeID = 500, TmdbSeasonID = 50, SeasonNumber = 3, EpisodeNumber = 9 });
        IMetadataEpisodeCrossReference link = Link(1, MetadataSource.TMDB, 104, "500", "60", 4);

        Assert.Equal(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Season, "60"), link.SeasonID);
        Assert.Equal(4, link.SeasonNumber);
        Assert.Equal(4, link.EpisodeNumber);
    }

    [Fact]
    public void ALinkOfAnotherSourceNeverReadsTheTmdbTables()
    {
        using var scope = TmdbEpisodes(new TMDB_Episode { TMDB_EpisodeID = 1, TmdbEpisodeID = 500, TmdbSeasonID = 50, SeasonNumber = 3 });
        IMetadataEpisodeCrossReference link = Link(1, TestSources.AniList, 101, "500");

        Assert.Null(link.SeasonID);
        Assert.Null(link.SeasonNumber);
        Assert.Null(link.EpisodeNumber);
    }

    #endregion
}
