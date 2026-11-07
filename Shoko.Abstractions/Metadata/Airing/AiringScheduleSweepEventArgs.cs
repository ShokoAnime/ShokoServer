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
    ///   The sweep the chunk is part of, the same for every chunk from the
    ///   first one after the walk began to the one that ended it.
    /// </summary>
    public required Guid SweepID { get; init; }

    /// <summary>
    ///   When the sweep's first chunk started, in UTC.
    /// </summary>
    public required DateTime SweepStartedAt { get; init; }

    /// <summary>
    ///   The chunk's place in its sweep, from <c>0</c> for the first.
    /// </summary>
    public required int ChunkIndex { get; init; }

    /// <summary>
    ///   Whether the chunk was the first of its sweep.
    /// </summary>
    public bool IsFirstChunk => ChunkIndex is 0;

    /// <summary>
    ///   How many chunks in a row, this one included, got nowhere in the walk.
    ///   Once it reaches three the sweep is held back to the provider's own
    ///   interval until it moves again.
    /// </summary>
    public required int NoProgressCount { get; init; }

    /// <summary>
    ///   When the chunk started, in UTC.
    /// </summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    ///   When the chunk ended, in UTC.
    /// </summary>
    public required DateTime CompletedAt { get; init; }

    /// <summary>
    ///   Whether the sweep is over, as opposed to resuming in a later chunk.
    ///   A chunk that failed, timed out or was stopped keeps its place, so the
    ///   sweep is not over and resumes there; only a cursor too long to store
    ///   ends a failed sweep.
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
