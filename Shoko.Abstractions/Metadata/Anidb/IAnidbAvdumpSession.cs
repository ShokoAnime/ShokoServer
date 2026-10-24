using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Anidb;

/// <summary>
///   An AVDump session, queued or running, for one or more videos.
/// </summary>
public interface IAnidbAvdumpSession
{
    /// <summary>
    ///   The ID of the session, the one the AVDump events carry.
    /// </summary>
    int SessionID { get; }

    /// <summary>
    ///   The Shoko IDs of the videos the session dumps.
    /// </summary>
    IReadOnlyList<int> VideoIDs { get; }

    /// <summary>
    ///   The absolute paths of the files the session dumps.
    /// </summary>
    IReadOnlyList<string> AbsolutePaths { get; }

    /// <summary>
    ///   Whether the session is running, rather than waiting to start.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    ///   Whether the session finished without errors.
    /// </summary>
    bool IsSuccess { get; }

    /// <summary>
    ///   How far the session is, from 0 to 100.
    /// </summary>
    double Progress { get; }

    /// <summary>
    ///   The number of creation requests AniDB accepted.
    /// </summary>
    int SucceededCreqCount { get; }

    /// <summary>
    ///   The number of creation requests still waiting to be sent.
    /// </summary>
    int PendingCreqCount { get; }

    /// <summary>
    ///   The number of creation requests that failed.
    /// </summary>
    int FailedCreqCount { get; }

    /// <summary>
    ///   When the session was started, in UTC.
    /// </summary>
    DateTime StartedAt { get; }

    /// <summary>
    ///   When the session ended, in UTC, or <c>null</c> while it runs.
    /// </summary>
    DateTime? EndedAt { get; }
}
