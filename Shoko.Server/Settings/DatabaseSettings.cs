using System.IO;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions.Configuration;
using Shoko.Server.Server;

namespace Shoko.Server.Settings
{
    public class DatabaseSettings : IDefaultedConfig
    {
        public string MySqliteDirectory { get; set; } = Path.Combine(ServerSettings.ApplicationPath, "SQLite");
        public string DatabaseBackupDirectory { get; set; } = Path.Combine(ServerSettings.ApplicationPath, "DatabaseBackup");

        [JsonIgnore]
        public string DefaultUserUsername { get; set; } = "Default";
        [JsonIgnore]
        public string DefaultUserPassword { get; set; } = string.Empty;
        /// <summary>
        /// Use Constants.DatabaseType
        /// </summary>
        public string Type { get; set; } = Constants.DatabaseType.Sqlite;


        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Schema { get; set; } = string.Empty;
        public string Hostname { get; set; } = string.Empty;


        public string SQLite_DatabaseFile { get; set; } = "JMMServer.db3";
        public void SetDefaults()
        {
            
        }
    }
}