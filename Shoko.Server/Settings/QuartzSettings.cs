using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Shoko.Plugin.Abstractions.Config.Attributes;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.Server.Settings;

public class QuartzSettings
{
    /// <summary>
    /// Determines the database backend to use for Quartz.
    /// </summary>
    public Constants.DatabaseType DatabaseType { get; set; } = Constants.DatabaseType.SQLite;

    /// <summary>
    /// The connection string for the database
    /// </summary>
    [TextArea]
    public string ConnectionString { get; set; } = $"Data Source={Path.Combine(Utils.ApplicationPath, "SQLite", "Quartz.db3")};Mode=ReadWriteCreate;Pooling=True";

    /// <summary>
    /// Set this value to override the default size of the queue thread pool.
    /// </summary>
    [Range(-1, int.MaxValue)]
    public int MaxThreadPoolSize { get; set; }

    /// <summary>
    /// The number of waiting jobs to cache for API usage.
    /// </summary>
    [Range(0, 1000)]
    public int WaitingCacheSize { get; set; } = 100;

    /// <summary>
    /// A map of Type (yes, you need to look at the source code, under ./Shoko.Server/Scheduling/Jobs) to the number of allowed concurrent jobs of the same type.
    /// Some types will not be able to have a lower limit, due to API restrictions. HashFileJob is included as an example.
    /// </summary>
    public Dictionary<string, int> LimitedConcurrencyOverrides { get; set; } = new()
    {
        { nameof(HashFileJob), 2 },
    };
}
