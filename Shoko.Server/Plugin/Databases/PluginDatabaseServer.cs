using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Shoko.Server.Databases;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Plugin.Databases;

/// <summary>
/// The core's database server, as the plugin databases see it: which backend
/// the core runs on, how to reach it, and the few things done to a plugin's
/// tables there outside Entity Framework Core.
/// </summary>
/// <remarks>
/// Read from the settings the first time it is asked, which is when the late
/// start migrates the plugin databases, after the core's own database is up
/// and after the first-run setup picked it. It is kept for the rest of the
/// run, so every plugin context and the migrator agree on where the databases
/// are even if the settings change before the next restart.
/// </remarks>
internal class PluginDatabaseServer
{
    private readonly Lazy<(Constants.DatabaseType Type, string ConnectionString)> _server;

    /// <summary>
    /// Creates the server from the core's database settings, as the container does.
    /// </summary>
    /// <param name="settingsProvider">The settings.</param>
    public PluginDatabaseServer(ISettingsProvider settingsProvider)
        => _server = new(() => FromSettings(settingsProvider.GetSettings().Database));

    /// <summary>
    /// Creates a server with a known backend.
    /// </summary>
    /// <param name="type">The backend.</param>
    /// <param name="connectionString">The connection string, empty for SQLite.</param>
    internal PluginDatabaseServer(Constants.DatabaseType type, string connectionString)
        => _server = new((type, connectionString));

    /// <summary>
    /// The backend the core runs on.
    /// </summary>
    public Constants.DatabaseType Type => _server.Value.Type;

    /// <summary>
    /// The connection string to the core's database; empty on SQLite.
    /// </summary>
    public string ConnectionString => _server.Value.ConnectionString;

    /// <summary>
    /// The backend's name, for messages.
    /// </summary>
    public string DisplayName => GetDisplayName(Type);

    #region Tables

    /// <summary>
    /// The tables in the core's database whose names start with a prefix.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <returns>The table names, as <c>schema.table</c> on SQL Server.</returns>
    /// <exception cref="NotSupportedException">The core runs on SQLite.</exception>
    public IReadOnlyList<string> GetTables(string prefix)
        => GetTables(Type, ConnectionString, prefix);

    /// <summary>
    /// Drops the tables in the core's database whose names start with a
    /// prefix, with the foreign keys between them.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <returns>The tables dropped.</returns>
    /// <exception cref="NotSupportedException">The core runs on SQLite.</exception>
    public IReadOnlyList<string> DropTables(string prefix)
        => DropTables(Type, ConnectionString, prefix);

    /// <summary>
    /// Writes the tables whose names start with a prefix, structure and rows,
    /// to a SQL file that recreates them. MySQL only.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <param name="file">The file.</param>
    /// <returns>The tables written.</returns>
    /// <exception cref="NotSupportedException">The core does not run on MySQL.</exception>
    public IReadOnlyList<string> DumpTables(string prefix, string file)
    {
        if (Type is not Constants.DatabaseType.MySQL)
            throw new NotSupportedException($"Tables are only dumped on MySQL, not on {DisplayName}.");

        var tables = GetTables(prefix);
        using var connection = new MySqlConnection(ConnectionString);
        using var command = connection.CreateCommand();
        using var backup = new MySqlBackup(command);
        command.CommandTimeout = 0;
        connection.Open();
        backup.ExportInfo.TablesToBeExportedList = tables.ToList();
        backup.ExportInfo.AddCreateDatabase = false;
        backup.ExportInfo.AddDropTable = true;
        backup.ExportInfo.ExportViews = false;
        backup.ExportInfo.ExportTriggers = false;
        backup.ExportInfo.ExportEvents = false;
        backup.ExportInfo.ExportProcedures = false;
        backup.ExportInfo.ExportFunctions = false;
        backup.ExportToFile(file);
        return tables;
    }

    /// <summary>
    /// The tables in a database whose names start with a prefix.
    /// </summary>
    /// <param name="type">The backend.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="prefix">The prefix.</param>
    /// <returns>The table names, as <c>schema.table</c> on SQL Server.</returns>
    /// <exception cref="NotSupportedException"><paramref name="type"/> is SQLite.</exception>
    internal static IReadOnlyList<string> GetTables(Constants.DatabaseType type, string connectionString, string prefix)
    {
        switch (type)
        {
            case Constants.DatabaseType.MySQL:
            {
                using var connection = new MySqlConnection(connectionString);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'";
                return ReadNames(command.ExecuteReader(), prefix, reader => reader.GetString(0));
            }
            case Constants.DatabaseType.SQLServer:
            {
                using var connection = new SqlConnection(connectionString);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT SCHEMA_NAME(schema_id), name FROM sys.tables";
                return ReadNames(command.ExecuteReader(), prefix, reader => $"{reader.GetString(0)}.{reader.GetString(1)}", reader => reader.GetString(1));
            }
            default:
                throw new NotSupportedException($"Plugin tables are not kept in the core's database on {GetDisplayName(type)}.");
        }
    }

    /// <summary>
    /// Drops the tables in a database whose names start with a prefix, with
    /// the foreign keys between them.
    /// </summary>
    /// <param name="type">The backend.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="prefix">The prefix.</param>
    /// <returns>The tables dropped.</returns>
    /// <exception cref="NotSupportedException"><paramref name="type"/> is SQLite.</exception>
    internal static IReadOnlyList<string> DropTables(Constants.DatabaseType type, string connectionString, string prefix)
    {
        var tables = GetTables(type, connectionString, prefix);
        if (tables.Count is 0)
            return tables;

        switch (type)
        {
            case Constants.DatabaseType.MySQL:
            {
                using var connection = new MySqlConnection(connectionString);
                connection.Open();
                Execute(connection, "SET FOREIGN_KEY_CHECKS = 0");
                try
                {
                    Execute(connection, $"DROP TABLE IF EXISTS {string.Join(", ", tables.Select(table => $"`{table.Replace("`", "``")}`"))}");
                }
                finally
                {
                    Execute(connection, "SET FOREIGN_KEY_CHECKS = 1");
                }

                break;
            }
            case Constants.DatabaseType.SQLServer:
            {
                using var connection = new SqlConnection(connectionString);
                connection.Open();
                var foreignKeys = new List<(string Schema, string Table, string Name)>();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT SCHEMA_NAME(t.schema_id), t.name, fk.name FROM sys.foreign_keys fk JOIN sys.tables t ON t.object_id = fk.parent_object_id";
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                        if (reader.GetString(1).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            foreignKeys.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
                }

                foreach (var (schema, table, name) in foreignKeys)
                    Execute(connection, $"ALTER TABLE {QuoteSqlServer(schema)}.{QuoteSqlServer(table)} DROP CONSTRAINT {QuoteSqlServer(name)}");
                foreach (var table in tables)
                {
                    var dot = table.IndexOf('.');
                    Execute(connection, $"DROP TABLE {QuoteSqlServer(table[..dot])}.{QuoteSqlServer(table[(dot + 1)..])}");
                }

                break;
            }
        }

        return tables;
    }

    private static List<string> ReadNames(System.Data.Common.DbDataReader reader, string prefix, Func<System.Data.Common.DbDataReader, string> name, Func<System.Data.Common.DbDataReader, string>? tableName = null)
    {
        using (reader)
        {
            var names = new List<string>();
            while (reader.Read())
                if ((tableName ?? name)(reader).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    names.Add(name(reader));
            names.Sort(StringComparer.Ordinal);
            return names;
        }
    }

    private static void Execute(System.Data.Common.DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string QuoteSqlServer(string identifier)
        => $"[{identifier.Replace("]", "]]")}]";

    #endregion

    #region Settings

    /// <summary>
    /// The backend and connection string the core's database settings name.
    /// </summary>
    /// <param name="settings">The database settings.</param>
    /// <returns>The backend, and the connection string, empty for SQLite.</returns>
    internal static (Constants.DatabaseType Type, string ConnectionString) FromSettings(DatabaseSettings settings)
        => settings.Type switch
        {
            Constants.DatabaseType.MySQL => (Constants.DatabaseType.MySQL, MySQL.GetConnectionString(settings)),
            Constants.DatabaseType.SQLServer => (Constants.DatabaseType.SQLServer, SQLServer.GetConnectionString(settings)),
            _ => (Constants.DatabaseType.SQLite, string.Empty),
        };

    /// <summary>
    /// A backend's name, for messages.
    /// </summary>
    /// <param name="type">The backend.</param>
    /// <returns>The name.</returns>
    internal static string GetDisplayName(Constants.DatabaseType type)
        => type switch
        {
            Constants.DatabaseType.MySQL => "MySQL",
            Constants.DatabaseType.SQLServer => "SQL Server",
            _ => "SQLite",
        };

    #endregion
}
