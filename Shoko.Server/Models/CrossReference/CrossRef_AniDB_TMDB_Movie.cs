using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Metadata.Tmdb.CrossReferences;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;

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
    public IReadOnlyList<IImage> GetImages(ImageEntityType? entityType, IReadOnlyDictionary<ImageEntityType, IImage> preferredImages) =>
        GetImages(entityType)
            .GroupBy(i => i.ImageType)
            .SelectMany(gB => preferredImages.TryGetValue(gB.Key, out var pI) ? gB.Select(i => i.Equals(pI) ? pI : i) : gB)
            .ToList();

    #endregion

    #region IWithImages Implementation

    IImage? IWithImages.GetPreferredImageForType(ImageEntityType entityType)
        => null;

    IReadOnlyList<IImage> IWithImages.GetImages(ImageEntityType? entityType)
        => GetImages(entityType);

    #endregion

    #region ITmdbMovieCrossReference Implementation

    IShokoSeries? ITmdbMovieCrossReference.ShokoSeries => AnimeSeries;

    IShokoEpisode? ITmdbMovieCrossReference.ShokoEpisode => AnimeEpisode;

    ITmdbMovie? ITmdbMovieCrossReference.TmdbMovie => TmdbMovie;

    #endregion
}
