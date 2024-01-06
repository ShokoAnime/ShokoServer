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
}
