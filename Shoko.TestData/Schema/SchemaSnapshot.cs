using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace Shoko.TestData.Schema;

/// <summary>
/// One column as the database itself reports it, reduced to the parts that are meaningful across
/// all three backends.
/// </summary>
/// <param name="Name">Column name, as declared.</param>
/// <param name="Family">
/// The backend-neutral type family — see <see cref="SchemaSnapshot.FamilyOf"/>. Each backend spells
/// the same intent differently (<c>INTEGER</c>/<c>int</c>, <c>text</c>/<c>nvarchar(max)</c>), so the
/// dialect name itself cannot be compared.
/// </param>
/// <param name="Size">
/// <c>"500"</c> for a bounded string, <c>"6,2"</c> for a decimal, <c>"max"</c> for unbounded text, or
/// <see langword="null"/> when the backend declares no size at all. SQLite uses type affinity rather
/// than declared widths, so most of its columns report <see langword="null"/> here.
/// </param>
/// <param name="Nullable">Whether the column accepts nulls.</param>
/// <param name="PrimaryKey">Whether the column takes part in the primary key.</param>
public sealed record ColumnSnapshot(string Name, string Family, string? Size, bool Nullable, bool PrimaryKey);

/// <summary>
/// The live schema of a migrated database, read from the backend's own catalog.
/// </summary>
/// <remarks>
/// Read from the catalog rather than replayed from the DDL in <c>Shoko.Server/Databases/</c>: MySQL
/// performs some migrations through <c>PREPARE stmt FROM @sqlstmt</c>, and every backend has
/// <see cref="Shoko.Server.Databases.DatabaseCommandType.CodedCommand"/> migrations written in C#.
/// Neither is visible to a text replay, so only the migrated database knows the real answer.
/// </remarks>
public sealed class SchemaSnapshot
{
    private const string Sqlite = "SQLite";


    public SortedDictionary<string, SortedDictionary<string, ColumnSnapshot>> Tables { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Tables Shoko does not own, and which therefore have no cross-backend meaning.</summary>
    private static readonly string[] _ignoredTables = ["sysdiagrams", "database_firewall_rules", "trace_xe_action_map", "trace_xe_event_map"];

    public static SchemaSnapshot Read(IDbConnection connection, string backend)
    {
        var snapshot = new SchemaSnapshot();
        foreach (var (table, column, type, size, nullable, primaryKey) in Rows(connection, backend))
        {
            if (_ignoredTables.Contains(table, StringComparer.OrdinalIgnoreCase))
                continue;

            if (!snapshot.Tables.TryGetValue(table, out var columns))
                snapshot.Tables[table] = columns = new SortedDictionary<string, ColumnSnapshot>(StringComparer.OrdinalIgnoreCase);

            // A key column is never nullable in practice, whatever the catalog says: SQLite reports
            // `INTEGER PRIMARY KEY` as nullable because it is a rowid alias, which would otherwise
            // make every table's identity column look like a divergence.
            columns[column] = new ColumnSnapshot(column, FamilyOf(type), size, nullable && !primaryKey, primaryKey);
        }

        return snapshot;
    }

    private static IEnumerable<(string Table, string Column, string Type, string? Size, bool Nullable, bool PrimaryKey)> Rows(IDbConnection connection, string backend)
        => backend switch
        {
            "SQLite" => SqliteRows(connection),
            "MySQL" => CatalogRows(connection, MySqlQuery),
            "SQLServer" => CatalogRows(connection, SqlServerQuery),
            _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, "Unknown backend."),
        };

    private const string MySqlQuery = """
        SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE, IS_NULLABLE,
               CASE WHEN COLUMN_KEY = 'PRI' THEN 1 ELSE 0 END
        FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
        """;

    private const string SqlServerQuery = """
        SELECT c.TABLE_NAME, c.COLUMN_NAME, c.DATA_TYPE, c.CHARACTER_MAXIMUM_LENGTH, c.NUMERIC_PRECISION, c.NUMERIC_SCALE, c.IS_NULLABLE,
               CASE WHEN k.COLUMN_NAME IS NULL THEN 0 ELSE 1 END
        FROM INFORMATION_SCHEMA.COLUMNS c
        JOIN INFORMATION_SCHEMA.TABLES t ON t.TABLE_NAME = c.TABLE_NAME AND t.TABLE_SCHEMA = c.TABLE_SCHEMA
        LEFT JOIN (
            SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
            JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
              ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME AND tc.CONSTRAINT_SCHEMA = ku.CONSTRAINT_SCHEMA
            WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
        ) k ON k.TABLE_SCHEMA = c.TABLE_SCHEMA AND k.TABLE_NAME = c.TABLE_NAME AND k.COLUMN_NAME = c.COLUMN_NAME
        WHERE t.TABLE_TYPE = 'BASE TABLE'
        """;

    private static IEnumerable<(string, string, string, string?, bool, bool)> CatalogRows(IDbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var type = reader.GetString(2);
            var length = reader.IsDBNull(3) ? (long?)null : Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture);
            var precision = reader.IsDBNull(4) ? (int?)null : Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture);
            var scale = reader.IsDBNull(5) ? (int?)null : Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture);

            yield return (reader.GetString(0), reader.GetString(1), type, SizeOf(type, length, precision, scale),
                reader.GetString(6).Equals("YES", StringComparison.OrdinalIgnoreCase),
                Convert.ToInt32(reader.GetValue(7), CultureInfo.InvariantCulture) == 1);
        }
    }

    private static IEnumerable<(string, string, string, string?, bool, bool)> SqliteRows(IDbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT m.name, p.name, p.type, p."notnull", p.pk
            FROM sqlite_master m JOIN pragma_table_info(m.name) p
            WHERE m.type = 'table' AND m.name NOT LIKE 'sqlite_%'
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            // SQLite stores the declared type verbatim ("TEXT", "nvarchar(128)"), so the size, where
            // one was declared at all, has to come out of that string.
            var declared = reader.GetString(2);
            var open = declared.IndexOf('(');
            var type = open < 0 ? declared : declared[..open];
            var size = open < 0 ? null : declared[(open + 1)..].TrimEnd(')').Replace(" ", string.Empty);

            yield return (reader.GetString(0), reader.GetString(1), type.Trim(), Normalize(size),
                Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture) == 0,
                Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture) > 0);
        }
    }

    private static string? SizeOf(string type, long? length, int? precision, int? scale)
    {
        if (FamilyOf(type) is "decimal")
            return precision is null ? null : $"{precision},{scale ?? 0}";

        if (length is null)
            return null;

        // SQL Server reports -1 for the MAX types; MySQL gives `text` and friends their true byte
        // ceiling. Both mean "unbounded" and neither is a width anyone chose.
        return length < 0 || length >= 65535 ? "max" : length.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static string? Normalize(string? size)
        => size is null ? null : size.Equals("max", StringComparison.OrdinalIgnoreCase) ? "max" : size;

    /// <summary>
    /// Reduces a dialect type name to a backend-neutral family.
    /// </summary>
    /// <remarks>
    /// Grouped by what the column is for, not by storage: SQLite has no boolean or GUID type and
    /// spells every integer <c>INTEGER</c>, and MySQL has no GUID type either, so <c>bit</c>,
    /// <c>tinyint</c> and <c>uniqueidentifier</c> cannot be families of their own without every
    /// SQLite column being reported as a divergence.
    /// </remarks>
    public static string FamilyOf(string type) => type.Trim().ToLowerInvariant() switch
    {
        "int" or "integer" or "smallint" or "mediumint" or "tinyint" or "bit" or "bool" or "boolean" => "integer",
        "bigint" => "bigint",
        "text" or "varchar" or "nvarchar" or "char" or "nchar" or "longtext" or "mediumtext" or "tinytext" or "ntext" or "uniqueidentifier" => "text",
        "date" => "date",
        "datetime" or "datetime2" or "smalldatetime" or "timestamp" => "datetime",
        "time" => "time",
        "decimal" or "numeric" or "real" or "float" or "double" or "money" => "decimal",
        "blob" or "longblob" or "mediumblob" or "varbinary" or "binary" or "image" => "binary",
        var other => other,
    };

    /// <summary>
    /// Families SQLite has no separate type for, and the one it uses instead.
    /// </summary>
    /// <remarks>
    /// SQLite stores everything as one of five storage classes, and its <c>INTEGER</c> is already a
    /// variable-width signed 64-bit value, so there is no <c>BIGINT</c> for it to declare — asking it
    /// for one would be asking for a type that does not exist.
    /// </remarks>
    private static readonly Dictionary<string, string> _sqliteCannotDistinguish = new(StringComparer.Ordinal)
    {
        ["bigint"] = "integer",
    };

    /// <summary>
    /// Whether the type families observed for one column across the backends are the same type.
    /// </summary>
    /// <remarks>
    /// The backends that have the full type system are held to each other exactly, so a column that
    /// is <c>INT</c> on one and <c>BIGINT</c> on the other is still a divergence. Only SQLite is
    /// compared after collapsing the families it cannot express.
    /// </remarks>
    public static bool FamiliesAgree(IReadOnlyDictionary<string, string> observed)
    {
        var precise = observed.Where(entry => entry.Key is not Sqlite).Select(entry => entry.Value).Distinct().ToArray();
        if (precise.Length > 1)
            return false;

        if (!observed.TryGetValue(Sqlite, out var sqlite) || precise.Length is 0)
            return true;

        return AsSqliteWouldDeclareIt(precise[0]) == AsSqliteWouldDeclareIt(sqlite);
    }

    private static string AsSqliteWouldDeclareIt(string family)
        => _sqliteCannotDistinguish.TryGetValue(family, out var collapsed) ? collapsed : family;
}
