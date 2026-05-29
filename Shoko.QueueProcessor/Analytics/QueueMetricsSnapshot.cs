using System;
using System.Collections.Generic;

namespace Shoko.QueueProcessor.Analytics;

/// <summary>
/// Immutable analytics snapshot returned by <see cref="QueueMetrics.GetSnapshot"/>.
/// Safe to serialize and send over SignalR / REST.
/// </summary>
public record QueueMetricsSnapshot
{
    /// <summary>Average completions per second over the configured metrics window (default: last 60 s).</summary>
    public double JobsPerSecond { get; init; }

    /// <summary>Highest single-second completion rate observed within the metrics window.</summary>
    public double JobsPerSecondPeak { get; init; }

    /// <summary>Per-type metrics keyed by short type name.</summary>
    public IReadOnlyDictionary<string, TypeMetrics> ByType { get; init; } = new Dictionary<string, TypeMetrics>();

    /// <summary>Per-pool live status keyed by pool name.</summary>
    public IReadOnlyDictionary<string, PoolStatus> ByPool { get; init; } = new Dictionary<string, PoolStatus>();

    /// <summary>Total jobs currently waiting (across all pools).</summary>
    public int TotalWaiting { get; init; }

    /// <summary>Total jobs currently executing (across all pools).</summary>
    public int TotalExecuting { get; init; }

    /// <summary>Waiting jobs that have been retried at least once (<c>RetryCount &gt; 0</c>).</summary>
    public int TotalRetrying { get; init; }

    /// <summary>Waiting jobs currently blocked by acquisition filters.</summary>
    public int TotalBlocked { get; init; }

    /// <summary>When this snapshot was captured (UTC).</summary>
    public DateTime SnapshotAt { get; init; }
}
