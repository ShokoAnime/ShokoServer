namespace Shoko.QueueProcessor.Analytics;

/// <summary>Per-job-type performance metrics snapshot.</summary>
public record TypeMetrics
{
    /// <summary>Short type name (e.g., <c>"HashFileJob"</c>).</summary>
    public string TypeName { get; init; } = string.Empty;

    /// <summary>Name of the pool responsible for this type.</summary>
    public string PoolName { get; init; } = string.Empty;

    /// <summary>Jobs of this type currently in the waiting sub-queue.</summary>
    public int Waiting { get; init; }

    /// <summary>Jobs of this type currently executing.</summary>
    public int Executing { get; init; }

    /// <summary>Rolling average execution time (last N samples, default N=100).</summary>
    public double AvgExecutionMs { get; init; }

    /// <summary>Total completions since server startup.</summary>
    public long TotalCompleted { get; init; }

    /// <summary>Total failures (before retry) since server startup.</summary>
    public long TotalFailed { get; init; }
}
