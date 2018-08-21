using System;
using System.IO;
using Newtonsoft.Json;
using Shoko.Server.Databases;

namespace Shoko.Server.Settings
{
    public class DatabaseSettings
    {
        public string MySqliteDirectory { get; set; } = Path.Combine(ServerSettings.ApplicationPath, "SQLite");
        public string DatabaseBackupDirectory { get; set; } = Path.Combine(ServerSettings.ApplicationPath, "DatabaseBackup");

        [JsonIgnore]
        public string DefaultUserUsername { get; set; } = "Default";
        [JsonIgnore]
        public string DefaultUserPassword { get; set; } = string.Empty;
        public DatabaseTypes Type { get; set; } = DatabaseTypes.Sqlite;


        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Schema { get; set; } = string.Empty;
        public string Hostname { get; set; } = string.Empty;


        public string SQLite_DatabaseFile { get; set; } = "JMMServer.db3";
    }
}