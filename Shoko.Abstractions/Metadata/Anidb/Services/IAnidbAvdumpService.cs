using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Anidb.Events;
using Shoko.Abstractions.Video;

namespace Shoko.Abstractions.Metadata.Anidb.Services;

/// <summary>
///   Service for interacting with AniDB's AVDump utility, if available.
/// </summary>
public interface IAnidbAvdumpService
{
    /// <summary>
    /// Dispatched when an AVDump event occurs.
    /// </summary>
    event EventHandler<AnidbAvdumpEventArgs> AvdumpEvent;

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
}
