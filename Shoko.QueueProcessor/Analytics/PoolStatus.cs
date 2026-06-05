using System;
using System.Collections.Generic;

namespace Shoko.QueueProcessor.Analytics;

/// <summary>Live status snapshot for a single worker pool.</summary>
public record PoolStatus
{
    /// <summary>Pool name (matches concurrency group name or job type name).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Maximum number of concurrent workers in this pool.</summary>
    public int MaxWorkers { get; init; }

    /// <summary>Workers currently executing a job.</summary>
    public int ActiveWorkers { get; init; }

    /// <summary>Workers currently idle, awaiting a job.</summary>
    public int IdleWorkers { get; init; }

    /// <summary>
    /// True when all job types in this pool are currently excluded by one or more acquisition filters
    /// (e.g., AniDB banned, network unavailable).
    /// </summary>
    public bool IsBlocked { get; init; }

    /// <summary>Number of waiting jobs in this pool's sub-queue (includes blocked and scheduled).</summary>
    public int WaitingCount { get; init; }

    /// <summary>
    /// Number of jobs in this pool deferred to a future scheduled time (not yet ready to run).
    /// A subset of <see cref="WaitingCount"/>.
    /// </summary>
    public int ScheduledCount { get; init; }

    /// <summary>Short names of job types handled by this pool.</summary>
    public IReadOnlyList<string> HandledTypeNames { get; init; } = [];

    /// <summary>
    /// UTC timestamp of the most recent job acquisition (stamped in <c>IncrementActive</c>).
    /// Null if the pool has never run a job. Lets throttled SignalR pushes report
    /// "this group was active in the last N ms" even when <see cref="ActiveWorkers"/> is back to 0.
    /// </summary>
    public DateTimeOffset? LastActiveAt { get; init; }
}
