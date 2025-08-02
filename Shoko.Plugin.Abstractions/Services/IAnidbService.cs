using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Anidb;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Exceptions;

namespace Shoko.Plugin.Abstractions.Services;

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

    #region "Remote" Search

    /// <summary>
    /// Searches the locally cached AniDB title database for the given <paramref name="query"/>.
    /// </summary>
    /// <param name="query">Query to search for.</param>
    /// <param name="fuzzy">Indicates fuzzy-matching should be used for the search.</param>
    /// <returns>Search results.</returns>
    IReadOnlyList<IAnidbAnimeSearchResult> Search(string query, bool fuzzy = false);

    /// <summary>
    /// Searches the locally cached AniDB title database for the given <paramref name="anidbID"/>.
    /// </summary>
    /// <param name="anidbID">AniDB ID to search for.</param>
    /// <returns>Search result, if found by ID.</returns>
    IAnidbAnimeSearchResult? SearchByID(int anidbID);

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
    Task<IAnidbAnime?> RefreshByID(int anidbAnimeID, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto, CancellationToken cancellationToken = default);

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
    Task ScheduleRefreshByID(int anidbAnimeID, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto, bool prioritize = false);

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
    Task<IAnidbAnime> Refresh(IAnidbAnime anidbAnime, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto, CancellationToken cancellationToken = default);

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
    Task ScheduleRefresh(IAnidbAnime anidbAnime, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto, bool prioritize = false);

    #endregion

    #endregion

    #region AVDump

    /// <summary>
    /// Dispatched when an AVDump event occurs.
    /// </summary>
    event EventHandler<AvdumpEventArgs> AvdumpEvent;

    /// <summary>
    /// Indicates that some version of AVDump is installed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(InstalledAvdumpVersion))]
    bool IsAvdumpInstalled { get; }

    /// <summary>
    /// The version of AVDump that is installed.
    /// </summary>
    string? InstalledAvdumpVersion { get; }

    /// <summary>
    /// The version of AVDump that is Shoko knows is available to be installed.
    /// </summary>
    string? AvailableAvdumpVersion { get; }

    /// <summary>
    /// Update the installed AVDump component.
    /// </summary>
    /// <param name="force">
    /// Forcefully update the AVDump component regardless
    /// of the version previously installed, if any.
    /// </param>
    /// <returns>If the AVDump component was updated.</returns>
    bool UpdateAvdump(bool force = false);

    /// <summary>
    /// Start a new AVDump3 session for one or more <paramref name="videos"/>.
    /// </summary>
    /// <remarks>
    /// To get updates from the AVDump session, use the <see cref="AvdumpEvent"/> event.
    /// </remarks>
    /// <param name="videos">The videos to dump.</param>
    /// <returns>
    ///   A <see cref="Task"/> representing the asynchronous operation of dumping
    ///   the videos.
    /// </returns>
    Task AvdumpVideos(params IVideo[] videos);

    /// <summary>
    /// Schedule an AVDump3 session to be ran on in the queue for one or more <paramref name="videos"/>.
    /// </summary>
    /// <remarks>
    /// To get updates from the AVDump session, use the <see cref="AvdumpEvent"/> event.
    /// </remarks>
    /// <param name="videos">The videos to dump.</param>
    /// <returns>
    ///   A <see cref="Task"/> representing the asynchronous operation of scheduling
    ///   the job in the queue.
    /// </returns>
    Task ScheduleAvdumpVideos(params IVideo[] videos);

    /// <summary>
    /// Start a new AVDump3 session for one or more <paramref name="videoFiles"/>.
    /// </summary>
    /// <remarks>
    /// To get updates from the AVDump session, use the <see cref="AvdumpEvent"/> event.
    /// </remarks>
    /// <param name="videoFiles">The video files to dump.</param>
    /// <returns>
    ///   A <see cref="Task"/> representing the asynchronous operation of dumping
    ///   the files.
    /// </returns>
    Task AvdumpVideoFiles(params IVideoFile[] videoFiles);

    /// <summary>
    /// Schedule an AVDump3 session to be ran on in the queue for one or more <paramref name="videoFiles"/>.
    /// </summary>
    /// <remarks>
    /// To get updates from the AVDump session, use the <see cref="AvdumpEvent"/> event.
    /// </remarks>
    /// <param name="videoFiles">The video files to dump.</param>
    /// <returns>
    ///   A <see cref="Task"/> representing the asynchronous operation of scheduling
    ///   the job in the queue.
    /// </returns>
    Task ScheduleAvdumpVideoFiles(params IVideoFile[] videoFiles);

    #endregion
}
