using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.TestData.Schema;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// Holds the three supported backends to one schema: the same tables, the same columns, and the same
/// data type, width and nullability for each column.
/// </summary>
/// <remarks>
/// Each backend keeps its own hand-written DDL, so nothing forces the three to agree and no test on a
/// single backend can see it drift — <c>add missing primary keys on SQL Server for 38 tables</c> was
/// this. The schemas come from <c>Shoko.IntegrationTests</c> migrating a real database of each
/// backend; without all three there is nothing to compare and these skip. See
/// <c>Shoko.TestData/Schema/README.md</c> for running it locally.
///
/// SQLite declares no widths, so it takes no part in the width comparison.
/// </remarks>
public class SchemaTypeParityTests
{
    private const string Sqlite = "SQLite";

    /// <summary>Skips rather than passing when there is nothing to compare.</summary>
    private static void RequireDumps()
    {
        if (SchemaDumps.Unavailable() is { } reason)
            Assert.Skip($"{reason} Nothing to compare.");
    }

    public static TheoryData<string> Backends()
    {
        var data = new TheoryData<string>();
        foreach (var backend in SchemaDumps.Backends)
            data.Add(backend);

        return data;
    }

    #region Discovery

    [Theory]
    [MemberData(nameof(Backends))]
    public void EachSchemaDumpIsLoaded(string backend)
    {
        RequireDumps();
        // A dump that arrived near-empty would make every column below look agreed.
        var schema = SchemaDumps.For(backend);

        Assert.True(schema.Count > 60, $"{backend}: only {schema.Count} tables.");
        Assert.True(schema.Values.Sum(table => table.Count) > 600, $"{backend}: only {schema.Values.Sum(table => table.Count)} columns.");
    }

    #endregion

    #region Shape

    [Theory]
    [MemberData(nameof(Backends))]
    public void EveryBackendDefinesTheSameTables(string backend)
    {
        RequireDumps();
        var expected = SchemaDumps.For(Sqlite).Keys;
        var actual = SchemaDumps.For(backend).Keys;

        Report($"{backend} does not define the same tables as {Sqlite}",
            expected.Except(actual, StringComparer.OrdinalIgnoreCase).Select(table => $"{table}: missing")
                .Concat(actual.Except(expected, StringComparer.OrdinalIgnoreCase).Select(table => $"{table}: unexpected")));
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public void EveryBackendDefinesTheSameColumns(string backend)
    {
        RequireDumps();
        var missing = new List<string>();
        var extra = new List<string>();
        foreach (var (table, columns) in SchemaDumps.For(Sqlite))
        {
            if (!SchemaDumps.For(backend).TryGetValue(table, out var other))
                continue;

            missing.AddRange(columns.Keys.Except(other.Keys, StringComparer.OrdinalIgnoreCase).Select(column => $"{table}.{column}"));
            extra.AddRange(other.Keys.Except(columns.Keys, StringComparer.OrdinalIgnoreCase).Select(column => $"{table}.{column}"));
        }

        Report($"{backend} does not define the same columns as {Sqlite}",
            missing.Select(column => $"{column}: missing").Concat(extra.Select(column => $"{column}: unexpected")));
    }

    #endregion

    #region Types

    [Fact]
    public void EveryColumnHasTheSameTypeFamilyOnEveryBackend()
    {
        RequireDumps();
        AssertAgreement("Columns whose type family differs between backends", column => column.Family, SchemaSnapshot.FamiliesAgree);
    }

    [Fact]
    public void EveryColumnDeclaresTheSameWidthWhereItDeclaresOne()
    {
        RequireDumps();
        AssertAgreement("Columns declared at different widths", column => column.Size, observed => observed.Values.Distinct().Count() is 1);
    }

    [Fact]
    public void EveryColumnHasTheSameNullabilityOnEveryBackend()
    {
        RequireDumps();
        AssertAgreement("Columns whose nullability differs between backends", column => column.Nullable, observed => observed.Values.Distinct().Count() is 1);
    }

    #endregion

    #region Comparison

    private static void AssertAgreement<T>(string what, Func<ColumnSnapshot, T?> facet, Func<IReadOnlyDictionary<string, T>, bool> agrees)
    {
        var divergent = new List<string>();
        foreach (var (table, columns) in SchemaDumps.For(Sqlite))
        {
            foreach (var column in columns.Keys)
            {
                var observed = Observe(table, column, facet);
                if (observed.Count > 1 && !agrees(observed))
                    divergent.Add($"{table}.{column}: {string.Join(", ", observed.Select(entry => $"{entry.Key}={entry.Value}"))}");
            }
        }

        Report(what, divergent);
    }

    /// <summary>
    /// Fails with every divergence listed. Not <c>Assert.Equal</c> against an empty string, which
    /// xUnit truncates — the whole list has to be readable from a CI log.
    /// </summary>
    private static void Report(string what, IEnumerable<string> divergent)
    {
        var lines = divergent.Order(StringComparer.OrdinalIgnoreCase).ToArray();

        Assert.True(lines.Length is 0, $"{what} ({lines.Length}):\n  {string.Join("\n  ", lines)}");
    }

    private static Dictionary<string, T> Observe<T>(string table, string column, Func<ColumnSnapshot, T?> facet)
    {
        var observed = new Dictionary<string, T>();
        foreach (var backend in SchemaDumps.Backends)
        {
            if (!SchemaDumps.For(backend).TryGetValue(table, out var columns) || !columns.TryGetValue(column, out var snapshot))
                continue;

            // Absent, not disagreeing: SQLite reports no width at all.
            if (facet(snapshot) is { } value)
                observed[backend] = value;
        }

        return observed;
    }

    #endregion
}
