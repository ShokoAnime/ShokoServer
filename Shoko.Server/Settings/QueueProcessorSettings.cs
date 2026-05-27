using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.QueueProcessor;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Server;
using Shoko.Server.Services;

namespace Shoko.Server.Settings;

public class QueueProcessorSettings
{
    /// <summary>
    /// Determines the database backend to use for the queue.
    /// </summary>
    [Display(Name = "Database Type")]
    [RequiresRestart]
    [EnvironmentVariable("QUEUE_DB_TYPE")]
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.SQLite;

    /// <summary>
    /// The connection string for the queue database.
    /// </summary>
    [Display(Name = "Connection String")]
    [RequiresRestart]
    [EnvironmentVariable("QUEUE_CONNECTION_STRING")]
    [TextArea]
    public string ConnectionString { get; set; } = $"Data Source={Path.Combine(ApplicationPaths.StaticDataPath, "SQLite", "Queue.db3")};Mode=ReadWriteCreate;Pooling=True";

    /// <summary>
    /// Maximum total concurrent workers across all pools. Defaults to CPU count + 4.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [RequiresRestart]
    [EnvironmentVariable("QUEUE_MAX_WORKERS")]
    [Range(-1, int.MaxValue)]
    public int MaxTotalWorkers { get; set; }

    /// <summary>
    /// The number of waiting jobs to cache for API usage.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [EnvironmentVariable("QUEUE_WAITING_CACHE_SIZE")]
    [Range(0, 1000)]
    public int WaitingCacheSize { get; set; } = 100;

    /// <summary>
    /// Milliseconds between coalesced DB flush operations.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [Display(Name = "Flush Interval (ms)")]
    [EnvironmentVariable("QUEUE_FLUSH_INTERVAL")]
    [Range(100, 30_000)]
    public int FlushIntervalMs { get; set; } = 3000;

    /// <summary>
    /// Maximum number of jobs to flush in a single DB batch.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [Display(Name = "Max Flush Batch")]
    [EnvironmentVariable("QUEUE_MAX_FLUSH_BATCH")]
    [Range(1, 10_000)]
    public int MaxFlushBatch { get; set; } = 500;

    /// <summary>
    /// A map of job type name to the number of allowed concurrent workers of that type.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [RequiresRestart]
    [EnvironmentVariable("QUEUE_CONCURRENCY_LIMITS")]
    public Dictionary<string, int> LimitedConcurrencyOverrides { get; set; } = new()
    {
        { nameof(HashFileJob), 2 },
    };
}
