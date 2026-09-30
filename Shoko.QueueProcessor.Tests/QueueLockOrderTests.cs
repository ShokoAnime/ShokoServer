using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Chain;
using Shoko.QueueProcessor.Events;
using Shoko.QueueProcessor.Orchestration;
using Shoko.QueueProcessor.Storage;
using Shoko.QueueProcessor.Workers;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>
/// Runs the queue operations that touch both the orchestrator gate and a pool's sub-queue lock
/// against busy acquisition, so a lock-order inversion between them deadlocks and times out.
/// </summary>
public class QueueLockOrderTests
{
    #region Fixtures

    [JobKeyMember("LockOrderJob")]
    private class LockOrderJob : IQueueJob
    {
        public string TypeName => "LockOrderJob";

        public string Title => "";

        public Dictionary<string, object> Details => [];

        public void PostInit() { }

        public Task Process() => Task.CompletedTask;
    }

    private const int Rounds = 200;

    private static readonly TimeSpan _deadline = TimeSpan.FromSeconds(30);

    private static readonly string _storedType = JobTypeNames.Stored(typeof(LockOrderJob));

    private static (QueueOrchestrator Orchestrator, WorkerPool Pool) MakeSetup()
    {
        var repo = new Mock<IJobRepository>();
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(IJobRepository))).Returns(repo.Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(sp.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var buffer = new PersistenceBuffer(scopeFactory.Object, NullLogger<PersistenceBuffer>.Instance, flushIntervalMs: int.MaxValue, maxFlushBatch: int.MaxValue);
        var registry = new ConcurrencyRegistry(new Dictionary<Type, int>(), new Dictionary<Type, string?>(), new Dictionary<string, int>());
        var orchestrator = new QueueOrchestrator(
            NullLogger<QueueOrchestrator>.Instance,
            buffer,
            scopeFactory.Object,
            registry,
            new RetryPolicyResolver(new RetryPolicy { MaxRetries = 0 }),
            new QueueMetrics(),
            new QueueStateEventHandler(),
            new ChainScopeRegistry(scopeFactory.Object, NullLogger<ChainScopeRegistry>.Instance),
            4);
        var pool = new WorkerPool("LockOrderPool", 4, AcquisitionAttribute.LowestPriority, [typeof(LockOrderJob)], []);
        orchestrator.Initialize([], [pool]);
        return (orchestrator, pool);
    }

    private static EnqueueContext MakeContext(string key) => new()
    {
        Type = typeof(LockOrderJob),
        Job = new QueuedJob
        {
            Id = Guid.NewGuid(),
            JobType = _storedType,
            JobKey = key,
            QueuedAt = DateTimeOffset.UtcNow,
        },
        DisplayItem = new QueueItem
        {
            Key = key,
            JobType = nameof(LockOrderJob),
            TypeName = nameof(LockOrderJob),
            Title = "",
            Details = [],
        },
    };

    /// <summary>
    /// Keeps acquiring and completing jobs until <paramref name="stop"/> is cancelled, the way
    /// the pool's workers would, so the pool lock is held while the gate is taken.
    /// </summary>
    /// <param name="orchestrator">The orchestrator the pool registers with.</param>
    /// <param name="pool">The pool to acquire from.</param>
    /// <param name="stop">Cancelled once the operation under test is done.</param>
    /// <returns>A task that runs on its own thread until stopped.</returns>
    private static Task RunAcquirer(QueueOrchestrator orchestrator, WorkerPool pool, CancellationToken stop)
        => Task.Factory.StartNew(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                if (pool.TryAcquire() is { } job)
                    orchestrator.OnComplete(job.Id);
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    /// <summary>
    /// Runs <paramref name="operation"/> <see cref="Rounds"/> times, refilling the queue before
    /// each round, while two acquirers keep the pool busy.
    /// </summary>
    /// <param name="operation">The queue operation under test, given the round's job keys.</param>
    /// <returns>Whether every round finished before the deadline.</returns>
    private static async Task<bool> RunAgainstAcquisition(Func<QueueOrchestrator, IReadOnlyList<string>, Task> operation)
    {
        var (orchestrator, pool) = MakeSetup();
        using var stop = new CancellationTokenSource();
        var acquirers = new[] { RunAcquirer(orchestrator, pool, stop.Token), RunAcquirer(orchestrator, pool, stop.Token) };

        var rounds = Task.Factory.StartNew(async () =>
        {
            for (var round = 0; round < Rounds; round++)
            {
                var keys = Enumerable.Range(0, 16).Select(i => $"lock-order-{round}-{i}").ToList();
                await orchestrator.EnqueueRangeAsync(keys.Select(MakeContext));
                await operation(orchestrator, keys);
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

        var finished = await Task.WhenAny(rounds, Task.Delay(_deadline, TestContext.Current.CancellationToken)) == rounds;
        stop.Cancel();
        if (!finished)
            return false;

        await rounds;
        await Task.WhenAll(acquirers).WaitAsync(_deadline, TestContext.Current.CancellationToken);
        return true;
    }

    #endregion

    #region Lock Order

    [Fact]
    public async Task ClearDoesNotDeadlockWithAcquisition()
    {
        var finished = await RunAgainstAcquisition((orchestrator, _) => orchestrator.ClearAsync(TestContext.Current.CancellationToken));

        Assert.True(finished, "Clearing the queue deadlocked with a worker acquiring a job.");
    }

    [Fact]
    public async Task RemoveDoesNotDeadlockWithAcquisition()
    {
        var finished = await RunAgainstAcquisition(async (orchestrator, keys) =>
        {
            foreach (var key in keys)
                await orchestrator.RemoveAsync(key, TestContext.Current.CancellationToken);
        });

        Assert.True(finished, "Removing a waiting job deadlocked with a worker acquiring a job.");
    }

    #endregion

    #region Semantics

    [Fact]
    public async Task ClearDropsWaitingJobsAndKeepsExecutingOnes()
    {
        var (orchestrator, pool) = MakeSetup();
        await orchestrator.EnqueueAsync(MakeContext("executing"), TestContext.Current.CancellationToken);
        var executing = pool.TryAcquire();
        Assert.NotNull(executing);
        await orchestrator.EnqueueRangeAsync(new[] { "a", "b", "c" }.Select(MakeContext), TestContext.Current.CancellationToken);

        await orchestrator.ClearAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, pool.WaitingCount);
        Assert.True(orchestrator.IsQueued("executing"));
        Assert.False(orchestrator.IsQueued("a"));

        // Nothing stays held: a job enqueued again after the clear can run.
        await orchestrator.EnqueueAsync(MakeContext("a"), TestContext.Current.CancellationToken);
        Assert.Equal("a", pool.TryAcquire()?.JobKey);
    }

    #endregion
}
