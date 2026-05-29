using System.Collections.Generic;
using Shoko.QueueProcessor.Analytics;

namespace Shoko.QueueProcessor.Abstractions;

/// <summary>
/// Full queue state snapshot returned by <see cref="IQueueScheduler.GetState"/>.
/// Replaces <c>QueueStateContext</c>.
/// </summary>
public class QueueState
{

    /// <summary>Total number of waiting jobs (including blocked).</summary>
    public int TotalWaiting { get; init; }

    /// <summary>Total number of executing jobs.</summary>
    public int TotalExecuting { get; init; }

    /// <summary>Maximum concurrent workers across all pools.</summary>
    public int MaxWorkers { get; init; }

    /// <summary>Whether the queue is currently paused.</summary>
    public bool IsPaused { get; init; }

    /// <summary>Per-pool status snapshots.</summary>
    public IReadOnlyDictionary<string, PoolStatus> PoolStatus { get; init; } = new Dictionary<string, PoolStatus>();

    /// <summary>Analytics metrics snapshot.</summary>
    public QueueMetricsSnapshot? Metrics { get; init; }
}
