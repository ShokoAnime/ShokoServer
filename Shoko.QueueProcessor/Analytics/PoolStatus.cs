#nullable enable
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

    /// <summary>Number of waiting jobs in this pool's sub-queue.</summary>
    public int WaitingCount { get; init; }

    /// <summary>Short names of job types handled by this pool.</summary>
    public IReadOnlyList<string> HandledTypeNames { get; init; } = [];
}
