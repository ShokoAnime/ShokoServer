using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.CrossReference;

public class CrossRef_AniDB_TMDB_Episode
{
    #region Database Columns

    public int CrossRef_AniDB_TMDB_EpisodeID { get; set; }

    public int AnidbAnimeID { get; set; }

    public int AnidbEpisodeID { get; set; }

    public int TmdbShowID { get; set; }

    public int TmdbEpisodeID { get; set; }

    public int Ordering { get; set; }

    public MatchRating MatchRating { get; set; }

    #endregion
    #region Constructors

    public CrossRef_AniDB_TMDB_Episode() { }

    public CrossRef_AniDB_TMDB_Episode(int anidbEpisodeId, int anidbAnimeId, int tmdbEpisodeId, int tmdbShowId, MatchRating rating = MatchRating.UserVerified, int ordering = 0)
    {
        AnidbEpisodeID = anidbEpisodeId;
        AnidbAnimeID = anidbAnimeId;
        TmdbEpisodeID = tmdbEpisodeId;
        TmdbShowID = tmdbShowId;
        Ordering = ordering;
        MatchRating = rating;
    }

    #endregion
    #region Methods

    public SVR_AniDB_Episode? AnidbEpisode =>
        RepoFactory.AniDB_Episode.GetByEpisodeID(AnidbEpisodeID);

    public SVR_AniDB_Anime? AnidbAnime =>
        RepoFactory.AniDB_Anime.GetByAnimeID(AnidbAnimeID);

    public SVR_AnimeEpisode? AnimeEpisode =>
        RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(AnidbEpisodeID);

    public SVR_AnimeSeries? AnimeSeries =>
        RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    public TMDB_Episode? TmdbEpisode =>
        TmdbEpisodeID == 0 ? null : RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(TmdbEpisodeID);

    public TMDB_Season? TmdbSeason =>
        TmdbEpisode?.TmdbSeason;

    public TMDB_Show? GetTmdbShow() =>
        TmdbShowID == 0 ? null : RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID);

    #endregion
}
