using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Chain;
using Shoko.QueueProcessor.Events;
using Shoko.QueueProcessor.Orchestration;
using Shoko.QueueProcessor.Scheduling;
using Shoko.QueueProcessor.Storage;
using Shoko.QueueProcessor.Storage.Contexts;
using Shoko.QueueProcessor.Workers;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>
/// Covers <see cref="JobPriorityAttribute"/>: how a job type's declared priorities are resolved,
/// checked, and used by the enqueue paths that do not take an explicit priority.
/// </summary>
public class JobPriorityTests
{
    #region Fixture Job Types

    private abstract class TestJob : IQueueJob
    {
        public string TypeName => GetType().Name;

        public string Title => string.Empty;

        public Dictionary<string, object> Details => [];

        public void PostInit() { }

        public Task Process() => Task.CompletedTask;
    }

    private sealed class PlainJob : TestJob;

    private sealed class NumberedJob : TestJob
    {
        public int Number { get; set; }
    }

    [JobPriority(Default = RankedDefault, Prioritized = RankedPrioritized)]
    private sealed class RankedJob : TestJob;

    [JobPriority(Default = RankedPrioritized)]
    private sealed class InvertedJob : TestJob;

    [JobPriority(Prioritized = QueuePriority.Immediate)]
    private sealed class ImmediateJob : TestJob;

    private const int RankedDefault = QueuePriority.Prioritized + 10;

    private const int RankedPrioritized = RankedDefault + 10;

    #endregion

    #region Harness

    /// <summary>
    /// A scheduler over one pool per job type, whose waiting jobs can be read back.
    /// </summary>
    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider _provider;

        private readonly List<WorkerPool> _pools;

        public QueueScheduler Scheduler { get; }

        public PersistenceBuffer Buffer { get; }

        /// <summary>
        /// Builds the scheduler.
        /// </summary>
        /// <param name="repository">The store the buffer flushes to; a mock when <c>null</c>.</param>
        public Harness(IJobRepository? repository = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton(repository ?? new Mock<IJobRepository>().Object);
            services.AddTransient<PlainJob>();
            services.AddTransient<RankedJob>();
            services.AddTransient<NumberedJob>();
            _provider = services.BuildServiceProvider();

            var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
            var chainScopes = new ChainScopeRegistry(scopeFactory, NullLogger<ChainScopeRegistry>.Instance);
            Buffer = new PersistenceBuffer(scopeFactory, NullLogger<PersistenceBuffer>.Instance, flushIntervalMs: int.MaxValue);
            var orchestrator = new QueueOrchestrator(
                NullLogger<QueueOrchestrator>.Instance,
                Buffer,
                scopeFactory,
                new ConcurrencyRegistry(new Dictionary<Type, int>(), new Dictionary<Type, string?>(), new Dictionary<string, int>()),
                new RetryPolicyResolver(new RetryPolicy()),
                new QueueMetrics(),
                new QueueStateEventHandler(),
                chainScopes,
                maxTotalWorkers: 2
            );
            _pools =
            [
                new(nameof(PlainJob), 1, 0, [typeof(PlainJob)], []),
                new(nameof(RankedJob), 1, 0, [typeof(RankedJob)], []),
                new(nameof(NumberedJob), 1, 0, [typeof(NumberedJob)], []),
            ];
            orchestrator.Initialize([], _pools);
            Scheduler = new QueueScheduler(orchestrator, chainScopes, scopeFactory);
        }

        /// <summary>
        /// Gets the priority of the only waiting job of <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The job type.</typeparam>
        /// <returns>The priority.</returns>
        public int WaitingPriority<T>()
            => _pools.SelectMany(pool => pool.GetWaitingSnapshot())
                .Single(job => job.JobType == JobTypeNames.Stored(typeof(T)))
                .Priority;

        /// <summary>
        /// Gets the pool that handles <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The job type.</typeparam>
        /// <returns>The pool.</returns>
        public WorkerPool PoolOf<T>()
            => _pools.Single(pool => pool.HandledTypes.Contains(typeof(T)));

        public void Dispose() => _provider.Dispose();
    }

    #endregion

    #region Resolution

    [Theory]
    [InlineData(typeof(PlainJob), false, QueuePriority.Default)]
    [InlineData(typeof(PlainJob), true, QueuePriority.Prioritized)]
    [InlineData(typeof(RankedJob), false, RankedDefault)]
    [InlineData(typeof(RankedJob), true, RankedPrioritized)]
    public void For_ResolvesDeclaredOrDefaultPriorities(Type jobType, bool prioritize, int expected)
        => Assert.Equal(expected, QueuePriority.For(jobType, prioritize));

    [Theory]
    [InlineData(typeof(PlainJob), false, QueuePriority.Scheduled)]
    [InlineData(typeof(PlainJob), true, QueuePriority.Prioritized)]
    [InlineData(typeof(RankedJob), false, RankedDefault)]
    [InlineData(typeof(RankedJob), true, RankedPrioritized)]
    public void ScheduledFor_TakesHigherOfScheduledAndDefault(Type jobType, bool prioritize, int expected)
        => Assert.Equal(expected, QueuePriority.ScheduledFor(jobType, prioritize));

    [Theory]
    [InlineData(typeof(InvertedJob))]
    [InlineData(typeof(ImmediateJob))]
    public void InvalidAttribute_Throws(Type jobType)
    {
        Assert.Throws<InvalidOperationException>(() => QueuePriority.For(jobType, prioritize: false));
        Assert.Throws<InvalidOperationException>(() => new PoolDiscovery(NullLogger<PoolDiscovery>.Instance, 2, 2).Discover([jobType], []));
    }

    #endregion

    #region Enqueue Paths

    [Theory]
    [InlineData(false, RankedDefault)]
    [InlineData(true, RankedPrioritized)]
    public async Task Enqueue_Typed_UsesDeclaredPriority(bool prioritize, int expected)
    {
        using var harness = new Harness();

        await harness.Scheduler.Enqueue<RankedJob>(prioritize: prioritize, ct: TestContext.Current.CancellationToken);

        Assert.Equal(expected, harness.WaitingPriority<RankedJob>());
    }

    [Theory]
    [InlineData(false, RankedDefault)]
    [InlineData(true, RankedPrioritized)]
    public async Task Enqueue_Untyped_UsesDeclaredPriority(bool prioritize, int expected)
    {
        using var harness = new Harness();

        await harness.Scheduler.Enqueue(typeof(RankedJob), prioritize: prioritize);

        Assert.Equal(expected, harness.WaitingPriority<RankedJob>());
    }

    [Fact]
    public async Task Enqueue_Unannotated_UsesQueueDefault()
    {
        using var harness = new Harness();

        await harness.Scheduler.Enqueue<PlainJob>(ct: TestContext.Current.CancellationToken);

        Assert.Equal(QueuePriority.Default, harness.WaitingPriority<PlainJob>());
    }

    [Fact]
    public async Task EnqueueWithPriority_OverridesDeclaredPriority()
    {
        using var harness = new Harness();

        await harness.Scheduler.EnqueueWithPriority<RankedJob>(null, QueuePriority.Scheduled, ct: TestContext.Current.CancellationToken);

        Assert.Equal(QueuePriority.Scheduled, harness.WaitingPriority<RankedJob>());
    }

    [Fact]
    public async Task Chain_Enqueue_HeadUsesDeclaredDefault()
    {
        using var harness = new Harness();

        await harness.Scheduler.CreateJobChain()
            .Then<RankedJob>()
            .Then<PlainJob>()
            .Enqueue();

        Assert.Equal(RankedDefault, harness.WaitingPriority<RankedJob>());
    }

    [Fact]
    public async Task Chain_EnqueueAfterCurrent_OutsideJob_HeadUsesDeclaredPrioritized()
    {
        using var harness = new Harness();

        await harness.Scheduler.CreateJobChain()
            .Then(typeof(RankedJob))
            .EnqueueAfterCurrent();

        Assert.Equal(RankedPrioritized, harness.WaitingPriority<RankedJob>());
    }

    #endregion

    #region Re-enqueue

    [Fact]
    public async Task ReEnqueue_Waiting_HigherPriority_RaisesItAheadOfEarlierJobs()
    {
        using var harness = new Harness();
        var ct = TestContext.Current.CancellationToken;
        await harness.Scheduler.Enqueue<NumberedJob>(job => job.Number = 1, ct: ct);
        await harness.Scheduler.Enqueue<NumberedJob>(job => job.Number = 2, ct: ct);

        await harness.Scheduler.Enqueue<NumberedJob>(job => job.Number = 2, prioritize: true, ct: ct);
        await harness.Scheduler.EnqueueWithPriority<NumberedJob>(job => job.Number = 1, QueuePriority.Scheduled, ct: ct);

        var pool = harness.PoolOf<NumberedJob>();
        Assert.Equal(
            [QueuePriority.For(typeof(NumberedJob), prioritize: true), QueuePriority.Scheduled],
            pool.GetWaitingSnapshot().Select(job => job.Priority)
        );
        Assert.Equal(JobKeyBuilder<NumberedJob>.Create().UsingJobData(job => job.Number = 2).Build(), pool.TryAcquire()?.JobKey);
    }

    [Fact]
    public async Task ReEnqueue_Waiting_LowerPriority_DoesNotDemoteIt()
    {
        using var harness = new Harness();
        var ct = TestContext.Current.CancellationToken;
        await harness.Scheduler.Enqueue<NumberedJob>(prioritize: true, ct: ct);

        await harness.Scheduler.Enqueue<NumberedJob>(ct: ct);

        Assert.Equal(QueuePriority.For(typeof(NumberedJob), prioritize: true), harness.WaitingPriority<NumberedJob>());
    }

    [Fact]
    public async Task ReEnqueue_Executing_LeavesItAlone()
    {
        using var harness = new Harness();
        var ct = TestContext.Current.CancellationToken;
        await harness.Scheduler.Enqueue<NumberedJob>(ct: ct);
        var pool = harness.PoolOf<NumberedJob>();
        var executing = pool.TryAcquire();

        await harness.Scheduler.Enqueue<NumberedJob>(prioritize: true, ct: ct);

        Assert.Equal(QueuePriority.Default, executing?.Priority);
        Assert.Empty(pool.GetWaitingSnapshot());
    }

    #endregion

    #region Raises

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReEnqueue_ImmediateJob_Prioritized_StaysImmediate(bool withMergeHandler)
    {
        using var harness = new Harness();
        var ct = TestContext.Current.CancellationToken;
        if (withMergeHandler)
            harness.Scheduler.RegisterMergeHandler<NumberedJob>((_, _) => false);
        await harness.Scheduler.Enqueue<NumberedJob>(ct: ct);
        // Promotes the waiting job at once; the returned task only completes when it has run.
        _ = harness.Scheduler.EnqueueImmediate<NumberedJob>(ct: ct);

        await harness.Scheduler.Enqueue<NumberedJob>(prioritize: true, ct: ct);

        Assert.Equal(QueuePriority.Immediate, harness.WaitingPriority<NumberedJob>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReEnqueue_DeferredJob_Prioritized_KeepsItsQueueTimeAndDelay(bool withMergeHandler)
    {
        using var harness = new Harness();
        var ct = TestContext.Current.CancellationToken;
        if (withMergeHandler)
            harness.Scheduler.RegisterMergeHandler<NumberedJob>((_, _) => false);
        await harness.Scheduler.Enqueue<NumberedJob>(scheduledAt: DateTimeOffset.UtcNow.AddHours(1), ct: ct);
        var before = Assert.Single(harness.PoolOf<NumberedJob>().GetWaitingSnapshot());

        await harness.Scheduler.Enqueue<NumberedJob>(prioritize: true, ct: ct);

        var after = Assert.Single(harness.PoolOf<NumberedJob>().GetWaitingSnapshot());
        Assert.Equal(QueuePriority.For(typeof(NumberedJob), prioritize: true), after.Priority);
        Assert.Equal(before.QueuedAt, after.QueuedAt);
        Assert.Equal(before.ScheduledAt, after.ScheduledAt);
    }

    [Fact]
    public async Task Raise_IsSaved_AndSurvivesAReload()
    {
        var connectionString = $"Data Source=priority_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync(TestContext.Current.CancellationToken);
        await using (var migrations = new SqliteQueueDbContext(connectionString))
            await migrations.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var repository = new JobRepository(new SqliteContextFactory(connectionString));
        using var harness = new Harness(repository);
        var ct = TestContext.Current.CancellationToken;
        await harness.Scheduler.Enqueue<NumberedJob>(ct: ct);
        await harness.Buffer.FlushNowAsync(ct);

        await harness.Scheduler.Enqueue<NumberedJob>(prioritize: true, ct: ct);
        await harness.Buffer.FlushNowAsync(ct);

        var stored = Assert.Single(await repository.LoadAllAsync(ct));
        Assert.Equal(QueuePriority.For(typeof(NumberedJob), prioritize: true), stored.Priority);
    }

    private sealed class SqliteContextFactory(string connectionString) : IDbContextFactory<QueueDbContext>
    {
        public QueueDbContext CreateDbContext() => new SqliteQueueDbContext(connectionString);
    }

    #endregion
}
