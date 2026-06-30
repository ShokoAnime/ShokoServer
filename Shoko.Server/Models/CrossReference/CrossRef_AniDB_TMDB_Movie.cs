using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Metadata.Tmdb.CrossReferences;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;

#pragma warning disable CS0618
#nullable enable
namespace Shoko.Server.Models.CrossReference;

public class CrossRef_AniDB_TMDB_Movie : ITmdbMovieCrossReference
{
    #region Database Columns

    public int CrossRef_AniDB_TMDB_MovieID { get; set; }

    public int AnidbAnimeID { get; set; }

    public int AnidbEpisodeID { get; set; }

    public int TmdbMovieID { get; set; }

    public MatchRating MatchRating { get; set; }

    #endregion
    #region Constructors

    public CrossRef_AniDB_TMDB_Movie() { }

    public CrossRef_AniDB_TMDB_Movie(int anidbEpisodeId, int anidbAnimeId, int tmdbMovieId, MatchRating matchRating = MatchRating.UserVerified)
    {
        AnidbEpisodeID = anidbEpisodeId;
        AnidbAnimeID = anidbAnimeId;
        TmdbMovieID = tmdbMovieId;
        MatchRating = matchRating;
    }

    #endregion

    #region Methods

    public AniDB_Episode? AnidbEpisode => RepoFactory.AniDB_Episode.GetByEpisodeID(AnidbEpisodeID);

    public AniDB_Anime? AnidbAnime =>
        RepoFactory.AniDB_Anime.GetByAnimeID(AnidbAnimeID);

    public AnimeEpisode? AnimeEpisode => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(AnidbEpisodeID);

    public AnimeSeries? AnimeSeries =>
        RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    public TMDB_Movie? TmdbMovie
        => RepoFactory.TMDB_Movie.GetByTmdbMovieID(TmdbMovieID);

    #endregion

    #region IMetadata Implementation

    DataSource IMetadata.Source => DataSource.TMDB;

    #endregion

    #region ITmdbMovieCrossReference Implementation

    IShokoSeries? ITmdbMovieCrossReference.ShokoSeries => AnimeSeries;

    IShokoEpisode? ITmdbMovieCrossReference.ShokoEpisode => AnimeEpisode;

    ITmdbMovie? ITmdbMovieCrossReference.TmdbMovie => TmdbMovie;

    #endregion
}
