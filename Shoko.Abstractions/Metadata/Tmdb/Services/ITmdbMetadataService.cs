using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Metadata.Tmdb.Services;

/// <summary>
/// TMDB metadata service for managing movie and show metadata.
/// </summary>
public interface ITmdbMetadataService
{
    #region Genres

    /// <summary>
    /// Gets the available movie genres from TMDB.
    /// </summary>
    /// <returns>A dictionary mapping genre IDs to genre names.</returns>
    Task<IReadOnlyDictionary<int, string>> GetMovieGenres();

    /// <summary>
    /// Gets the available TV show genres from TMDB.
    /// </summary>
    /// <returns>A dictionary mapping genre IDs to genre names.</returns>
    Task<IReadOnlyDictionary<int, string>> GetShowGenres();

    #endregion

    #region Movies

    /// <summary>
    /// Updates all movies that are currently linked to AniDB entries.
    /// </summary>
    /// <param name="force">Force refresh even if recently updated.</param>
    /// <param name="saveImages">Whether to download images.</param>
    Task UpdateAllMovies(bool force, bool saveImages);

    /// <summary>
    /// Schedules a movie update job.
    /// </summary>
    /// <param name="movieId">The TMDB movie ID.</param>
    /// <param name="forceRefresh">Force refresh even if recently updated.</param>
    /// <param name="downloadImages">Whether to download images.</param>
    /// <param name="downloadCrewAndCast">Whether to download crew and cast. If null, uses settings default.</param>
    /// <param name="downloadCollections">Whether to download collection info. If null, uses settings default.</param>
    Task ScheduleUpdateOfMovie(int movieId, bool forceRefresh = false, bool downloadImages = false, bool? downloadCrewAndCast = null, bool? downloadCollections = null);

    /// <summary>
    /// Updates a TMDB movie's metadata.
    /// </summary>
    /// <param name="movieId">The TMDB movie ID.</param>
    /// <param name="forceRefresh">Force refresh even if recently updated.</param>
    /// <param name="downloadImages">Whether to download images.</param>
    /// <param name="downloadCrewAndCast">Whether to download crew and cast.</param>
    /// <param name="downloadCollections">Whether to download collection info.</param>
    /// <returns>True if the movie was updated, false otherwise.</returns>
    Task<bool> UpdateMovie(int movieId, bool forceRefresh = false, bool downloadImages = false, bool downloadCrewAndCast = false, bool downloadCollections = false);

    /// <summary>
    /// Schedules a job to download all images for a movie.
    /// </summary>
    /// <param name="movieId">The TMDB movie ID.</param>
    /// <param name="forceDownload">Force download even if images already exist.</param>
    Task ScheduleDownloadAllMovieImages(int movieId, bool forceDownload = false);

    /// <summary>
    /// Schedules all unused movies to be purged.
    /// </summary>
    Task PurgeAllUnusedMovies();

    /// <summary>
    /// Schedules a movie purge job.
    /// </summary>
    /// <param name="movieId">The TMDB movie ID.</param>
    Task SchedulePurgeOfMovie(int movieId);

    /// <summary>
    /// Purges a TMDB movie from the local database.
    /// </summary>
    /// <param name="movieId">The TMDB movie ID.</param>
    Task PurgeMovie(int movieId);

    /// <summary>
    /// Purges all movie collections from the local database.
    /// </summary>
    Task PurgeAllMovieCollections();

    #endregion

    #region Shows

    /// <summary>
    /// Updates all shows that are currently linked to AniDB entries.
    /// </summary>
    /// <param name="force">Force refresh even if recently updated.</param>
    /// <param name="downloadImages">Whether to download images.</param>
    Task UpdateAllShows(bool force = false, bool downloadImages = false);

    /// <summary>
    /// Schedules a show update job.
    /// </summary>
    /// <param name="showId">The TMDB show ID.</param>
    /// <param name="forceRefresh">Force refresh even if recently updated.</param>
    /// <param name="downloadImages">Whether to download images.</param>
    /// <param name="downloadCrewAndCast">Whether to download crew and cast. If null, uses settings default.</param>
    /// <param name="downloadAlternateOrdering">Whether to download alternate ordering. If null, uses settings default.</param>
    /// <param name="downloadNetworks">Whether to download network info. If null, uses settings default.</param>
    Task ScheduleUpdateOfShow(int showId, bool forceRefresh = false, bool downloadImages = false, bool? downloadCrewAndCast = null, bool? downloadAlternateOrdering = null, bool? downloadNetworks = null);

    /// <summary>
    /// Updates a TMDB show's metadata.
    /// </summary>
    /// <param name="showId">The TMDB show ID.</param>
    /// <param name="forceRefresh">Force refresh even if recently updated.</param>
    /// <param name="downloadImages">Whether to download images.</param>
    /// <param name="downloadCrewAndCast">Whether to download crew and cast.</param>
    /// <param name="downloadAlternateOrdering">Whether to download alternate ordering.</param>
    /// <param name="downloadNetworks">Whether to download network info.</param>
    /// <param name="quickRefresh">Whether to perform a quick refresh (skip some operations).</param>
    /// <returns>True if the show was updated, false otherwise.</returns>
    Task<bool> UpdateShow(int showId, bool forceRefresh = false, bool downloadImages = false, bool downloadCrewAndCast = false, bool downloadAlternateOrdering = false, bool downloadNetworks = false, bool quickRefresh = false);

    /// <summary>
    /// Schedules a job to download all images for a show.
    /// </summary>
    /// <param name="showId">The TMDB show ID.</param>
    /// <param name="forceDownload">Force download even if images already exist.</param>
    Task ScheduleDownloadAllShowImages(int showId, bool forceDownload = false);

    /// <summary>
    /// Schedules all unused shows to be purged.
    /// </summary>
    Task PurgeAllUnusedShows();

    /// <summary>
    /// Schedules a show purge job.
    /// </summary>
    /// <param name="showId">The TMDB show ID.</param>
    Task SchedulePurgeOfShow(int showId);

    /// <summary>
    /// Purges a TMDB show from the local database.
    /// </summary>
    /// <param name="showId">The TMDB show ID.</param>
    Task PurgeShow(int showId);

    #endregion

    #region Search/Matching

    /// <summary>
    /// Schedules a search job for auto-matching an AniDB anime to TMDB.
    /// </summary>
    /// <param name="anidbId">The AniDB anime ID.</param>
    /// <param name="force">Force search even if already linked.</param>
    Task ScheduleSearchForMatch(int anidbId, bool force);

    /// <summary>
    /// Scans all series for missing TMDB matches and schedules search jobs.
    /// </summary>
    Task ScanForMatches();

    #endregion
}
