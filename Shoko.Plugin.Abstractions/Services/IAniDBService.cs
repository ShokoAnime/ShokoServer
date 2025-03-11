using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Exceptions;

namespace Shoko.Plugin.Abstractions.Services;

/// <summary>
/// AniDB service.
/// </summary>
public interface IAniDBService
{
    #region Banned Status

    /// <summary>
    /// Dispatched when an AniDB HTTP or UDP ban occurs.
    /// </summary>
    event EventHandler<AniDBBannedEventArgs> AniDBBanned;

    /// <summary>
    /// Indicates the AniDB UDP API currently reachable?
    /// </summary>
    bool IsAniDBUdpReachable { get; }

    /// <summary>
    /// Indicates we are currently banned from using the AniDB HTTP API.
    /// </summary>
    bool IsAniDBHttpBanned { get; }

    /// <summary>
    /// Indicates we are currently banned from using the AniDB UDP API.
    /// </summary>
    bool IsAniDBUdpBanned { get; }

    #endregion

    #region "Remote" Search

    /// <summary>
    /// Searches the locally cached AniDB title database for the given <paramref name="query"/>.
    /// </summary>
    /// <param name="query">Query to search for.</param>
    /// <param name="fuzzy">Indicates fuzzy-matching should be used for the search.</param>
    /// <returns>Search results.</returns>
    IReadOnlyList<IAnidbAnimeSearchResult> Search(string query, bool fuzzy = false);

    #endregion

    #region Refresh

    #region By AniDB Anime ID

    /// <summary>
    /// Refreshes the AniDB anime with the given <paramref name="anidbAnimeID"/>.
    /// </summary>
    /// <param name="anidbAnimeID">AniDB Anime ID.</param>
    /// <param name="refreshMethod">Refresh method.</param>
    /// <exception cref="AnidbHttpBannedException">
    /// Indicates that the AniDB user has been temporarily (or permanently) banned.
    /// </exception>
    /// <returns>The refreshed AniDB anime, or <c>null</c> if the anime doesn't exist on AniDB.</returns>
    Task<ISeries?> RefreshByID(int anidbAnimeID, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto);

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
    /// <exception cref="AnidbHttpBannedException">
    /// Indicates that the AniDB user has been temporarily (or permanently) banned.
    /// </exception>
    /// <returns>The refreshed AniDB anime.</returns>
    Task<ISeries> Refresh(ISeries anidbAnime, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto);

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
    Task ScheduleRefresh(ISeries anidbAnime, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto, bool prioritize = false);

    #endregion

    #region By Shoko Series

    /// <summary>
    /// Refreshes the AniDB anime linked to the <paramref name="shokoSeries"/>.
    /// </summary>
    /// <param name="shokoSeries">Shoko series.</param>
    /// <param name="refreshMethod">Refresh method.</param>
    /// <exception cref="AnidbHttpBannedException">
    /// Indicates that the AniDB user has been temporarily (or permanently) banned.
    /// </exception>
    /// <exception cref="NullReferenceException">
    /// Indicates that something is severely broken and the AniDB anime series for the Shoko series is not available
    /// locally.
    /// </exception>
    /// <returns>The refreshed AniDB anime.</returns>
    Task<ISeries> RefreshForShokoSeries(IShokoSeries shokoSeries, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto);

    /// <summary>
    /// Schedules a refresh of the AniDB anime linked to the <paramref name="shokoSeries"/> in the queue.
    /// </summary>
    /// <param name="shokoSeries">Shoko series.</param>
    /// <param name="refreshMethod">Refresh method.</param>
    /// <param name="prioritize">Whether to prioritize the refresh in the queue.</param>
    /// <exception cref="AnidbHttpBannedException">
    /// Indicates that the AniDB user has been temporarily (or permanently) banned.
    /// </exception>
    /// <returns>The refreshed AniDB anime.</returns>
    Task ScheduleRefreshForShokoSeries(IShokoSeries shokoSeries, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto, bool prioritize = false);

    #endregion

    #endregion

    #region AVDump

    /// <summary>
    /// Dispatched when an AVDump event occurs.
    /// </summary>
    event EventHandler<AVDumpEventArgs> AVDumpEvent;

    /// <summary>
    /// Indicates that some version of AVDump is installed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(InstalledAVDumpVersion))]
    bool IsAVDumpInstalled { get; }

    /// <summary>
    /// The version of AVDump that is installed.
    /// </summary>
    string? InstalledAVDumpVersion { get; }

    /// <summary>
    /// The version of AVDump that is Shoko knows is available to be installed.
    /// </summary>
    string? AvailableAVDumpVersion { get; }

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
    /// To get updates from the AVDump session, use the <see cref="AVDumpEvent"/> event.
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
    /// To get updates from the AVDump session, use the <see cref="AVDumpEvent"/> event.
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
    /// To get updates from the AVDump session, use the <see cref="AVDumpEvent"/> event.
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
    /// To get updates from the AVDump session, use the <see cref="AVDumpEvent"/> event.
    /// </remarks>
    /// <param name="videoFiles">The video files to dump.</param>
    /// <returns>
    ///   A <see cref="Task"/> representing the asynchronous operation of scheduling
    ///   the job in the queue.
    /// </returns>
    Task ScheduleAvdumpVideoFiles(params IVideoFile[] videoFiles);

    #endregion
}
