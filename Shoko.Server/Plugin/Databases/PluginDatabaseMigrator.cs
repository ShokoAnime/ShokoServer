using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Databases;
using Shoko.Server.Server;

namespace Shoko.Server.Plugin.Databases;

/// <summary>
/// Brings every plugin database up to date during the late start, after the
/// core's database, copying each one before its first pending migration, and
/// then opens them to the plugins.
/// </summary>
/// <param name="databases">The registered plugin databases.</param>
/// <param name="server">The core's database server.</param>
/// <param name="applicationPaths">The server's directories.</param>
/// <param name="gate">The gate opened once every database is up to date.</param>
/// <param name="logger">The logger.</param>
internal sealed class PluginDatabaseMigrator(
    IEnumerable<IPluginDatabase> databases,
    PluginDatabaseServer server,
    IApplicationPaths applicationPaths,
    PluginDatabaseGate gate,
    ILogger<PluginDatabaseMigrator> logger
)
{
    /// <summary>
    /// How many copies taken before migrating are kept per database.
    /// </summary>
    public const int KeptBackups = 3;

    /// <summary>
    /// The name of the folder under the server's backup folder that holds the
    /// copies taken before migrating.
    /// </summary>
    public const string BackupFolderName = "plugin-databases";

    /// <summary>
    /// Where the copies taken before migrating go, under the server's backup
    /// folder: <c>plugin-databases/&lt;plugin-id&gt;</c>. Not <c>plugins</c>, as
    /// the backup folder falls back to the data folder, where that name is the
    /// folder the plugins are installed in.
    /// </summary>
    internal string BackupDirectory
    {
        get => _backupDirectory ??= GetBackupDirectory(DatabaseBackupLocation.GetDirectory(applicationPaths));
        init => _backupDirectory = value;
    }

    private string? _backupDirectory;

    /// <summary>
    /// The folder the copies taken before migrating go in.
    /// </summary>
    /// <param name="backupRoot">The server's backup folder, which may be the data folder itself.</param>
    /// <returns>The folder.</returns>
    internal static string GetBackupDirectory(string backupRoot)
        => Path.Join(backupRoot, BackupFolderName);

    /// <summary>
    /// Applies the pending migrations of every plugin database, in the order
    /// the plugins registered them, then opens the databases to the plugins.
    /// Stops at the first failure, leaving them all closed.
    /// </summary>
    /// <param name="reportProgress">Told of each copy and each migration, one line apiece; optional.</param>
    /// <exception cref="PluginDatabaseMigrationException">A database could not be reached, copied or migrated.</exception>
    public void MigrateAll(Action<string>? reportProgress = null)
    {
        var all = databases.ToList();
        foreach (var database in all)
            Describe(database);

        foreach (var database in all)
            Migrate(database, reportProgress);

        gate.Open();
    }

    /// <summary>
    /// Says where a database is kept when that is not the plain answer: a
    /// plugin without migrations for the core's server stays on SQLite, and a
    /// file left from when the core ran on SQLite is no longer read.
    /// </summary>
    /// <param name="database">The database.</param>
    private void Describe(IPluginDatabase database)
    {
        if (database.Target.Type is Constants.DatabaseType.SQLite)
        {
            if (server.Type is not Constants.DatabaseType.SQLite)
                logger.LogInformation(
                    "Database \"{Name}\" of plugin \"{Plugin}\" has no migrations for {Server}, so it stays in its own SQLite file at {File}.",
                    database.Name,
                    database.PluginName,
                    server.DisplayName,
                    database.FilePath
                );
            return;
        }

        logger.LogDebug(
            "Database \"{Name}\" of plugin \"{Plugin}\" is kept in the core's {Server} database, in the tables starting with \"{Prefix}\".",
            database.Name,
            database.PluginName,
            server.DisplayName,
            database.Target.Naming!.Prefix
        );
        if (File.Exists(database.FilePath))
            logger.LogWarning(
                "Database \"{Name}\" of plugin \"{Plugin}\" is kept in the core's {Server} database, so its SQLite file at {File}, from when the core ran on SQLite, is no longer used.",
                database.Name,
                database.PluginName,
                server.DisplayName,
                database.FilePath
            );
    }

    /// <summary>
    /// Applies the pending migrations of one database, one at a time so a
    /// failure names the migration.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <param name="reportProgress">Told of the copy and each migration, one line apiece; optional.</param>
    /// <exception cref="PluginDatabaseMigrationException">The database could not be copied or migrated.</exception>
    internal void Migrate(IPluginDatabase database, Action<string>? reportProgress = null)
    {
        IReadOnlyList<string> pending;
        string? backupFile = null;
        try
        {
            pending = database.GetPendingMigrations();
            if (pending.Count is 0)
            {
                logger.LogDebug("Database \"{Name}\" of plugin \"{Plugin}\" is up to date.", database.Name, database.PluginName);
                return;
            }

            backupFile = Backup(database, reportProgress);
        }
        catch (Exception ex)
        {
            throw new PluginDatabaseMigrationException(database, null, null, ex);
        }

        if (backupFile is null)
            logger.LogInformation(
                "Applying {Count} migrations to database \"{Name}\" of plugin \"{Plugin}\", without a copy: it is new, or on SQL Server, where each migration runs in a transaction.",
                pending.Count,
                database.Name,
                database.PluginName
            );
        else
            logger.LogInformation(
                "Applying {Count} migrations to database \"{Name}\" of plugin \"{Plugin}\", copied first to {Backup}.",
                pending.Count,
                database.Name,
                database.PluginName,
                backupFile
            );
        for (var index = 0; index < pending.Count; index++)
        {
            var migration = pending[index];
            reportProgress?.Invoke($"Migrating plugin database {database.PluginName}/{database.Name}: {migration} ({index + 1}/{pending.Count})...");
            try
            {
                database.Migrate(migration);
            }
            catch (Exception ex)
            {
                throw new PluginDatabaseMigrationException(database, migration, backupFile, ex);
            }

            logger.LogDebug("Applied migration {Migration} to database \"{Name}\" of plugin \"{Plugin}\".", migration, database.Name, database.PluginName);
        }
    }

    /// <summary>
    /// Copies a database before migrating it: a SQLite file through SQLite's
    /// backup API, a plugin's MySQL tables as a SQL dump. SQL Server runs each
    /// migration in a transaction and is backed up with the core's database,
    /// so it is not copied, and neither is a database with nothing in it yet.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <param name="reportProgress">Told of the copy before it is taken; optional.</param>
    /// <returns>The copy, or <see langword="null"/> when none was taken.</returns>
    private string? Backup(IPluginDatabase database, Action<string>? reportProgress)
    {
        var directory = Path.Join(BackupDirectory, database.PluginID.ToString());
        var progress = $"Backing up plugin database {database.PluginName}/{database.Name} before migrating it...";
        switch (database.Target.Type)
        {
            case Constants.DatabaseType.MySQL:
                var prefix = database.Target.Naming!.Prefix;
                if (server.GetTables(prefix).Count is 0)
                    return null;

                reportProgress?.Invoke(progress);
                return PluginDatabaseBackups.Backup(directory, database.Name, ".sql", DateTime.Now, KeptBackups, file => server.DumpTables(prefix, file));
            case Constants.DatabaseType.SQLServer:
                return null;
            default:
                if (!File.Exists(database.FilePath))
                    return null;

                reportProgress?.Invoke(progress);
                return PluginDatabaseBackups.Backup(database.FilePath, directory, database.Name, DateTime.Now, KeptBackups);
        }
    }
}
