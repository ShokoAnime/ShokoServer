using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Shoko.Server.Databases;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// Compares the hand-written DDL of the three database backends against each other.
/// </summary>
/// <remarks>
/// Each backend carries its own copy of the schema as an ordered list of raw SQL statements, so the
/// three can drift apart without anything failing until a user on that backend hits it. Both
/// <c>add missing primary keys on SQL Server for 38 tables</c> and
/// <c>widen TMDB_Show/TMDB_Movie Genres column on MySQL and SQL Server</c> were exactly that, and
/// neither is reachable from a test that only exercises SQLite. This replays each backend's
/// statements into a logical schema and compares the results, without touching a database.
/// </remarks>
public class SchemaParityTests
{
    private static readonly string[] s_commandListFields =
        ["_createVersionTable", "_updateVersionTable", "_createTables", "_patchCommands"];

    private sealed class Schema
    {
        public Dictionary<string, bool> TablesWithPrimaryKey { get; } = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<string> Tables => TablesWithPrimaryKey.Keys;
    }

    private static readonly Regex s_createTable = new(
        @"CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?[\[`""]?(?<name>\w+)[\]`""]?", RegexOptions.IgnoreCase);

    private static readonly Regex s_dropTable = new(
        @"DROP\s+TABLE\s+(?:IF\s+EXISTS\s+)?[\[`""]?(?<name>\w+)[\]`""]?", RegexOptions.IgnoreCase);

    private static readonly Regex s_alterTable = new(
        @"ALTER\s+TABLE\s+[\[`""]?(?<name>\w+)[\]`""]?", RegexOptions.IgnoreCase);

    private static readonly Regex s_renameTo = new(
        @"ALTER\s+TABLE\s+[\[`""]?(?<from>\w+)[\]`""]?\s+RENAME\s+TO\s+[\[`""]?(?<to>\w+)[\]`""]?", RegexOptions.IgnoreCase);

    private static readonly Regex s_renameTable = new(
        @"RENAME\s+TABLE\s+[\[`""]?(?<from>\w+)[\]`""]?\s+TO\s+[\[`""]?(?<to>\w+)[\]`""]?", RegexOptions.IgnoreCase);

    private static readonly Regex s_spRename = new(
        @"sp_rename\s+'(?<from>[^']+)'\s*,\s*'(?<to>[^']+)'", RegexOptions.IgnoreCase);

    /// <summary>Replays a backend's DDL in order into a logical schema.</summary>
    private static Schema Build(IDatabase database)
    {
        var schema = new Schema();
        foreach (var statement in Statements(database))
        {
            if (s_spRename.Match(statement) is { Success: true } spRename)
            {
                Rename(schema, spRename.Groups["from"].Value, spRename.Groups["to"].Value);
                continue;
            }

            if (s_renameTable.Match(statement) is { Success: true } renameTable)
            {
                Rename(schema, renameTable.Groups["from"].Value, renameTable.Groups["to"].Value);
                continue;
            }

            if (s_renameTo.Match(statement) is { Success: true } renameTo)
            {
                Rename(schema, renameTo.Groups["from"].Value, renameTo.Groups["to"].Value);
                continue;
            }

            if (s_dropTable.Match(statement) is { Success: true } drop)
            {
                schema.TablesWithPrimaryKey.Remove(drop.Groups["name"].Value);
                continue;
            }

            if (s_createTable.Match(statement) is { Success: true } create)
            {
                schema.TablesWithPrimaryKey[create.Groups["name"].Value] =
                    statement.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (statement.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) &&
                s_alterTable.Match(statement) is { Success: true } alter &&
                schema.TablesWithPrimaryKey.ContainsKey(alter.Groups["name"].Value))
                schema.TablesWithPrimaryKey[alter.Groups["name"].Value] = true;
        }

        return schema;
    }

    private static void Rename(Schema schema, string from, string to)
    {
        if (!schema.TablesWithPrimaryKey.Remove(from, out var hadPrimaryKey))
            return;

        schema.TablesWithPrimaryKey[to] = hadPrimaryKey;
    }

    private static IEnumerable<string> Statements(IDatabase database)
        => s_commandListFields
            .Select(name => database.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic))
            .Where(field => field is not null)
            .SelectMany(field => (IEnumerable<DatabaseCommand>)field!.GetValue(database)!)
            .Where(command => command.Type is DatabaseCommandType.NormalCommand && command.Command is not null)
            .SelectMany(command => command.Command!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public static TheoryData<string> Backends() => new("SQLite", "MySQL", "SQLServer");

    private static IDatabase Instantiate(string backend)
    {
        // MySQL reads the settings singleton while initialising its DDL fields.
        StubSettingsProvider.Install();
        return Create(backend);
    }

    private static IDatabase Create(string backend) => backend switch
    {
        "SQLite" => new SQLite(null!),
        "MySQL" => new MySQL(null!),
        "SQLServer" => new SQLServer(null!),
        _ => throw new ArgumentOutOfRangeException(nameof(backend)),
    };

    #region Discovery

    [Theory]
    [MemberData(nameof(Backends))]
    public void TheDdlIsDiscovered(string backend)
    {
        // Guards against the private command lists being renamed, which would otherwise turn every
        // assertion below into a comparison of two empty schemas.
        var statements = Statements(Instantiate(backend)).ToArray();

        Assert.True(statements.Length > 100, $"{backend}: only found {statements.Length} statements.");
    }

    #endregion

    #region Parity

    /// <summary>
    /// SQLite drops this from a coded migration rather than plain SQL, so a static replay of the
    /// command lists cannot see it go. All three backends do drop it.
    /// </summary>
    private static readonly string[] s_droppedOutsideSql = ["Language"];

    private static HashSet<string> TablesOf(string backend)
        => Build(Instantiate(backend)).Tables
            .Except(s_droppedOutsideSql, StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    [Theory]
    [InlineData("MySQL")]
    [InlineData("SQLServer")]
    public void EveryBackendDefinesTheSameTablesAsSqlite(string backend)
    {
        var sqlite = TablesOf("SQLite");
        var other = TablesOf(backend);

        // Each backend keeps its own copy of the schema, so one can gain or lose a table without
        // anything failing until a user on that backend hits it.
        Assert.Equal(string.Empty, string.Join(", ", sqlite.Except(other).Order()));
        Assert.Equal(string.Empty, string.Join(", ", other.Except(sqlite).Order()));
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public void EveryTableDeclaresAPrimaryKey(string backend)
    {
        var missing = Build(Instantiate(backend)).TablesWithPrimaryKey
            .Where(entry => !entry.Value)
            .Select(entry => entry.Key)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // NHibernate needs an identifier for every mapped entity, and a table without a primary key
        // also silently permits duplicate rows.
        Assert.Equal(string.Empty, string.Join(", ", missing));
    }

    #endregion
}
