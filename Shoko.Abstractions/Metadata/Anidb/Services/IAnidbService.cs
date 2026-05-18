using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Events;

namespace Shoko.Abstractions.Metadata.Anidb.Services;

/// <summary>
/// AniDB service.
/// </summary>
public interface IAnidbService
{
    #region Banned Status

    /// <summary>
    /// Dispatched when an AniDB HTTP or UDP ban occurs.
    /// </summary>
    event EventHandler<AnidbBanOccurredEventArgs> BanOccurred;

    /// <summary>
    /// Dispatched when an AniDB HTTP or UDP ban expires.
    /// </summary>
    event EventHandler<AnidbBanOccurredEventArgs> BanExpired;

    /// <summary>
    /// Indicates we are currently banned from using the AniDB HTTP API.
    /// </summary>
    bool IsAnidbHttpBanned { get; }

    /// <summary>
    /// Indicates we are currently banned from using the AniDB UDP API.
    /// </summary>
    bool IsAnidbUdpBanned { get; }

    /// <summary>
    /// Indicates the AniDB UDP API currently reachable?
    /// </summary>
    bool IsAnidbUdpReachable { get; }

    /// <summary>
    /// The last event arguments indicating when the last or current AniDB HTTP
    /// ban started, and/or if a ban is currently still in effect.
    /// </summary>
    AnidbBanOccurredEventArgs LastHttpBanEventArgs { get; }

    /// <summary>
    /// The last event arguments indicating when the last or current AniDB UDP
    /// ban started, and/or if a ban is currently still in effect.
    /// </summary>
    AnidbBanOccurredEventArgs LastUdpBanEventArgs { get; }

    #endregion

    #region URLs

    /// <summary>
    ///   Get or set the AniDB HTTP API base URL override. If set to
    ///   <code>null</code>, an empty string, or the default value, then the
    ///   override will be removed.
    /// </summary>
    string? AnidbHttpApiBaseUrlOverride { get; set; }

    /// <summary>
    /// Get or set the AniDB CDN base URL override. If set to
    /// <code>null</code>, an empty string, or the default value, then the
    /// override will be removed.
    /// </summary>
    string? AnidbCdnBaseUrlOverride { get; set; }

    /// <summary>
    /// Get or set the AniDB title cache URL override. If set to
    /// <code>null</code>, an empty string, or the default value, then the
    /// override will be removed.
    /// </summary>
    string? AnidbTitleCacheUrlOverride { get; set; }

    #endregion

    #region "Remote" Search

    /// <summary>
    /// Searches the locally cached AniDB title database for the given <paramref name="query"/>.
    /// </summary>
    /// <param name="query">Query to search for.</param>
    /// <param name="fuzzy">Indicates fuzzy-matching should be used for the search.</param>
    /// <returns>Search results.</returns>
    IReadOnlyList<IAnidbAnimeSearchResult> SearchAnime(string query, bool fuzzy = false);

    /// <summary>
    /// Searches the locally cached AniDB title database for the given <paramref name="anidbID"/>.
    /// </summary>
    /// <param name="anidbID">AniDB ID to search for.</param>
    /// <returns>Search result, if found by ID.</returns>
    IAnidbAnimeSearchResult? SearchAnimeByID(int anidbID);

    #endregion

    #region Refresh

    #region By AniDB Anime ID

    /// <summary>
    /// Refreshes the AniDB anime with the given <paramref name="anidbAnimeID"/>.
    /// </summary>
    /// <param name="anidbAnimeID">AniDB Anime ID.</param>
    /// <param name="refreshMethod">Refresh method.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="AnidbHttpBannedException">
    /// Indicates that the AniDB user has been temporarily (or permanently) banned.
    /// </exception>
    /// <returns>The refreshed AniDB anime, or <c>null</c> if the anime doesn't exist on AniDB.</returns>
    Task<IAnidbAnime?> RefreshAnimeByID(int anidbAnimeID, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a refresh of the AniDB anime with the given <paramref name="anidbAnimeID"/> in the queue.
    /// </summary>
    /// <param name="anidbAnimeID">AniDB Anime ID.</param>
    /// <param name="refreshMethod">Refresh method.</param>
    /// <param name="prioritize">Whether to prioritize the refresh in the queue.</param>
    /// <exception cref="AnidbHttpBannedException">
    /// Indicates that the AniDB user has been temporarily (or permanently) banned.
    /// </exception>
    /// <returns>The refreshed AniDB anime, or <c>null</c> if the anime doesn't exist on AniDB.</returns>
    Task ScheduleRefreshOfAnimeByID(int anidbAnimeID, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto, bool prioritize = false);

    #endregion

    #region By AniDB Anime

    /// <summary>
    /// Refreshes the AniDB anime represented by <paramref name="anidbAnime"/>.
    /// </summary>
    /// <param name="anidbAnime">AniDB anime.</param>
    /// <param name="refreshMethod">Refresh method.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="AnidbHttpBannedException">
    /// Indicates that the AniDB user has been temporarily (or permanently) banned.
    /// </exception>
    /// <returns>The refreshed AniDB anime.</returns>
    Task<IAnidbAnime> RefreshAnime(IAnidbAnime anidbAnime, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a refresh of the AniDB anime represented by <paramref name="anidbAnime"/> in the queue.
    /// </summary>
    /// <param name="anidbAnime">AniDB anime.</param>
    /// <param name="refreshMethod">Refresh method.</param>
    /// <param name="prioritize">Whether to prioritize the refresh in the queue.</param>
    /// <exception cref="AnidbHttpBannedException">
    /// Indicates that the AniDB user has been temporarily (or permanently) banned.
    /// </exception>
    /// <returns>The refreshed AniDB anime.</returns>
    Task ScheduleRefreshOfAnime(IAnidbAnime anidbAnime, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto, bool prioritize = false);

    #endregion

    #endregion

    #region AniDB Tags

    /// <summary>
    /// Gets all the AniDB tags stored in the local database.
    /// </summary>
    /// <param name="topLevelOnly">
    ///   Whether to only return top-level tags.
    /// </param>
    /// <returns>
    ///   All tags or all top-level tags in the local database.
    /// </returns>
    IEnumerable<IAnidbTag> GetAllTags(bool topLevelOnly = false);

    #endregion

    #region Images

    /// <summary>
    /// Schedule processing of AniDB image records and cross-references for an
    /// anime and related entities.
    /// </summary>
    /// <param name="anidbAnimeID">AniDB anime ID.</param>
    /// <param name="onlyPosters">Only process poster images.</param>
    /// <param name="forceDownload">Force re-download of images.</param>
    /// <param name="prioritize">Whether to prioritize the queue task.</param>
    Task ScheduleImagesForAnimeByID(int anidbAnimeID, bool onlyPosters = false, bool forceDownload = false, bool prioritize = false);

    #endregion

    #region Purge

    /// <summary>
    /// Purge all AniDB anime entries that are no longer linked to a Shoko series.
    /// </summary>
    Task PurgeAllUnusedAnime();

    /// <summary>
    /// Schedule purge of an AniDB anime from the local database.
    /// </summary>
    /// <param name="anidbAnimeID">AniDB anime ID.</param>
    /// <param name="removeFromMylist">Remove release links from AniDB MyList while purging.</param>
    /// <param name="prioritize">Whether to prioritize the queue task.</param>
    Task SchedulePurgeOfAnimeByID(int anidbAnimeID, bool removeFromMylist = true, bool prioritize = false);

    /// <summary>
    /// Purge an AniDB anime from the local database.
    /// </summary>
    /// <param name="anidbAnimeID">AniDB anime ID.</param>
    /// <param name="removeFromMylist">Remove release links from AniDB MyList while purging.</param>
    Task PurgeAnimeByID(int anidbAnimeID, bool removeFromMylist = true);

    #endregion
}
