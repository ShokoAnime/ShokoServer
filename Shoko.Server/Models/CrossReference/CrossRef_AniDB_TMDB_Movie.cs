using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
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

    /// <summary>
    /// Get all images for the movie, or all images for the given
    /// <paramref name="entityType"/> provided for the movie.
    /// </summary>
    /// <param name="entityType">If set, will restrict the returned list to only
    /// containing the images of the given entity type.</param>
    /// <returns>A read-only list of images that are linked to the movie.
    /// </returns>
    public IReadOnlyList<TMDB_Image> GetImages(ImageEntityType? entityType = null) => entityType.HasValue
        ? RepoFactory.TMDB_Image.GetByTmdbMovieIDAndType(TmdbMovieID, entityType.Value)
        : RepoFactory.TMDB_Image.GetByTmdbMovieID(TmdbMovieID);

    /// <summary>
    /// Get all images for the movie, or all images for the given
    /// <paramref name="entityType"/> provided for the movie.
    /// </summary>
    /// <param name="entityType">If set, will restrict the returned list to only
    /// containing the images of the given entity type.</param>
    /// <param name="preferredImages">The preferred images.</param>
    /// <returns>A read-only list of images that are linked to the movie.
    /// </returns>
    public IReadOnlyList<IImageMetadata> GetImages(ImageEntityType? entityType, IReadOnlyDictionary<ImageEntityType, IImageMetadata> preferredImages) =>
        GetImages(entityType)
            .GroupBy(i => i.ImageType)
            .SelectMany(gB => preferredImages.TryGetValue(gB.Key, out var pI) ? gB.Select(i => i.Equals(pI) ? pI : i) : gB)
            .ToList();

    #endregion
}
