using System;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Metadata.Anilist.Services;

/// <summary>
/// Anilist metadata service for managing anime metadata.
/// </summary>
public interface IAnilistMetadataService
{
    #region Anime

    /// <summary>
    /// Updates all anime that are currently linked to AniDB entries.
    /// </summary>
    /// <param name="force">Force refresh even if recently updated.</param>
    /// <param name="downloadImages">Whether to download images.</param>
    Task UpdateAllAnime(bool force = false, bool downloadImages = false);

    /// <summary>
    /// Schedules an anime update job.
    /// </summary>
    /// <param name="options">The update options.</param>
    Task ScheduleUpdateOfAnime(AnilistAnimeUpdateOptions options);

    /// <summary>
    /// Updates an Anilist anime's metadata.
    /// </summary>
    /// <param name="options">The update options.</param>
    /// <returns><see langword="true"/> if the anime was updated.</returns>
    Task<bool> UpdateAnime(AnilistAnimeUpdateOptions options);

    /// <summary>
    /// Schedules a job to download all images for an anime.
    /// </summary>
    /// <param name="anilistAnimeId">The Anilist anime ID.</param>
    /// <param name="forceDownload">Force download even if images already exist.</param>
    Task ScheduleDownloadAllAnimeImages(int anilistAnimeId, bool forceDownload = false);

    /// <summary>
    /// Schedules all unused anime to be purged.
    /// </summary>
    /// <param name="olderThan">Only purge anime last updated before this point in time.</param>
    Task PurgeAllUnusedAnime(DateTime? olderThan = null);

    /// <summary>
    /// Schedules an anime purge job.
    /// </summary>
    /// <param name="anilistAnimeId">The Anilist anime ID.</param>
    Task SchedulePurgeOfAnime(int anilistAnimeId);

    /// <summary>
    /// Purges an Anilist anime from the local database.
    /// </summary>
    /// <param name="anilistAnimeId">The Anilist anime ID.</param>
    Task PurgeAnime(int anilistAnimeId);

    #endregion

    #region Rate Limiting

    /// <summary>
    /// Get a snapshot of the AniList rate limiter's pause state.
    /// </summary>
    AnilistRateLimitPauseStatus GetPauseStatus();

    #endregion

    #region Search/Matching

    /// <summary>
    /// Schedules a search job for auto-matching an AniDB anime to Anilist.
    /// </summary>
    /// <param name="anidbId">The AniDB anime ID.</param>
    /// <param name="force">Force search even if already linked.</param>
    Task ScheduleSearchForMatch(int anidbId, bool force);

    /// <summary>
    /// Scans all series for missing Anilist matches and schedules search jobs.
    /// </summary>
    Task ScanForMatches();

    #endregion
}
