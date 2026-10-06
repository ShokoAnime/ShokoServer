using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Events;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;

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

    #region Cached Anime

    /// <summary>
    ///   Lists the AniDB anime in the local cache, in the collection or not,
    ///   filtered and ordered by <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The filters and order, or <c>null</c> for every anime by title.</param>
    /// <exception cref="ArgumentNullException">
    ///   The options' filter depends on the user and the options name none.
    /// </exception>
    /// <returns>
    ///   The matching anime, each with its Shoko series, or <c>null</c> when
    ///   it is not in the collection.
    /// </returns>
    IReadOnlyList<(IAnidbAnime Anime, IShokoSeries? Series)> GetCachedAnime(AnidbAnimeListOptions? options = null);

    /// <summary>
    ///   Lists the seasons the cached AniDB anime are in, by the rule on
    ///   <see cref="Containers.IWithYearlySeasons"/>, with how many anime are
    ///   in each, newest first, up to the season after the one under way as
    ///   of the options' <see cref="AnidbAnimeListOptions.At"/>. The season
    ///   under way is always listed. A season's images come from its best
    ///   anime by weighted rating among those starting in it that have a
    ///   poster.
    /// </summary>
    /// <param name="options">The filters on the anime counted; the seasons and order are ignored.</param>
    /// <param name="includeImages">Whether to pick a poster and a backdrop for each season.</param>
    /// <exception cref="ArgumentNullException">
    ///   The options' filter depends on the user and the options name none.
    /// </exception>
    /// <returns>The seasons.</returns>
    IReadOnlyList<AnidbAnimeSeasonCount> GetCachedAnimeSeasons(AnidbAnimeListOptions? options = null, bool includeImages = false);

    #endregion

    #region Start Season Overrides

    /// <summary>
    ///   Dispatched when the start season override of an AniDB anime is set,
    ///   changed or removed. Not dispatched for a set that changed nothing.
    /// </summary>
    event EventHandler<AnidbStartSeasonOverrideChangedEventArgs> StartSeasonOverrideChanged;

    /// <summary>
    ///   Gets the start season a user set by hand for an AniDB anime.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <returns>The override, or <c>null</c> when none is set.</returns>
    AnidbStartSeasonOverride? GetStartSeasonOverride(int anidbAnimeID);

    /// <summary>
    ///   Lists every start season override, by AniDB anime ID.
    /// </summary>
    /// <returns>The overrides, by ascending AniDB anime ID.</returns>
    IReadOnlyList<AnidbStartSeasonOverride> GetStartSeasonOverrides();

    /// <summary>
    ///   Sets the season an AniDB anime starts in, in place of the one the
    ///   yearly season rule works out (see
    ///   <see cref="Containers.IWithYearlySeasons"/>), for the current actor.
    /// </summary>
    /// <remarks>
    ///   The anime need not be in the local cache yet, so it can be set
    ///   ahead of a fetch; it applies once the anime is there. Setting the
    ///   same season again changes nothing.
    /// </remarks>
    /// <param name="anidbAnimeID">The AniDB anime ID, above <c>0</c>.</param>
    /// <param name="year">The year, from 1900 to 9999.</param>
    /// <param name="season">The season.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <paramref name="anidbAnimeID"/>, <paramref name="year"/> or
    ///   <paramref name="season"/> is out of range.
    /// </exception>
    /// <returns>The override as it is now stored.</returns>
    AnidbStartSeasonOverride SetStartSeasonOverride(int anidbAnimeID, int year, YearlySeason season);

    /// <summary>
    ///   Removes the start season override of an AniDB anime, so the yearly
    ///   season rule applies again.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <returns><c>true</c> when an override was removed, <c>false</c> when none was set.</returns>
    bool RemoveStartSeasonOverride(int anidbAnimeID);

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
