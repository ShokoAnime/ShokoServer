#nullable enable
using System.Collections.Generic;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;

namespace Shoko.QueueProcessor.Events;

/// <summary>
/// Fired when the set of executing jobs changes (job started or completed).
/// Replaces the Quartz-coupled version in <c>Shoko.Server/Scheduling</c>.
/// </summary>
public class QueueChangedEventArgs : System.EventArgs
{
    /// <summary>Jobs that started executing in this event.</summary>
    public IReadOnlyList<QueueItem> AddedItems { get; init; } = [];

    /// <summary>Jobs that completed or were discarded in this event.</summary>
    public IReadOnlyList<QueueItem> RemovedItems { get; init; } = [];

    /// <summary>Full current snapshot of executing items.</summary>
    public IReadOnlyList<QueueItem> ExecutingItems { get; init; } = [];

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
