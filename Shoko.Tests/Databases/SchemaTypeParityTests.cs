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
/// Each backend keeps its own hand-written DDL in <c>Shoko.Server/Databases/</c>, so nothing forces
/// the three to agree — a column added, widened or made nullable on one can be missed on another, and
/// no test that exercises a single backend can see it. <c>add missing primary keys on SQL Server for
/// 38 tables</c> and <c>widen TMDB_Show/TMDB_Movie Genres column on MySQL and SQL Server</c> were
/// both this.
///
/// The schemas compared here are read from the catalog of a real database of each backend, migrated
/// from empty by <c>Shoko.IntegrationTests</c>. Nothing is committed, so there is no recorded schema
/// to fall out of date: CI migrates all three, one job per backend, and this compares what those runs
/// actually produced. Without all three dumps present there is nothing to compare and these skip —
/// see <see cref="SchemaDumps.DirectoryVariable"/> and <c>Shoko.TestData/Schema/README.md</c> for
/// running it locally.
///
/// Two differences are not divergences and are treated as equal: SQLite's <c>INTEGER</c> is a
/// variable-width signed 64-bit integer, so it is the same type as <c>BIGINT</c> elsewhere; and
/// SQLite declares no column widths at all, using type affinity instead, so it takes no part in the
/// width comparison. Everything else has to match.
/// </remarks>
public class SchemaTypeParityTests
{
    private const string Sqlite = "SQLite";

    /// <summary>
    /// Skips when the run has no dumps to compare. They come from migrating a real database of each
    /// backend, so a machine with only one of the three cannot answer the question at all — and a
    /// silent pass would claim it had.
    /// </summary>
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
        // Guards the embedded resource names: a typo would otherwise turn every comparison below into
        // a comparison of two empty schemas.
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
    /// Fails with every divergence listed, one per line.
    /// </summary>
    /// <remarks>
    /// Not <c>Assert.Equal</c> against an empty string: xUnit renders that as a truncated string diff,
    /// and this list is the work to be done, so all of it has to be readable from a CI log.
    /// </remarks>
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

            // A null facet is an absence of information rather than a value to disagree with — it is
            // how SQLite reports a column whose width it never declared.
            if (facet(snapshot) is { } value)
                observed[backend] = value;
        }

        return observed;
    }

    #endregion
}
