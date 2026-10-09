using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Newtonsoft.Json;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Enums;
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
    /// The connection string for the queue database. For SQLite, this can be used to append additional options to the connection string. For all other providers this is a required field.
    /// </summary>
    [Display(Name = "Connection String")]
    [RequiresRestart]
    [EnvironmentVariable("QUEUE_CONNECTION_STRING")]
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
    /// Maximum concurrent workers in the catch-all Default pool, for the jobs without a pool of
    /// their own. 0 matches the max total workers, and a higher value is capped to it.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [RequiresRestart]
    [EnvironmentVariable("QUEUE_DEFAULT_POOL_MAX_WORKERS")]
    [Range(0, int.MaxValue)]
    public int DefaultPoolMaxWorkers { get; set; }

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
    /// A metadata provider's job is named with the provider's full type name, e.g.
    /// <c>RefreshMetadataJob&lt;My.Plugin.MyProvider&gt;</c>.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [RequiresRestart]
    [EnvironmentVariable("QUEUE_CONCURRENCY_LIMITS")]
    public Dictionary<string, int> LimitedConcurrencyOverrides { get; set; } = new()
    {
        { nameof(HashFileJob), 2 },
    };

    /// <summary>
    /// Gets the effective maximum of concurrent workers across all pools:
    /// <see cref="MaxTotalWorkers"/>, or the processor count plus four when it is not set.
    /// </summary>
    /// <returns>The effective maximum.</returns>
    public int GetEffectiveMaxTotalWorkers()
        => MaxTotalWorkers > 0 ? MaxTotalWorkers : Environment.ProcessorCount + 4;

    /// <summary>
    /// Gets the effective worker cap of the Default pool: <see cref="DefaultPoolMaxWorkers"/>,
    /// capped to <see cref="GetEffectiveMaxTotalWorkers"/>, which it matches when not set.
    /// </summary>
    /// <returns>The effective cap.</returns>
    public int GetEffectiveDefaultPoolMaxWorkers()
    {
        var maxTotalWorkers = GetEffectiveMaxTotalWorkers();
        return DefaultPoolMaxWorkers > 0 ? Math.Min(DefaultPoolMaxWorkers, maxTotalWorkers) : maxTotalWorkers;
    }
}
