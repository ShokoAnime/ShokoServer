using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shoko.QueueProcessor.Chain;
using Shoko.QueueProcessor.Storage.Contexts;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>
/// Integration tests for <see cref="JobChainContextRepository"/> against an in-memory SQLite
/// database with all migrations applied. Verifies that chain context CRUD and JSON round-trips
/// (data, outcomes, status) work correctly through the full persistence layer.
///
/// Many tests use a "fresh repo" (new <see cref="SqliteQueueDbContext"/> sharing the same named
/// in-memory database) to bypass EF Core's change tracker and confirm data was actually written
/// to the database rather than served from a cached entity.
/// </summary>
public class JobChainContextRepositoryTests
{
    private static string NewCs() =>
        $"Data Source=test_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    private static TestDbContextFactory MakeFactory(string cs) => new(cs);

    private sealed class TestDbContextFactory(string cs) : IDbContextFactory<QueueDbContext>
    {
        public QueueDbContext CreateDbContext() => new SqliteQueueDbContext(cs);
    }

    private static async Task<(SqliteConnection Keeper, string Cs, JobChainContextRepository Repo)> CreateAsync()
    {
        var cs = NewCs();
        var keeper = new SqliteConnection(cs);
        keeper.Open();
        await using var migCtx = new SqliteQueueDbContext(cs);
        await migCtx.Database.MigrateAsync();
        return (keeper, cs, new JobChainContextRepository(MakeFactory(cs)));
    }

    private static JobChainContextRepository FreshRepo(string cs) =>
        new(MakeFactory(cs));

    private static JobOutcome MakeOutcome(string jobType, JobOutcomeStatus status = JobOutcomeStatus.Succeeded) =>
        new()
        {
            JobId = Guid.NewGuid(),
            JobType = jobType,
            JobKey = $"key-{Guid.NewGuid():N}",
            Status = status,
            CompletedAt = DateTimeOffset.UtcNow,
        };

    // ── GetAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_NonexistentChain_ReturnsNull()
    {
        var (keeper, _, repo) = await CreateAsync();
        await using (keeper)
        {
            var result = await repo.GetAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
            Assert.Null(result);
        }
    }

    [Fact]
    public async Task GetAsync_ExistingChain_ReturnsContextWithCorrectId()
    {
        var (keeper, cs, repo) = await CreateAsync();
        await using (keeper)
        {
            var chainId = Guid.NewGuid();
            await repo.GetOrCreateAsync(chainId, TestContext.Current.CancellationToken);

            var loaded = await FreshRepo(cs).GetAsync(chainId, TestContext.Current.CancellationToken);

            Assert.NotNull(loaded);
            Assert.Equal(chainId, loaded.ChainId);
        }
    }

    // ── GetOrCreateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreateAsync_NewChain_CreatesActiveContext()
    {
        var (keeper, _, repo) = await CreateAsync();
        await using (keeper)
        {
            var chainId = Guid.NewGuid();

            var result = await repo.GetOrCreateAsync(chainId, TestContext.Current.CancellationToken);

            Assert.Equal(chainId, result.ChainId);
            Assert.Equal(ChainStatus.Active, result.Status);
        }
    }

    [Fact]
    public async Task GetOrCreateAsync_ExistingChain_ReturnsSavedData()
    {
        var (keeper, cs, repo) = await CreateAsync();
        await using (keeper)
        {
            var chainId = Guid.NewGuid();
            var first = await repo.GetOrCreateAsync(chainId, TestContext.Current.CancellationToken);
            first.SetData("answer", 42);
            await repo.SaveAsync(first, TestContext.Current.CancellationToken);

            var second = await FreshRepo(cs).GetOrCreateAsync(chainId, TestContext.Current.CancellationToken);

            Assert.Equal(42, second.GetData<int>("answer"));
        }
    }

    // ── SaveAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_NewContext_InsertsRecord()
    {
        var (keeper, cs, repo) = await CreateAsync();
        await using (keeper)
        {
            var chainId = Guid.NewGuid();
            var context = new JobChainContext(chainId);

            await repo.SaveAsync(context, TestContext.Current.CancellationToken);

            var loaded = await FreshRepo(cs).GetAsync(chainId, TestContext.Current.CancellationToken);
            Assert.NotNull(loaded);
            Assert.Equal(chainId, loaded.ChainId);
        }
    }

    [Fact]
    public async Task SaveAsync_ExistingContext_UpdatesRecord()
    {
        var (keeper, cs, repo) = await CreateAsync();
        await using (keeper)
        {
            var chainId = Guid.NewGuid();
            await repo.GetOrCreateAsync(chainId, TestContext.Current.CancellationToken);

            var loaded = await FreshRepo(cs).GetAsync(chainId, TestContext.Current.CancellationToken);
            loaded!.SetData("updated", true);
            await FreshRepo(cs).SaveAsync(loaded, TestContext.Current.CancellationToken);

            var reloaded = await FreshRepo(cs).GetAsync(chainId, TestContext.Current.CancellationToken);
            Assert.True(reloaded!.GetData<bool>("updated"));
        }
    }

    // ── Data round-trips ──────────────────────────────────────────────────────

    [Fact]
    public async Task DataFields_StringAndInt_RoundTripThroughDatabase()
    {
        var (keeper, cs, repo) = await CreateAsync();
        await using (keeper)
        {
            var chainId = Guid.NewGuid();
            var context = new JobChainContext(chainId);
            context.SetData("greeting", "hello world");
            context.SetData("count", 99);

            await repo.SaveAsync(context, TestContext.Current.CancellationToken);

            var loaded = await FreshRepo(cs).GetAsync(chainId, TestContext.Current.CancellationToken);
            Assert.Equal("hello world", loaded!.GetData<string>("greeting"));
            Assert.Equal(99, loaded.GetData<int>("count"));
        }
    }

    [Fact]
    public async Task DataFields_NullValue_OverwritesPreviousValue()
    {
        var (keeper, cs, repo) = await CreateAsync();
        await using (keeper)
        {
            var chainId = Guid.NewGuid();
            var context = new JobChainContext(chainId);
            context.SetData("key", "original");
            await repo.SaveAsync(context, TestContext.Current.CancellationToken);

            var loaded = await FreshRepo(cs).GetAsync(chainId, TestContext.Current.CancellationToken);
            loaded!.SetData<string?>("key", null);
            await FreshRepo(cs).SaveAsync(loaded, TestContext.Current.CancellationToken);

            var reloaded = await FreshRepo(cs).GetAsync(chainId, TestContext.Current.CancellationToken);
            Assert.Null(reloaded!.GetData<string>("key"));
        }
    }

    // ── ChainStatus round-trip ────────────────────────────────────────────────

    [Fact]
    public async Task ChainStatus_AllValues_RoundTripThroughDatabase()
    {
        var (keeper, cs, _) = await CreateAsync();
        await using (keeper)
        {
            foreach (var status in new[] { ChainStatus.Active, ChainStatus.Aborted, ChainStatus.Completed })
            {
                var chainId = Guid.NewGuid();
                var context = new JobChainContext(chainId, status);
                await FreshRepo(cs).SaveAsync(context, TestContext.Current.CancellationToken);

                var loaded = await FreshRepo(cs).GetAsync(chainId, TestContext.Current.CancellationToken);
                Assert.Equal(status, loaded!.Status);
            }
        }
    }

    // ── AddOutcomesAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task AddOutcomesAsync_BatchAdd_AllOutcomesPersisted()
    {
        var (keeper, cs, repo) = await CreateAsync();
        await using (keeper)
        {
            var chainId = Guid.NewGuid();
            await repo.GetOrCreateAsync(chainId, TestContext.Current.CancellationToken);

            var outcomes = new[]
            {
                MakeOutcome("TypeA"),
                MakeOutcome("TypeB", JobOutcomeStatus.Failed),
                MakeOutcome("TypeC", JobOutcomeStatus.Aborted),
            };
            await FreshRepo(cs).AddOutcomesAsync(chainId, outcomes, TestContext.Current.CancellationToken);

            var loaded = await FreshRepo(cs).GetAsync(chainId, TestContext.Current.CancellationToken);
            var all = loaded!.GetAllOutcomes();
            Assert.Equal(3, all.Count);
            Assert.Contains(all, o => o.JobType == "TypeA" && o.Status == JobOutcomeStatus.Succeeded);
            Assert.Contains(all, o => o.JobType == "TypeB" && o.Status == JobOutcomeStatus.Failed);
            Assert.Contains(all, o => o.JobType == "TypeC" && o.Status == JobOutcomeStatus.Aborted);
        }
    }

    [Fact]
    public async Task AddOutcomesAsync_NonexistentChain_IsNoOp()
    {
        var (keeper, _, repo) = await CreateAsync();
        await using (keeper)
        {
            var ex = await Record.ExceptionAsync(
                () => repo.AddOutcomesAsync(Guid.NewGuid(), [MakeOutcome("T")], TestContext.Current.CancellationToken));

            Assert.Null(ex);
        }
    }

    [Fact]
    public async Task AddOutcomesAsync_EmptyCollection_IsNoOp()
    {
        var (keeper, cs, repo) = await CreateAsync();
        await using (keeper)
        {
            var chainId = Guid.NewGuid();
            await repo.GetOrCreateAsync(chainId, TestContext.Current.CancellationToken);

            var ex = await Record.ExceptionAsync(
                () => FreshRepo(cs).AddOutcomesAsync(chainId, [], TestContext.Current.CancellationToken));

            Assert.Null(ex);
        }
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesChainRecord()
    {
        var (keeper, cs, repo) = await CreateAsync();
        await using (keeper)
        {
            var chainId = Guid.NewGuid();
            await repo.GetOrCreateAsync(chainId, TestContext.Current.CancellationToken);

            await FreshRepo(cs).DeleteAsync(chainId, TestContext.Current.CancellationToken);

            var loaded = await FreshRepo(cs).GetAsync(chainId, TestContext.Current.CancellationToken);
            Assert.Null(loaded);
        }
    }

    [Fact]
    public async Task DeleteAsync_NonexistentChain_IsNoOp()
    {
        var (keeper, _, repo) = await CreateAsync();
        await using (keeper)
        {
            var ex = await Record.ExceptionAsync(() => repo.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
            Assert.Null(ex);
        }
    }

    // ── Outcome lookup ────────────────────────────────────────────────────────

    [Fact]
    public async Task Outcomes_CanBeQueriedByJobId_AfterRoundTrip()
    {
        var (keeper, cs, repo) = await CreateAsync();
        await using (keeper)
        {
            var chainId = Guid.NewGuid();
            await repo.GetOrCreateAsync(chainId, TestContext.Current.CancellationToken);

            var outcome = MakeOutcome("TypeA");
            await FreshRepo(cs).AddOutcomesAsync(chainId, [outcome], TestContext.Current.CancellationToken);

            var loaded = await FreshRepo(cs).GetAsync(chainId, TestContext.Current.CancellationToken);
            var found = loaded!.GetOutcome(outcome.JobId);
            Assert.NotNull(found);
            Assert.Equal(outcome.JobId, found.JobId);
            Assert.Equal(outcome.JobType, found.JobType);
        }
    }
}
