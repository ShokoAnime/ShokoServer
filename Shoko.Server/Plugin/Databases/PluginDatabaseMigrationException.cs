using System;
using System.Text;
using Shoko.Server.Server;

namespace Shoko.Server.Plugin.Databases;

/// <summary>
/// A plugin database could not be brought up to date, which stops the
/// server's start-up. The message names the plugin, the context and the
/// migration, and says how to recover.
/// </summary>
internal sealed class PluginDatabaseMigrationException : Exception
{
    /// <summary>
    /// Creates the exception for a database that failed.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <param name="migration">The migration that failed, or <c>null</c> when none had started.</param>
    /// <param name="backupFile">The copy taken before migrating, or <c>null</c> when none was taken.</param>
    /// <param name="innerException">What went wrong.</param>
    public PluginDatabaseMigrationException(IPluginDatabase database, string? migration, string? backupFile, Exception innerException)
        : base(Describe(database, migration, backupFile, innerException), innerException)
    {
        PluginID = database.PluginID;
        PluginName = database.PluginName;
        DatabaseName = database.Name;
        ContextType = database.ContextType;
        Migration = migration;
        DatabaseType = database.Target.Type;
        DatabaseFile = database.Target.Type is Constants.DatabaseType.SQLite ? database.FilePath : null;
        TablePrefix = database.Target.Naming?.Prefix;
        BackupFile = backupFile;
    }

    /// <summary>
    /// The plugin's ID.
    /// </summary>
    public Guid PluginID { get; }

    /// <summary>
    /// The plugin's name.
    /// </summary>
    public string PluginName { get; }

    /// <summary>
    /// The database's name within the plugin.
    /// </summary>
    public string DatabaseName { get; }

    /// <summary>
    /// The context type.
    /// </summary>
    public Type ContextType { get; }

    /// <summary>
    /// The migration that failed, if one had started.
    /// </summary>
    public string? Migration { get; }

    /// <summary>
    /// The backend the database is kept on.
    /// </summary>
    public Constants.DatabaseType DatabaseType { get; }

    /// <summary>
    /// The database file, when it is kept in one.
    /// </summary>
    public string? DatabaseFile { get; }

    /// <summary>
    /// The prefix of its tables, when it is kept in the core's database.
    /// </summary>
    public string? TablePrefix { get; }

    /// <summary>
    /// The copy taken before migrating, if there was one.
    /// </summary>
    public string? BackupFile { get; }

    private static string Describe(IPluginDatabase database, string? migration, string? backupFile, Exception innerException)
    {
        var message = new StringBuilder();
        message.Append($"Plugin \"{database.PluginName}\" ({database.PluginID}) ");
        message.Append(migration is null
            ? $"could not prepare its database \"{database.Name}\" ({database.ContextType.Name}) for migrating"
            : $"could not apply the migration \"{migration}\" to its database \"{database.Name}\" ({database.ContextType.Name})");
        message.Append($": {innerException.Message.TrimEnd('.')}.");
        if (database.Target is { Type: not Constants.DatabaseType.SQLite, Naming: { } naming })
            DescribeServerRecovery(message, database.Target.Type, naming.Prefix, migration, backupFile);
        else if (backupFile is not null)
            message.Append($" To go back, stop the server and copy \"{backupFile}\", taken just before migrating, over \"{database.FilePath}\".");
        else if (migration is not null)
            message.Append($" The database was new, so deleting \"{database.FilePath}\" starts it over.");
        message.Append($" Or disable the plugin and restart: through the plugin API, which stays open while the start-up has failed, or by setting \"{database.PluginDllName}\" to false under \"Plugins.EnabledPlugins\" in settings-server.json.");
        return message.ToString();
    }

    private static void DescribeServerRecovery(StringBuilder message, Constants.DatabaseType type, string prefix, string? migration, string? backupFile)
    {
        var tables = $"the tables starting with \"{prefix}\" in the core's {PluginDatabaseServer.GetDisplayName(type)} database";
        if (migration is null)
            return;
        if (backupFile is not null)
            message.Append($" To go back, stop the server, drop {tables} and load \"{backupFile}\", taken just before migrating, into that database.");
        else if (type is Constants.DatabaseType.SQLServer)
            message.Append($" The failed migration was rolled back; to go back further, restore a backup of the core's database, or drop {tables} to start the plugin's database over.");
        else
            message.Append($" The database was new, so dropping {tables} starts it over.");
    }
}
