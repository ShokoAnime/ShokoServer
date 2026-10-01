using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Abstractions.Metadata.Tmdb.Services;

/// <summary>
/// TMDB metadata service for managing movie and show metadata.
/// </summary>
/// <remarks>
/// A shim over <see cref="IMetadataRefreshService"/> and
/// <see cref="IMetadataPurgeService"/> for the <c>tmdb</c> source, kept for
/// callers that name TMDB. Only the genre lists are TMDB's own.
/// </remarks>
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
    /// Queues a refresh of every movie linked to an anime in the library.
    /// </summary>
    /// <param name="force">Force refresh even if recently updated.</param>
    /// <param name="saveImages">Whether to download images.</param>
    /// <returns>A task that completes once the refreshes are queued.</returns>
    Task UpdateAllMovies(bool force, bool saveImages);

    /// <summary>
    /// Queues a refresh of a movie, fetched whether or not it is linked.
    /// </summary>
    /// <param name="options">The update options.</param>
    /// <returns>A task that completes once the refresh is queued.</returns>
    Task ScheduleUpdateOfMovie(TmdbMovieUpdateOptions options);

    /// <summary>
    /// Runs a refresh of a movie at once and waits for it, fetching it whether
    /// or not it is linked.
    /// </summary>
    /// <param name="options">The update options.</param>
    /// <returns>
    /// True if the refresh ran, even when it found the movie fresh or found
    /// nothing new; false while TMDB is paused or the queue holds its jobs
    /// back, so nothing ran.
    /// </returns>
    Task<bool> UpdateMovie(TmdbMovieUpdateOptions options);

    /// <summary>
    /// Schedules a job to download all images for a movie.
    /// </summary>
    /// <param name="movieId">The TMDB movie ID.</param>
    /// <param name="forceDownload">Force download even if images already exist.</param>
    Task ScheduleDownloadAllMovieImages(int movieId, bool forceDownload = false);

    /// <summary>
    /// Schedules all unused movies to be purged.
    /// </summary>
    /// <param name="olderThan">When set, only purge movies whose <c>LastUpdatedAt</c> is older than this value.</param>
    Task PurgeAllUnusedMovies(DateTime? olderThan = null);

    /// <summary>
    /// Queues a forced purge of a movie, which first removes every link naming
    /// it.
    /// </summary>
    /// <param name="movieId">The TMDB movie ID.</param>
    /// <returns>A task that completes once the purge is queued.</returns>
    Task SchedulePurgeOfMovie(int movieId);

    /// <summary>
    /// Queues a forced purge of a movie, which first removes every link naming
    /// it, the same as <see cref="SchedulePurgeOfMovie"/>.
    /// </summary>
    /// <remarks>
    /// The movie is still stored when the returned task completes; wait with
    /// <see cref="IMetadataRefreshService.WaitForRefresh"/> once the purge
    /// has started to read it back gone.
    /// </remarks>
    /// <param name="movieId">The TMDB movie ID.</param>
    /// <returns>A task that completes once the purge is queued.</returns>
    Task PurgeMovie(int movieId);

    /// <summary>
    /// Queues a forced purge of every stored movie collection.
    /// </summary>
    /// <returns>A task that completes once the purges are queued.</returns>
    Task PurgeAllMovieCollections();

    #endregion

    #region Shows

    /// <summary>
    /// Queues a refresh of every show linked to an anime in the library.
    /// </summary>
    /// <param name="force">Force refresh even if recently updated.</param>
    /// <param name="downloadImages">Whether to download images.</param>
    /// <returns>A task that completes once the refreshes are queued.</returns>
    Task UpdateAllShows(bool force = false, bool downloadImages = false);

    /// <summary>
    /// Queues a refresh of a show, fetched whether or not it is linked.
    /// </summary>
    /// <param name="options">The update options.</param>
    /// <returns>A task that completes once the refresh is queued.</returns>
    Task ScheduleUpdateOfShow(TmdbShowUpdateOptions options);

    /// <summary>
    /// Runs a refresh of a show at once and waits for it, fetching it whether
    /// or not it is linked.
    /// </summary>
    /// <param name="options">The update options.</param>
    /// <returns>
    /// True if the refresh ran, even when it found the show fresh or found
    /// nothing new; false for a show ID below 1, or while TMDB is paused or
    /// the queue holds its jobs back, so nothing ran.
    /// </returns>
    Task<bool> UpdateShow(TmdbShowUpdateOptions options);

    /// <summary>
    /// Schedules a job to download all images for a show.
    /// </summary>
    /// <param name="showId">The TMDB show ID.</param>
    /// <param name="forceDownload">Force download even if images already exist.</param>
    Task ScheduleDownloadAllShowImages(int showId, bool forceDownload = false);

    /// <summary>
    /// Schedules all unused shows to be purged.
    /// </summary>
    /// <param name="olderThan">When set, only purge shows whose <c>LastUpdatedAt</c> is older than this value.</param>
    Task PurgeAllUnusedShows(DateTime? olderThan = null);

    /// <summary>
    /// Queues a forced purge of a show, which first removes every link naming
    /// it and every episode link pointing into it.
    /// </summary>
    /// <param name="showId">The TMDB show ID.</param>
    /// <returns>A task that completes once the purge is queued.</returns>
    Task SchedulePurgeOfShow(int showId);

    /// <summary>
    /// Queues a forced purge of a show, which first removes every link naming
    /// it and every episode link pointing into it, the same as
    /// <see cref="SchedulePurgeOfShow"/>.
    /// </summary>
    /// <remarks>
    /// The show is still stored when the returned task completes; wait with
    /// <see cref="IMetadataRefreshService.WaitForRefresh"/> once the purge
    /// has started to read it back gone.
    /// </remarks>
    /// <param name="showId">The TMDB show ID.</param>
    /// <returns>A task that completes once the purge is queued.</returns>
    Task PurgeShow(int showId);

    #endregion

    #region Search/Matching

    /// <summary>
    /// Schedules a search job for auto-matching an AniDB anime to TMDB.
    /// </summary>
    /// <param name="anidbId">The AniDB anime ID.</param>
    /// <param name="force">
    ///   Search even when the anime is already linked, left alone or TMDB
    ///   does not auto-link, replacing every TMDB link the anime has, verified
    ///   ones and episode links included, with what is taken.
    /// </param>
    Task ScheduleSearchForMatch(int anidbId, bool force);

    /// <summary>
    /// Scans all series for missing TMDB matches and schedules search jobs.
    /// </summary>
    Task ScanForMatches();

    #endregion

    #region Rate Limiting

    /// <summary>
    /// Gets a consistent snapshot of the TMDB pause state, for a 429 or the 5XX circuit breaker.
    /// </summary>
    TmdbRateLimitPauseStatus GetPauseStatus();

    #endregion
}
