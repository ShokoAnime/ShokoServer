using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Shoko.Server.Server;
using Shoko.Server.Settings.Attributes;
using Shoko.Server.Utilities;

namespace Shoko.Server.Settings;

public class DatabaseSettings
{
    public string MySqliteDirectory { get; set; } = Path.Combine(Utils.ApplicationPath, "SQLite");

    public string DatabaseBackupDirectory { get; set; } = Path.Combine(Utils.ApplicationPath, "DatabaseBackup");

    [JsonIgnore]
    public string DefaultUserUsername { get; set; } = "Default";

    [JsonIgnore]
    public string DefaultUserPassword { get; set; } = string.Empty;

    /// <summary>
    /// Use Constants.DatabaseType
    /// </summary>
    [EnvironmentConfig("DB_TYPE")]
    public string Type { get; set; } = Constants.DatabaseType.Sqlite;

    public bool UseDatabaseLock { get; set; } = true;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Schema { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;
    
    [EnvironmentConfig("DB_CONNECTION_STRING")]
    public string OverrideConnectionString { get; set; } = string.Empty;

    [JsonIgnore]
    public int Port
    {
        get
        {
            var array = Host.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (array.Length >= 2 && int.TryParse(array.Last(), out var val))
                return val;

            return Type switch
            {
                Constants.DatabaseType.MySQL => 3306,
                Constants.DatabaseType.SqlServer => 1433,
                _ => 0,
            };
        }
    }

    [JsonIgnore]
    public string Hostname
    {
        get
        {
            return Host.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        }
    }

    public string SQLite_DatabaseFile
    {
        get => _sqliteFile;
        set
        {
            string prefix = null;
            if (value.StartsWith('/'))
            {
                prefix = "/";
            }
            else if (value.StartsWith("\\\\"))
            {
                prefix = "\\\\";
            }

            var parts = value.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 1)
            {
                _sqliteFile = value;
                return;
            }

            var directory = Path.Combine(parts[..^1]);
            if (prefix != null)
            {
                directory = prefix + directory;
            }

            MySqliteDirectory = directory;
            _sqliteFile = parts.LastOrDefault();
        }
    }

    [JsonIgnore]
    private string _sqliteFile = "JMMServer.db3";

    /// <summary>
    /// Log SQL in Console.
    /// </summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
    public bool LogSqlInConsole { get; set; } = false;
}
