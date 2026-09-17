using System;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Event arguments for a finished chunk of a core-driven sweep. Dispatched
///   once per chunk, whatever the outcome.
/// </summary>
/// <remarks>
///   Nothing here is stored. The server keeps only what the driver needs to
///   resume, so this event, and the log line next to it, is the whole record of
///   what a sweep did. Something that wants a history subscribes and keeps its
///   own.
/// </remarks>
public class AiringScheduleSweepEventArgs : EventArgs
{
    /// <summary>
    ///   The provider that was swept.
    /// </summary>
    public required AiringScheduleProviderInfo Provider { get; init; }

    /// <summary>
    ///   How the chunk ended.
    /// </summary>
    public required AiringScheduleSweepOutcome Outcome { get; init; }

    /// <summary>
    ///   When the chunk started, in UTC.
    /// </summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    ///   When the chunk ended, in UTC.
    /// </summary>
    public required DateTime CompletedAt { get; init; }

    /// <summary>
    ///   Whether the sweep is over, as opposed to resuming in a later chunk. A
    ///   sweep that ended badly is also over, since the outcome says so.
    /// </summary>
    public required bool IsFinished { get; init; }

    /// <summary>
    ///   Why the sweep failed, or <c>null</c> when it did not.
    /// </summary>
    public required string? ErrorMessage { get; init; }

    /// <summary>
    ///   How long the chunk took.
    /// </summary>
    public TimeSpan Duration => CompletedAt - StartedAt;
}
