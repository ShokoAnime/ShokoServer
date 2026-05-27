#nullable enable
using System.Collections.Generic;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;

namespace Shoko.QueueProcessor.Events;

/// <summary>
/// Fired when one or more jobs are added to the queue.
/// </summary>
public class QueueItemsAddedEventArgs : System.EventArgs
{
    /// <summary>The newly enqueued items.</summary>
    public IReadOnlyList<QueueItem> AddedItems { get; init; } = [];

    /// <summary>Current waiting items (up to the configured cache size).</summary>
    public IReadOnlyList<QueueItem> WaitingItems { get; init; } = [];

    public int WaitingJobsCount { get; init; }
    public int BlockedJobsCount { get; init; }
    public int TotalJobsCount { get; init; }
    public int ExecutingJobsCount { get; init; }
    public int ThreadCount { get; init; }

    /// <summary>Latest analytics snapshot, if available.</summary>
    public QueueMetricsSnapshot? Metrics { get; init; }
}
