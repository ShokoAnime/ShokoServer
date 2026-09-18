using System.Linq;
using Moq;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.Shoko.Embedded;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Models;

/// <summary>
/// Covers <see cref="IShokoSeason.TmdbSeasons"/> and <see cref="IShokoSeason.LinkedSeasons"/> on
/// <see cref="AnimeSeason"/>, which collect the TMDB seasons linked through the season's own
/// episodes. They read through the AniDB episode and TMDB repositories, so these run against real
/// repositories seeded from memory rather than a database.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class AnimeSeasonTmdbLinkTests
{
    private const int NormalAnidbEpisodeID = 100;
    private const int SpecialAnidbEpisodeID = 101;
    private const int NormalTmdbSeasonID = 50;
    private const int SpecialTmdbSeasonID = 60;

    private static RepoFactoryScope Scope()
        => new RepoFactoryScope()
            .With<AniDB_EpisodeRepository, int, AniDB_Episode>(e => e.AniDB_EpisodeID,
            [
                new() { AniDB_EpisodeID = NormalAnidbEpisodeID, EpisodeID = NormalAnidbEpisodeID, EpisodeType = EpisodeType.Episode },
                new() { AniDB_EpisodeID = SpecialAnidbEpisodeID, EpisodeID = SpecialAnidbEpisodeID, EpisodeType = EpisodeType.Special },
            ])
            .With<CrossRef_AniDB_TMDB_EpisodeRepository, int, CrossRef_AniDB_TMDB_Episode>(x => x.CrossRef_AniDB_TMDB_EpisodeID,
            [
                new() { CrossRef_AniDB_TMDB_EpisodeID = 1, AnidbEpisodeID = NormalAnidbEpisodeID, TmdbEpisodeID = 500 },
                new() { CrossRef_AniDB_TMDB_EpisodeID = 2, AnidbEpisodeID = SpecialAnidbEpisodeID, TmdbEpisodeID = 501 },
            ])
            .With<TMDB_EpisodeRepository, int, TMDB_Episode>(e => e.Id,
            [
                new() { TMDB_EpisodeID = 1, TmdbEpisodeID = 500, TmdbSeasonID = NormalTmdbSeasonID },
                new() { TMDB_EpisodeID = 2, TmdbEpisodeID = 501, TmdbSeasonID = SpecialTmdbSeasonID },
            ])
            .With<TMDB_SeasonRepository, int, TMDB_Season>(s => s.Id,
            [
                new() { TMDB_SeasonID = 1, TmdbSeasonID = NormalTmdbSeasonID },
                new() { TMDB_SeasonID = 2, TmdbSeasonID = SpecialTmdbSeasonID },
            ]);

    private static IShokoSeason Season(EpisodeType episodeType, int seasonNumber)
    {
        var series = new Mock<IShokoSeries>();
        series.Setup(s => s.Episodes).Returns(
        [
            new AnimeEpisode { AniDB_EpisodeID = NormalAnidbEpisodeID },
            new AnimeEpisode { AniDB_EpisodeID = SpecialAnidbEpisodeID },
        ]);
        return new AnimeSeason(series.Object, episodeType, seasonNumber);
    }

    [Fact]
    public void TmdbSeasons_ComeFromTheSeasonsOwnEpisodes()
    {
        using var scope = Scope();

        Assert.Equal([NormalTmdbSeasonID.ToString()], Season(EpisodeType.Episode, 1).TmdbSeasons.Select(s => s.ID));
        Assert.Equal([SpecialTmdbSeasonID.ToString()], Season(EpisodeType.Special, 0).TmdbSeasons.Select(s => s.ID));
    }

    [Fact]
    public void LinkedSeasons_ComeFromTheSeasonsOwnEpisodes()
    {
        using var scope = Scope();

        Assert.Equal([NormalTmdbSeasonID.ToString()], Season(EpisodeType.Episode, 1).LinkedSeasons.Select(s => s.ID));
        Assert.Equal([SpecialTmdbSeasonID.ToString()], Season(EpisodeType.Special, 0).LinkedSeasons.Select(s => s.ID));
    }
}
