using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shoko.QueueProcessor.Storage;
using Shoko.QueueProcessor.Storage.Contexts;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>
/// Integration tests for <see cref="JobRepository"/> against an in-memory SQLite database
/// with all migrations applied. Verifies that insert, load, delete, update, and activation
/// operations correctly persist data across EF Core save boundaries.
///
/// Each test creates a uniquely-named shared-cache in-memory database so that
/// <see cref="SqliteQueueDbContext"/> can be used directly (matching the migration filter)
/// while keeping data isolated between tests.
/// </summary>
public class JobRepositoryTests
{
    private static string NewCs() =>
        $"Data Source=test_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    private static IDbContextFactory<QueueDbContext> MakeFactory(string cs) =>
        new TestDbContextFactory(cs);

    private sealed class TestDbContextFactory(string cs) : IDbContextFactory<QueueDbContext>
    {
        public QueueDbContext CreateDbContext() => new SqliteQueueDbContext(cs);
    }

    private static async Task<(SqliteConnection Keeper, JobRepository Repo)> CreateAsync()
    {
        var cs = NewCs();
        var keeper = new SqliteConnection(cs);
        keeper.Open();
        await using var migCtx = new SqliteQueueDbContext(cs);
        await migCtx.Database.MigrateAsync();
        return (keeper, new JobRepository(MakeFactory(cs)));
    }

    private static QueuedJob FakeJob(
        string? jobKey = null,
        int priority = 0,
        DateTimeOffset? queuedAt = null,
        DateTimeOffset? scheduledAt = null,
        Guid? parentJobId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            JobType = "TestJob, TestAssembly",
            JobKey = jobKey ?? $"key-{Guid.NewGuid():N}",
            Priority = priority,
            QueuedAt = queuedAt ?? DateTimeOffset.UtcNow,
            ScheduledAt = scheduledAt,
            ParentJobId = parentJobId,
        };

    // ── Insert + Load ─────────────────────────────────────────────────────────

    [Fact]
    public async Task InsertBatchAsync_SingleJob_PersistedAndReloaded()
    {
        var (keeper, repo) = await CreateAsync();
        await using (keeper)
        {
            var job = FakeJob(jobKey: "persisted-key");

            await repo.InsertBatchAsync([job]);

            var loaded = await repo.LoadAllAsync();
            Assert.Single(loaded);
            Assert.Equal(job.Id, loaded[0].Id);
            Assert.Equal(job.JobKey, loaded[0].JobKey);
            Assert.Equal(job.JobType, loaded[0].JobType);
            Assert.Equal(job.Priority, loaded[0].Priority);
        }
    }

    [Fact]
    public async Task InsertBatchAsync_MultipleJobs_AllReloaded()
    {
        var (keeper, repo) = await CreateAsync();
        await using (keeper)
        {
            var jobs = Enumerable.Range(0, 10).Select(_ => FakeJob()).ToList();

            await repo.InsertBatchAsync(jobs);

            var loaded = await repo.LoadAllAsync();
            Assert.Equal(10, loaded.Count);
            foreach (var j in jobs)
                Assert.Contains(loaded, l => l.Id == j.Id);
        }
    }

    [Fact]
    public async Task InsertBatchAsync_DuplicateJobKey_ThrowsDbUpdateException()
    {
        var (keeper, repo) = await CreateAsync();
        await using (keeper)
        {
            await repo.InsertBatchAsync([FakeJob(jobKey: "same-key")]);

            await Assert.ThrowsAsync<DbUpdateException>(
                () => repo.InsertBatchAsync([FakeJob(jobKey: "same-key")]));
        }
    }

    // ── Load ordering ─────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAllAsync_OrdersBy_TypeThenScheduledAtThenPriorityDescThenQueuedAt()
    {
        var (keeper, repo) = await CreateAsync();
        await using (keeper)
        {
            var baseTime = DateTimeOffset.UnixEpoch;

            // Same JobType, no ScheduledAt → sort by Priority desc, then QueuedAt asc
            var low = FakeJob("k1", priority: 1, queuedAt: baseTime);
            var highLater = FakeJob("k2", priority: 10, queuedAt: baseTime.AddSeconds(1));
            var highEarlier = FakeJob("k3", priority: 10, queuedAt: baseTime);

            await repo.InsertBatchAsync([low, highLater, highEarlier]);

            var loaded = await repo.LoadAllAsync();

            Assert.Equal(3, loaded.Count);
            Assert.Equal(highEarlier.Id, loaded[0].Id); // priority 10, earliest QueuedAt
            Assert.Equal(highLater.Id, loaded[1].Id);   // priority 10, later QueuedAt
            Assert.Equal(low.Id, loaded[2].Id);          // priority 1
        }
    }

    [Fact]
    public async Task LoadAllAsync_NullScheduledAt_SortedBeforeScheduledJobs()
    {
        var (keeper, repo) = await CreateAsync();
        await using (keeper)
        {
            // SQLite NULL < any non-null value in ASC, so immediate jobs come first
            var immediate = FakeJob("k1");
            var deferred = FakeJob("k2", scheduledAt: DateTimeOffset.UnixEpoch.AddDays(1));

            await repo.InsertBatchAsync([deferred, immediate]);

            var loaded = await repo.LoadAllAsync();
            Assert.Equal(immediate.Id, loaded[0].Id);
            Assert.Equal(deferred.Id, loaded[1].Id);
        }
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesSingleJob()
    {
        var (keeper, repo) = await CreateAsync();
        await using (keeper)
        {
            var job = FakeJob();
            await repo.InsertBatchAsync([job]);

            await repo.DeleteAsync(job.Id);

            Assert.Empty(await repo.LoadAllAsync());
        }
    }

    [Fact]
    public async Task DeleteBatchAsync_RemovesAllSpecifiedJobs()
    {
        var (keeper, repo) = await CreateAsync();
        await using (keeper)
        {
            var toDelete = Enumerable.Range(0, 3).Select(_ => FakeJob()).ToList();
            var toKeep = FakeJob();
            await repo.InsertBatchAsync([.. toDelete, toKeep]);

            await repo.DeleteBatchAsync(toDelete.Select(j => j.Id).ToList());

            var remaining = await repo.LoadAllAsync();
            Assert.Single(remaining);
            Assert.Equal(toKeep.Id, remaining[0].Id);
        }
    }

    [Fact]
    public async Task DeleteBatchAsync_EmptyCollection_IsNoOp()
    {
        var (keeper, repo) = await CreateAsync();
        await using (keeper)
        {
            await repo.InsertBatchAsync([FakeJob()]);

            var ex = await Record.ExceptionAsync(() => repo.DeleteBatchAsync([]));

            Assert.Null(ex);
            Assert.Single(await repo.LoadAllAsync());
        }
    }

    // ── UpdateRetry ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateRetryAsync_UpdatesRetryCountAndScheduledAt()
    {
        var (keeper, repo) = await CreateAsync();
        await using (keeper)
        {
            var job = FakeJob();
            await repo.InsertBatchAsync([job]);

            // Truncate to ms precision to match SQLite's Unix-ms storage
            var retryAt = DateTimeOffset.FromUnixTimeMilliseconds(
                DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds());
            await repo.UpdateRetryAsync(job.Id, retryCount: 3, retryAt);

            // Each LoadAllAsync call creates a fresh context — no stale change tracker
            var loaded = (await repo.LoadAllAsync()).Single();

            Assert.Equal(3, loaded.RetryCount);
            Assert.NotNull(loaded.ScheduledAt);
            Assert.Equal(retryAt.ToUnixTimeMilliseconds(), loaded.ScheduledAt!.Value.ToUnixTimeMilliseconds());
        }
    }

    // ── ClearAll ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClearAllAsync_RemovesAllRows()
    {
        var (keeper, repo) = await CreateAsync();
        await using (keeper)
        {
            await repo.InsertBatchAsync([FakeJob(), FakeJob(), FakeJob()]);

            await repo.ClearAllAsync();

            Assert.Empty(await repo.LoadAllAsync());
        }
    }

    // ── Chain child activation ────────────────────────────────────────────────

    [Fact]
    public async Task ActivateChainChildrenAsync_NullsParentJobIdForSpecifiedJobs()
    {
        var (keeper, repo) = await CreateAsync();
        await using (keeper)
        {
            var parentId = Guid.NewGuid();
            var child1 = FakeJob(parentJobId: parentId);
            var child2 = FakeJob(parentJobId: parentId);
            var standalone = FakeJob();

            await repo.InsertBatchAsync([child1, child2, standalone]);
            await repo.ActivateChainChildrenAsync([child1.Id, child2.Id]);

            var loaded = await repo.LoadAllAsync();
            Assert.Null(loaded.Single(j => j.Id == child1.Id).ParentJobId);
            Assert.Null(loaded.Single(j => j.Id == child2.Id).ParentJobId);
            Assert.Null(loaded.Single(j => j.Id == standalone.Id).ParentJobId);
        }
    }

    [Fact]
    public async Task ActivateChainChildrenAsync_EmptyCollection_IsNoOp()
    {
        var (keeper, repo) = await CreateAsync();
        await using (keeper)
        {
            var child = FakeJob(parentJobId: Guid.NewGuid());
            await repo.InsertBatchAsync([child]);

            var ex = await Record.ExceptionAsync(() => repo.ActivateChainChildrenAsync([]));

            Assert.Null(ex);
            Assert.NotNull((await repo.LoadAllAsync()).Single().ParentJobId);
        }
    }

    // ── Large batch chunking ──────────────────────────────────────────────────

    [Fact]
    public async Task InsertBatchAsync_LargeBatch_PersistedAcrossChunkBoundary()
    {
        var (keeper, repo) = await CreateAsync();
        await using (keeper)
        {
            const int count = 501; // crosses the 500-item chunk boundary
            var jobs = Enumerable.Range(0, count).Select(_ => FakeJob()).ToList();

            await repo.InsertBatchAsync(jobs);

            Assert.Equal(count, (await repo.LoadAllAsync()).Count);
        }
    }

    // ── DateTimeOffset round-trip ─────────────────────────────────────────────

    [Fact]
    public async Task QueuedAt_RoundTrips_WithMillisecondPrecision()
    {
        var (keeper, repo) = await CreateAsync();
        await using (keeper)
        {
            var original = DateTimeOffset.FromUnixTimeMilliseconds(1_748_964_123_456L);
            var job = FakeJob(queuedAt: original);

            await repo.InsertBatchAsync([job]);

            var loaded = (await repo.LoadAllAsync()).Single();
            Assert.Equal(original.ToUnixTimeMilliseconds(), loaded.QueuedAt.ToUnixTimeMilliseconds());
        }
    }

    [Fact]
    public async Task ScheduledAt_WhenNull_RoundTripsAsNull()
    {
        var (keeper, repo) = await CreateAsync();
        await using (keeper)
        {
            await repo.InsertBatchAsync([FakeJob(scheduledAt: null)]);

            var loaded = (await repo.LoadAllAsync()).Single();
            Assert.Null(loaded.ScheduledAt);
        }
    }

    [Fact]
    public async Task ScheduledAt_WhenSet_RoundTrips_WithMillisecondPrecision()
    {
        var (keeper, repo) = await CreateAsync();
        await using (keeper)
        {
            var scheduledAt = DateTimeOffset.FromUnixTimeMilliseconds(1_748_964_000_000L);
            await repo.InsertBatchAsync([FakeJob(scheduledAt: scheduledAt)]);

            var loaded = (await repo.LoadAllAsync()).Single();
            Assert.Equal(scheduledAt.ToUnixTimeMilliseconds(), loaded.ScheduledAt!.Value.ToUnixTimeMilliseconds());
        }
    }
}
