using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.Server.Settings;

public class DatabaseSettings
{
    public string MySqliteDirectory { get; set; } = Path.Combine(Utils.ApplicationPath, "SQLite");

    public string DatabaseBackupDirectory { get; set; } =
        Path.Combine(Utils.ApplicationPath, "DatabaseBackup");

    [JsonIgnore] public string DefaultUserUsername { get; set; } = "Default";
    [JsonIgnore] public string DefaultUserPassword { get; set; } = string.Empty;

    /// <summary>
    /// Use Constants.DatabaseType
    /// </summary>
    public string Type { get; set; } = Constants.DatabaseType.Sqlite;


    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;

    public string SQLite_DatabaseFile
    {
        get => sqlite_file;
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
                sqlite_file = value;
                return;
            }

            var directory = Path.Combine(parts[..^1]);
            if (prefix != null)
            {
                directory = prefix + directory;
            }

            MySqliteDirectory = directory;
            sqlite_file = parts.LastOrDefault();
        }
    }

    [JsonIgnore] private string sqlite_file { get; set; } = "JMMServer.db3";
}
