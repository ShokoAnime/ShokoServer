using System;

namespace Shoko.QueueProcessor.Events;

/// <summary>
/// Fired when a running job reports new progress. Throttled per job, so not every report fires
/// one; the queue snapshot always has the latest value.
/// </summary>
public class QueueJobProgressEventArgs : EventArgs
{
    /// <summary>
    /// The key of the job that reported.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// The job's progress, as a percentage from 0 to 100.
    /// </summary>
    public required decimal Progress { get; init; }
}
