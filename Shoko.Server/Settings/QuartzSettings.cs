using System.Collections.Generic;
using System.IO;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.Server.Settings;

public class QuartzSettings
{
    /// <summary>
    /// Use <see cref="Constants.DatabaseType" />
    /// </summary>
    public string DatabaseType { get; set; } = Constants.DatabaseType.Sqlite;

    /// <summary>
    /// The connection string for the database
    /// </summary>
    public string ConnectionString { get; set; } = $"Data Source={Path.Combine(Utils.ApplicationPath, "SQLite", "Quartz.db3")};Mode=ReadWriteCreate;";

    /// <summary>
    /// Set this value to override the default size of the queue thread pool
    /// </summary>
    public int MaxThreadPoolSize { get; set; }

    /// <summary>
    /// A map of Type (yes, you need to look at the source code, under ./Shoko.Server/Scheduling/Jobs) to the number of allowed concurrent jobs of the same type.
    /// Some types will not be able to have a lower limit, due to API restrictions. HashFileJob is included as an example.
    /// </summary>
    public Dictionary<string, int> LimitedConcurrencyOverrides { get; set; } = new()
    {
        {
            "HashFileJob", 2
        }
    };
}
