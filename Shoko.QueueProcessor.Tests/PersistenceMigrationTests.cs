using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shoko.QueueProcessor.Storage.Contexts;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>
/// Integration tests verifying that EF Core migrations execute correctly against an in-memory
/// SQLite database and produce the expected schema at each migration step.
///
/// Uses named shared-cache in-memory databases so that <see cref="SqliteQueueDbContext"/> can be
/// used directly (preserving the [DbContext] migration filter) while still being purely in-memory.
/// A "keeper" connection is held open for the duration of each test to prevent SQLite from
/// destroying the shared-cache database when the main context is disposed between steps.
/// </summary>
public class PersistenceMigrationTests
{
    private static string NewCs() =>
        $"Data Source=test_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    private static async Task<IReadOnlyList<string>> GetTablesAsync(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE '__EF%' ORDER BY name";
        var names = new List<string>();
        using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            names.Add(rdr.GetString(0));
        return names;
    }

    private static async Task<IReadOnlyList<string>> GetColumnsAsync(SqliteConnection conn, string table)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info('{table}')";
        var names = new List<string>();
        using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            names.Add(rdr.GetString(rdr.GetOrdinal("name")));
        return names;
    }

    // ── Migration step verification ───────────────────────────────────────────

    [Fact]
    public async Task InitialCreate_EstablishesJobsTable_WithoutChainColumns()
    {
        var cs = NewCs();
        using var keeper = new SqliteConnection(cs);
        keeper.Open();
        await using var ctx = new SqliteQueueDbContext(cs);

        await ctx.GetService<IMigrator>().MigrateAsync("20260528013231_InitialCreate");

        var tables = await GetTablesAsync(keeper);
        Assert.Contains("Jobs", tables);
        Assert.DoesNotContain("JobChains", tables);

        var cols = await GetColumnsAsync(keeper, "Jobs");
        foreach (var expected in new[] { "Id", "JobType", "JobKey", "JobDataJson", "Priority", "QueuedAt", "ScheduledAt", "RetryCount" })
            Assert.Contains(expected, cols);

        foreach (var unexpected in new[] { "ChainId", "IsChainFinally", "ParentJobId" })
            Assert.DoesNotContain(unexpected, cols);
    }

    [Fact]
    public async Task AddJobChainContext_AddsChainColumnsAndCreatesJobChainsTable()
    {
        var cs = NewCs();
        using var keeper = new SqliteConnection(cs);
        keeper.Open();
        await using var ctx = new SqliteQueueDbContext(cs);

        await ctx.Database.MigrateAsync();

        var tables = await GetTablesAsync(keeper);
        Assert.Contains("Jobs", tables);
        Assert.Contains("JobChains", tables);

        var jobCols = await GetColumnsAsync(keeper, "Jobs");
        foreach (var col in new[] { "ChainId", "IsChainFinally", "ParentJobId" })
            Assert.Contains(col, jobCols);

        var chainCols = await GetColumnsAsync(keeper, "JobChains");
        foreach (var col in new[] { "ChainId", "Status", "DataJson", "ResultsJson", "OutcomesJson", "CreatedAt", "UpdatedAt" })
            Assert.Contains(col, chainCols);
    }

    // ── Idempotency and history ───────────────────────────────────────────────

    [Fact]
    public async Task MigrateAsync_SecondCall_IsIdempotent()
    {
        var cs = NewCs();
        using var keeper = new SqliteConnection(cs);
        keeper.Open();
        await using var ctx = new SqliteQueueDbContext(cs);

        await ctx.Database.MigrateAsync();

        var ex = await Record.ExceptionAsync(() => ctx.Database.MigrateAsync());
        Assert.Null(ex);
    }

    [Fact]
    public async Task AllMigrations_AreRecordedInHistoryTable()
    {
        var cs = NewCs();
        using var keeper = new SqliteConnection(cs);
        keeper.Open();
        await using var ctx = new SqliteQueueDbContext(cs);

        await ctx.Database.MigrateAsync();

        // Every migration defined in the assembly must be recorded as applied, in the same order —
        // no hardcoded count/names, so newly added migrations are covered automatically.
        var defined = ctx.Database.GetMigrations().ToList();
        var applied = (await ctx.Database.GetAppliedMigrationsAsync()).ToList();

        Assert.NotEmpty(defined);
        Assert.Equal(defined, applied);
    }

    // ── Schema constraints ────────────────────────────────────────────────────

    [Fact]
    public async Task UniqueIndex_OnJobKey_PreventsDuplicateRows()
    {
        var cs = NewCs();
        using var keeper = new SqliteConnection(cs);
        keeper.Open();
        await using var ctx = new SqliteQueueDbContext(cs);
        await ctx.Database.MigrateAsync();

        using var first = keeper.CreateCommand();
        first.CommandText = "INSERT INTO Jobs (Id, JobType, JobKey, Priority, QueuedAt, RetryCount, IsChainFinally) VALUES (randomblob(16), 'T', 'dup-key', 0, 0, 0, 0)";
        await first.ExecuteNonQueryAsync();

        using var second = keeper.CreateCommand();
        second.CommandText = "INSERT INTO Jobs (Id, JobType, JobKey, Priority, QueuedAt, RetryCount, IsChainFinally) VALUES (randomblob(16), 'T', 'dup-key', 0, 0, 0, 0)";
        await Assert.ThrowsAnyAsync<Exception>(() => second.ExecuteNonQueryAsync());
    }
}
