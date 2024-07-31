using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.CrossReference;

public class CrossRef_AniDB_TMDB_Movie
{
    #region Database Columns

    public int CrossRef_AniDB_TMDB_MovieID { get; set; }

    public int AnidbAnimeID { get; set; }

    public int? AnidbEpisodeID { get; set; }

    public int TmdbMovieID { get; set; }

    public CrossRefSource Source { get; set; }

    #endregion
    #region Constructors

    public CrossRef_AniDB_TMDB_Movie() { }

    public CrossRef_AniDB_TMDB_Movie(int anidbAnimeId, int tmdbMovieId, MatchRating rating = MatchRating.UserVerified, CrossRefSource source = CrossRefSource.User)
    {
        AnidbAnimeID = anidbAnimeId;
        TmdbMovieID = tmdbMovieId;
        Source = source;
    }

    #endregion

    #region Methods

    public SVR_AniDB_Episode? AnidbEpisode => AnidbEpisodeID.HasValue
        ? RepoFactory.AniDB_Episode.GetByEpisodeID(AnidbEpisodeID.Value)
        : null;

    public SVR_AniDB_Anime? AnidbAnime =>
        RepoFactory.AniDB_Anime.GetByAnimeID(AnidbAnimeID);

    public SVR_AnimeEpisode? AnimeEpisode => AnidbEpisodeID.HasValue
        ? RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(AnidbEpisodeID.Value)
        : null;

    public SVR_AnimeSeries? AnimeSeries =>
        RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    public TMDB_Movie? TmdbMovie
        => RepoFactory.TMDB_Movie.GetByTmdbMovieID(TmdbMovieID);

    #endregion
}
