using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions.Config.Attributes;
using Shoko.Plugin.Abstractions.Config.Enums;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.Server.Settings;

public class DatabaseSettings
{
    [JsonIgnore]
    public string DefaultUserUsername { get; set; } = "Default";

    [JsonIgnore]
    public string DefaultUserPassword { get; set; } = string.Empty;

    /// <summary>
    /// Determines the database backend to use for everything besides Quartz.
    /// </summary>
    [Display(Name = "Database Type")]
    [RequiresRestart]
    [EnvironmentVariable("DB_TYPE")]
    [Required]
    public Constants.DatabaseType Type { get; set; } = Constants.DatabaseType.SQLite;

    [JsonIgnore]
    private string _sqliteFile = "ShokoServer.db3";

    /// <summary>
    /// File name of the SQLite database file.
    /// </summary>
    [Visibility(
        Visibility = DisplayVisibility.Hidden,
        Size = DisplayElementSize.Large,
        ToggleWhenMemberIsSet = nameof(Type),
        ToggleWhenSetTo = Constants.DatabaseType.SQLite,
        ToggleVisibilityTo = DisplayVisibility.Visible
    )]
    [Display(Name = "Filename")]
    [RequiresRestart]
    [EnvironmentVariable("DB_SQLITE_FILENAME")]
    [RegularExpression(@".+\.(?:db3?|sqlite3?)$")]
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

    /// <summary>
    /// Directory where the SQLite database file is stored.
    /// </summary>
    [Visibility(
        Visibility = DisplayVisibility.Hidden,
        Size = DisplayElementSize.Full,
        ToggleWhenMemberIsSet = nameof(Type),
        ToggleWhenSetTo = Constants.DatabaseType.SQLite,
        ToggleVisibilityTo = DisplayVisibility.Visible
    )]
    [Display(Name = "Directory")]
    [RequiresRestart]
    [EnvironmentVariable("DB_SQLITE_DIRECTORY")]
    public string MySqliteDirectory { get; set; } = Path.Combine(Utils.ApplicationPath, "SQLite");

    /// <summary>
    /// SQL Server or MySQL/MariaDB host address, optionally with port if it's
    /// not the default port for the database type.
    /// </summary>
    [Visibility(
        Visibility = DisplayVisibility.Visible,
        Size = DisplayElementSize.Large,
        ToggleWhenMemberIsSet = nameof(Type),
        ToggleWhenSetTo = Constants.DatabaseType.SQLite,
        ToggleVisibilityTo = DisplayVisibility.Hidden
    )]
    [RequiresRestart]
    [EnvironmentVariable("DB_HOST")]
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// SQL Server or MySQL/MariaDB username.
    /// </summary>
    [Visibility(
        Visibility = DisplayVisibility.Visible,
        Size = DisplayElementSize.Large,
        ToggleWhenMemberIsSet = nameof(Type),
        ToggleWhenSetTo = Constants.DatabaseType.SQLite,
        ToggleVisibilityTo = DisplayVisibility.Hidden
    )]
    [RequiresRestart]
    [EnvironmentVariable("DB_USER")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// SQL Server or MySQL/MariaDB password.
    /// </summary>
    [Visibility(
        Visibility = DisplayVisibility.Visible,
        Size = DisplayElementSize.Large,
        ToggleWhenMemberIsSet = nameof(Type),
        ToggleWhenSetTo = Constants.DatabaseType.SQLite,
        ToggleVisibilityTo = DisplayVisibility.Hidden
    )]
    [RequiresRestart]
    [EnvironmentVariable("DB_PASS")]
    [PasswordPropertyText]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// SQL Server or MySQL/MariaDB database name.
    /// </summary>
    [Visibility(
        Visibility = DisplayVisibility.Visible,
        Size = DisplayElementSize.Large,
        ToggleWhenMemberIsSet = nameof(Type),
        ToggleWhenSetTo = Constants.DatabaseType.SQLite,
        ToggleVisibilityTo = DisplayVisibility.Hidden
    )]
    [Display(Name = "Database Name")]
    [RequiresRestart]
    [EnvironmentVariable("DB_NAME")]
    public string Schema { get; set; } = string.Empty;

    /// <summary>
    /// Advanced SQL Server or MySQL/MariaDB connection string.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(
        Visibility = DisplayVisibility.Visible,
        Advanced = true,
        ToggleWhenMemberIsSet = nameof(Type),
        ToggleWhenSetTo = Constants.DatabaseType.SQLite,
        ToggleVisibilityTo = DisplayVisibility.Hidden
    )]
    [Display(Name = "Connection String")]
    [RequiresRestart]
    [EnvironmentVariable("DB_CONNECTION_STRING")]
    [TextArea]
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
                Constants.DatabaseType.SQLServer => 1433,
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

    /// <summary>
    /// Directory for where to store the backups during database migrations.
    /// </summary>
    [Display(Name = "Backup Directory")]
    [Visibility(
        Size = DisplayElementSize.Full
    )]
    [RequiresRestart]
    [EnvironmentVariable("DB_BACKUP_DIRECTORY")]
    public string DatabaseBackupDirectory { get; set; } = Path.Combine(Utils.ApplicationPath, "DatabaseBackup");

    /// <summary>
    /// Use database locking in the application. This should be left on if
    /// you're using SQLite, but can safely be turned off for the other two.
    /// </summary>
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Visibility(
        Visibility = DisplayVisibility.Visible,
        Advanced = true,
        ToggleWhenMemberIsSet = nameof(Type),
        ToggleWhenSetTo = Constants.DatabaseType.SQLite,
        ToggleVisibilityTo = DisplayVisibility.Disabled
    )]
    [Display(Name = "Use Application Database Locking")]
    [RequiresRestart]
    [EnvironmentVariable("DB_USE_APPLICATION_LOCK")]
    public bool UseDatabaseLock { get; set; } = true;

    /// <summary>
    /// Log SQL statements to standard output. They will not appear in the log file or Web UI live log.
    /// </summary>
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Visibility(Advanced = true)]
    [Display(Name = "Log SQL to Console")]
    [RequiresRestart]
    [EnvironmentVariable("DB_LOG_TO_CONSOLE")]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
    public bool LogSqlInConsole { get; set; } = false;
}
