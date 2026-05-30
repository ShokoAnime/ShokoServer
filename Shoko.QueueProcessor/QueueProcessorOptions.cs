using System;
using System.Collections.Generic;

namespace Shoko.QueueProcessor;

/// <summary>Configuration options for the queue processor.</summary>
public class QueueProcessorOptions
{
    // ── Storage ──────────────────────────────────────────────────────────────

    /// <summary>Database provider. Defaults to SQLite.</summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.SQLite;

    /// <summary>Connection string for the queue database.</summary>
    public string ConnectionString { get; set; } = "Data Source=queue.db";

    // ── Concurrency ───────────────────────────────────────────────────────────

    /// <summary>
    /// Hard ceiling on total concurrent workers across all pools.
    /// Individual pool sizes are further constrained by their concurrency attributes.
    /// </summary>
    public int MaxTotalWorkers { get; set; } = Environment.ProcessorCount + 4;

    /// <summary>
    /// Worker count for the catch-all <c>"Default"</c> pool. Defaults to <see cref="MaxTotalWorkers"/>
    /// so the default pool alone can saturate the global concurrency cap when no other pools are busy.
    /// </summary>
    public int DefaultPoolMaxWorkers { get; set; } = Environment.ProcessorCount + 4;

    // ── PersistenceBuffer ─────────────────────────────────────────────────────

    /// <summary>
    /// Idle flush interval in milliseconds. A job enqueued <em>and</em> completed within this
    /// window will never hit the database.
    /// </summary>
    public int FlushIntervalMs { get; set; } = 3000;

    /// <summary>Force-flush when the pending buffer reaches this size.</summary>
    public int MaxFlushBatch { get; set; } = 500;

    // ── Retry policy ──────────────────────────────────────────────────────────

    /// <summary>Maximum retry attempts before the job is discarded (global default).</summary>
    public int RetryMaxAttempts { get; set; } = 8;

    /// <summary>Base delay for the first retry in seconds (subsequent = base * 2^n).</summary>
    public int RetryBaseDelaySeconds { get; set; } = 30;

    /// <summary>Maximum retry delay cap in seconds.</summary>
    public int RetryMaxDelaySeconds { get; set; } = 3600;

    // ── Worker idle behaviour ─────────────────────────────────────────────────

    /// <summary>
    /// Maximum time a worker waits for a wake signal before polling for
    /// <c>ScheduledAt</c>-ready jobs. Set lower if many deferred jobs are expected.
    /// </summary>
    public int MaxIdlePollIntervalMs { get; set; } = 5000;

    // ── Analytics ─────────────────────────────────────────────────────────────

    /// <summary>Sliding window (seconds) for the jobs/sec calculation.</summary>
    public int MetricsWindowSeconds { get; set; } = 60;

    /// <summary>Per-type rolling average sample count for execution time.</summary>
    public int MetricsRollingAvgSamples { get; set; } = 100;

    // ── Watchdog ──────────────────────────────────────────────────────────────

    /// <summary>
    /// How long a job must be running before the watchdog logs a warning (seconds).
    /// Jobs decorated with <see cref="Concurrency.LongRunningAttribute"/> are exempt.
    /// Default: 90.
    /// </summary>
    public int WatchdogTimeoutSeconds { get; set; } = 90;

    // ── Per-type overrides ────────────────────────────────────────────────────

    /// <summary>
    /// Map of job type name → desired concurrency limit.
    /// Can lower but never raise above the type's <c>MaxAllowedConcurrentJobs</c>.
    /// </summary>
    public Dictionary<string, int> LimitedConcurrencyOverrides { get; set; } = [];
}

/// <summary>Supported database providers for the queue.</summary>
public enum DatabaseProvider
{
    SQLite,
    MySQL,
    SqlServer
}
