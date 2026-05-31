using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Newtonsoft.Json;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.QueueProcessor;
using Shoko.Server.Scheduling.Jobs.Shoko;
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
    /// Path to the SQLite queue database file. Relative paths are resolved against
    /// the application data directory. Only used when <see cref="Provider"/> is SQLite.
    /// </summary>
    [Display(Name = "Database File")]
    [RequiresRestart]
    [EnvironmentVariable("QUEUE_SQLITE_FILE")]
    [Visibility(
        Visibility = DisplayVisibility.Hidden,
        ToggleWhenMemberIsSet = nameof(Provider),
        ToggleWhenSetTo = DatabaseProvider.SQLite,
        ToggleVisibilityTo = DisplayVisibility.Visible
    )]
    [field: JsonIgnore]
    public string SQLiteFilePath
    {
        get;
        set
        {
            // Strip StaticDataPath prefix to keep the stored value portable
            if (value.StartsWith(ApplicationPaths.StaticDataPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                value = value[(ApplicationPaths.StaticDataPath.Length + 1)..];
            else if (value.StartsWith(ApplicationPaths.StaticDataPath + '/', StringComparison.OrdinalIgnoreCase))
                value = value[(ApplicationPaths.StaticDataPath.Length + 1)..];
            field = value;
        }
    } = "SQLite/Queue.db3";

    /// <summary>
    /// The connection string for the queue database. Only used when <see cref="Provider"/> is MySQL or SQL Server.
    /// </summary>
    [Display(Name = "Connection String")]
    [RequiresRestart]
    [EnvironmentVariable("QUEUE_CONNECTION_STRING")]
    [TextArea]
    [Visibility(
        Visibility = DisplayVisibility.Visible,
        ToggleWhenMemberIsSet = nameof(Provider),
        ToggleWhenSetTo = DatabaseProvider.SQLite,
        ToggleVisibilityTo = DisplayVisibility.Hidden
    )]
    public string ConnectionString { get; set; } = string.Empty;

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
