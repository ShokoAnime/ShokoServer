using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Shoko.Plugin.Abstractions.Config.Attributes;
using Shoko.Plugin.Abstractions.Config.Enums;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.Server.Settings;

public class QuartzSettings
{
    /// <summary>
    /// Determines the database backend to use for Quartz.
    /// </summary>
    [Display(Name = "Database Type")]
    [RequiresRestart]
    [EnvironmentVariable("QUARTZ_DB_TYPE")]
    public Constants.DatabaseType DatabaseType { get; set; } = Constants.DatabaseType.SQLite;

    /// <summary>
    /// The connection string for the database
    /// </summary>
    [Display(Name = "Connection String")]
    [RequiresRestart]
    [EnvironmentVariable("QUARTZ_CONNECTION_STRING")]
    [TextArea]
    public string ConnectionString { get; set; } = $"Data Source={Path.Combine(Utils.ApplicationPath, "SQLite", "Quartz.db3")};Mode=ReadWriteCreate;Pooling=True";

    /// <summary>
    /// Set this value to override the default size of the queue thread pool.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [RequiresRestart]
    [EnvironmentVariable("QUARTZ_MAX_THREAD_POOL_SIZE")]
    [Range(-1, int.MaxValue)]
    public int MaxThreadPoolSize { get; set; }

    /// <summary>
    /// The number of waiting jobs to cache for API usage.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [RequiresRestart]
    [EnvironmentVariable("QUARTZ_WAITING_CACHE_SIZE")]
    [Range(0, 1000)]
    public int WaitingCacheSize { get; set; } = 100;

    /// <summary>
    /// The number of milliseconds to wait for before doing a batch insert.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [Display(Name = "Batch Insert Timeout (ms)")]
    [EnvironmentVariable("QUARTZ_BATCH_INSERT_TIMEOUT")]
    [Range(0, 10_000)]
    public int BatchInsertTimeoutInMS { get; set; } = 200;

    /// <summary>
    /// The maximum number of jobs to batch insert at once.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [Display(Name = "Batch Max Insert Size")]
    [EnvironmentVariable("QUARTZ_BATCH_MAX_INSERT_SIZE")]
    [Range(1, 10_000)]
    public int BatchMaxInsertSize { get; set; } = 1;

    /// <summary>
    /// A map of Type (yes, you need to look at the source code, under ./Shoko.Server/Scheduling/Jobs) to the number of allowed concurrent jobs of the same type.
    /// Some types will not be able to have a lower limit, due to API restrictions. HashFileJob is included as an example.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [RequiresRestart]
    [EnvironmentVariable("QUARTZ_CONCURRENCY_LIMITS")]
    public Dictionary<string, int> LimitedConcurrencyOverrides { get; set; } = new()
    {
        { nameof(HashFileJob), 2 },
    };
}
