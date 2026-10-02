using Shoko.Abstractions.Metadata;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Providers.TMDB;

/// <summary>
/// Covers the TMDB seasons and episodes left behind when their show's row is
/// gone: they are never handed out, so reading the links to them skips them
/// rather than failing on their missing show.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class TmdbOrphanedRowTests
{
    [Fact]
    public void AnEpisodeWhoseShowIsGoneIsSkippedWhenReadingItsLinks()
    {
        var links = CachedRepo.Build<CrossRef_AniDB_Metadata_EpisodeRepository, int, CrossRef_AniDB_Metadata_Episode>(
            link => link.CrossRef_AniDB_Metadata_EpisodeID,
            Link(1, "501", "5", 0),
            Link(2, "801", "8", 1)
        );
        var store = new MetadataCrossReferenceStore(
            CachedRepo.Build<CrossRef_AniDB_Metadata_SeriesRepository, int, CrossRef_AniDB_Metadata_Series>(link => link.CrossRef_AniDB_Metadata_SeriesID),
            CachedRepo.Build<CrossRef_AniDB_Metadata_MovieRepository, int, CrossRef_AniDB_Metadata_Movie>(link => link.CrossRef_AniDB_Metadata_MovieID),
            links,
            CachedRepo.Build<Metadata_EpisodeRepository, int, Metadata_Episode>(row => row.Metadata_EpisodeID)
        );
        using var scope = new RepoFactoryScope()
            .With<TMDB_ShowRepository, int, TMDB_Show>(show => show.TmdbShowID, [new TMDB_Show(5)])
            .With<TMDB_SeasonRepository, int, TMDB_Season>(season => season.TMDB_SeasonID, [
                new TMDB_Season(51) { TMDB_SeasonID = 1, TmdbShowID = 5, SeasonNumber = 1 },
                new TMDB_Season(81) { TMDB_SeasonID = 2, TmdbShowID = 8, SeasonNumber = 1 },
            ])
            .With<TMDB_EpisodeRepository, int, TMDB_Episode>(episode => episode.TMDB_EpisodeID, [
                new TMDB_Episode(501) { TMDB_EpisodeID = 1, TmdbShowID = 5, TmdbSeasonID = 51, SeasonNumber = 1, EpisodeNumber = 1 },
                new TMDB_Episode(801) { TMDB_EpisodeID = 2, TmdbShowID = 8, TmdbSeasonID = 81, SeasonNumber = 1, EpisodeNumber = 1 },
            ])
            .Set(new CrossRef_AniDB_TMDB_EpisodeRepository(links, store));

        var episode = Assert.Single(new AniDB_Episode { EpisodeID = 101, AnimeID = 10 }.TmdbEpisodes);

        Assert.Equal(501, episode.TmdbEpisodeID);
        Assert.Equal(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "5"), ((IEpisode)episode).Series.ID);
        Assert.Null(RepoFactory.TMDB_Season.GetByTmdbSeasonID(81));
    }

    private static CrossRef_AniDB_Metadata_Episode Link(int rowID, string episodeID, string showID, int ordering)
        => new()
        {
            CrossRef_AniDB_Metadata_EpisodeID = rowID,
            Source = MetadataSource.TMDB,
            AnidbAnimeID = 10,
            AnidbEpisodeID = 101,
            ProviderID = episodeID,
            ProviderParentID = showID,
            Ordering = ordering,
        };
}
