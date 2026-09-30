using System;
using Shoko.QueueProcessor.Abstractions;

namespace Shoko.QueueProcessor.Events;

/// <summary>
/// Fired when a user asks for a running job to be cancelled. The job keeps running until it
/// notices, and stays in the queue snapshot, marked as cancellation requested, until then.
/// </summary>
public class QueueJobCancellationEventArgs : EventArgs
{
    /// <summary>
    /// The running job, marked as cancellation requested.
    /// </summary>
    public required QueueItem Item { get; init; }
}
