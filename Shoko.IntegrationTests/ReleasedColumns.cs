using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Shoko.Server.Databases;
using Shoko.TestData.Schema;

namespace Shoko.IntegrationTests;

/// <summary>
/// Puts back text columns rows kept before the text store took their texts, as a database upgraded
/// from a released version has them until the drop steps run, and drops them again.
/// </summary>
/// <param name="targets">What the drop steps remove, in the order they run.</param>
internal sealed class ReleasedColumns(IReadOnlyList<(string Table, string Column)> targets)
{
    #region Constants

    /// <summary>
    /// The name and description columns of the orderings and their groups.
    /// </summary>
    public static readonly ReleasedColumns Ordering = new(
    [
        ("Metadata_Ordering", "Name"),
        ("Metadata_Ordering", "Description"),
        ("Metadata_Ordering_Group", "Name"),
        ("Metadata_Ordering_Group", "Description"),
    ]);

    /// <summary>
    /// The name, original name and description columns of the creators and characters.
    /// </summary>
    public static readonly ReleasedColumns People = new(
    [
        ("Metadata_Creator", "Name"),
        ("Metadata_Creator", "OriginalName"),
        ("Metadata_Creator", "Description"),
        ("Metadata_Character", "Name"),
        ("Metadata_Character", "OriginalName"),
        ("Metadata_Character", "Description"),
    ]);

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds back the columns that are missing. They take <c>null</c>, so the core's own writes need
    /// not fill them.
    /// </summary>
    /// <param name="connection">An open connection to the migrated database.</param>
    /// <returns>A step for the copy step lists, which always succeeds.</returns>
    public Tuple<bool, string?> Restore(object connection)
    {
        var dbConnection = (IDbConnection)connection;
        var (backend, text, addColumn) = Backend(dbConnection) switch
        {
            "SQLServer" => ("SQLServer", "NVARCHAR(MAX)", "ADD"),
            "MySQL" => ("MySQL", "TEXT", "ADD COLUMN"),
            _ => ("SQLite", "TEXT", "ADD COLUMN"),
        };
        foreach (var (table, column) in Columns(dbConnection, backend, present: false))
            Sql.Execute(dbConnection, $"ALTER TABLE {table} {addColumn} {column} {text} NULL");
        return new(true, null);
    }

    /// <summary>
    /// Drops the columns that are there, by plain SQL.
    /// </summary>
    /// <param name="connection">An open connection to the migrated database.</param>
    /// <returns>A step for the copy step lists, which always succeeds.</returns>
    public Tuple<bool, string?> Drop(object connection)
    {
        var dbConnection = (IDbConnection)connection;
        foreach (var (table, column) in Columns(dbConnection, Backend(dbConnection), present: true))
            Sql.Execute(dbConnection, $"ALTER TABLE {table} DROP COLUMN {column}");
        return new(true, null);
    }

    /// <summary>
    /// The backend's own drop steps, one per target, in the same order.
    /// </summary>
    /// <param name="fixture">The started server.</param>
    /// <returns>One step per target.</returns>
    public IReadOnlyList<DatabaseCommand> DropSteps(DatabaseMigrationFixture fixture)
        => [.. targets.Select(target => SchemaSteps.Find(fixture, $"ALTER TABLE {target.Table} DROP COLUMN {target.Column};"))];

    /// <summary>
    /// Whether the database has any of the columns.
    /// </summary>
    /// <param name="connection">An open connection to the migrated database.</param>
    /// <returns><c>true</c> when one is there.</returns>
    public bool AnyPresent(IDbConnection connection)
        => Columns(connection, Backend(connection), present: true).Any();

    #endregion

    #region Helpers

    /// <summary>
    /// The backend a connection is to.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <returns>The backend, as <see cref="DatabaseMigrationFixture.Backend"/> names it.</returns>
    /// <exception cref="NotSupportedException">The connection is to no supported backend.</exception>
    private static string Backend(IDbConnection connection)
        => connection switch
        {
            SqlConnection => "SQLServer",
            MySqlConnection => "MySQL",
            SqliteConnection => "SQLite",
            _ => throw new NotSupportedException($"Unknown database connection {connection.GetType().Name}."),
        };

    /// <summary>
    /// The targets the database has, or the ones it lacks.
    /// </summary>
    /// <param name="connection">An open connection to the migrated database.</param>
    /// <param name="backend">The backend, as <see cref="DatabaseMigrationFixture.Backend"/> names it.</param>
    /// <param name="present">Whether to name the columns that are there rather than the missing ones.</param>
    /// <returns>The columns, by table.</returns>
    private IEnumerable<(string Table, string Column)> Columns(IDbConnection connection, string backend, bool present)
    {
        var schema = SchemaSnapshot.Read(connection, backend);
        return targets.Where(target => schema.Tables.TryGetValue(target.Table, out var columns) && columns.ContainsKey(target.Column) == present).ToList();
    }

    #endregion
}
