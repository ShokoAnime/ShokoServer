using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Chain;
using Shoko.QueueProcessor.Events;
using Shoko.QueueProcessor.Orchestration;
using Shoko.QueueProcessor.Scheduling;
using Shoko.QueueProcessor.Storage;
using Shoko.QueueProcessor.Workers;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>
/// A chain job that is retried or re-queued runs again inside its chain, with the chain's scope
/// and context, and the rest of the chain still runs after it. A discarded one skips the rest but
/// the finally step, and promoting a queued chain job keeps its chain and actor.
/// </summary>
public class ChainRetryTests
{
    #region Fixture

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How a fixture job fails.
    /// </summary>
    public enum FailMode
    {
        None,
        ThrowOnce,
        RequeueOnce,
        ThrowAlways,
    }

    /// <summary>
    /// One run of a fixture job: the chain it saw and the scope it ran in.
    /// </summary>
    public sealed record Run(Guid? ChainId, Guid ScopeId);

    /// <summary>
    /// A scoped service telling scopes apart.
    /// </summary>
    public sealed class ScopeMarker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// The runs of each fixture job, by the job's name.
    /// </summary>
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<Run>> _runs = new();

    /// <summary>
    /// Completed once a fixture job, by its name, has run the given number of times.
    /// </summary>
    private static readonly ConcurrentDictionary<(string Name, int Count), TaskCompletionSource> _reached = new();

    private static ConcurrentQueue<Run> Runs(string name)
        => _runs.GetOrAdd(name, _ => new());

    private static Task Reached(string name, int count)
    {
        var reached = _reached.GetOrAdd((name, count), _ => new(TaskCreationOptions.RunContinuationsAsynchronously));
        if (Runs(name).Count >= count)
            reached.TrySetResult();
        return reached.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
    }

    private static Task RecordAndFail(string name, FailMode fail, IJobChainContextAccessor accessor, ScopeMarker marker)
    {
        var runs = Runs(name);
        runs.Enqueue(new(accessor.GetCurrentContext()?.ChainId, marker.Id));
        foreach (var ((reachedName, count), reached) in _reached)
        {
            if (reachedName == name && runs.Count >= count)
                reached.TrySetResult();
        }

        return fail switch
        {
            FailMode.ThrowOnce when runs.Count == 1 => throw new InvalidOperationException("First attempt fails."),
            FailMode.RequeueOnce when runs.Count == 1 => throw new RequeueJobException(),
            FailMode.ThrowAlways => throw new InvalidOperationException("Every attempt fails."),
            _ => Task.CompletedTask,
        };
    }

    public sealed class StepJob(IJobChainContextAccessor accessor, ScopeMarker marker) : IQueueJob
    {
        public string Name { get; set; } = string.Empty;

        public FailMode Fail { get; set; }

        public string TypeName => "Step";

        public string Title => Name;

        public Task Process() => RecordAndFail(Name, Fail, accessor, marker);
    }

    [ChainFinally]
    public sealed class FinallyStepJob(IJobChainContextAccessor accessor, ScopeMarker marker) : IQueueJob
    {
        public string Name { get; set; } = string.Empty;

        public string TypeName => "Finally step";

        public string Title => Name;

        public Task Process() => RecordAndFail(Name, FailMode.None, accessor, marker);
    }

    #endregion

    #region Harness

    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider _provider;

        public WorkerPool Pool { get; }

        public QueueScheduler Scheduler { get; }

        public ChainScopeRegistry ChainScopes { get; }

        public Harness(int maxRetries = 3)
        {
            var repo = new Mock<IJobRepository>();
            repo.Setup(r => r.InsertBatchAsync(It.IsAny<IReadOnlyCollection<QueuedJob>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            repo.Setup(r => r.DeleteBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            repo.Setup(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            repo.Setup(r => r.UpdateRetryAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            repo.Setup(r => r.ActivateChainChildrenAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            repo.Setup(r => r.ReparentChainChildrenAsync(It.IsAny<IReadOnlyCollection<(Guid, Guid)>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var chainRepo = new Mock<IJobChainContextRepository>();
            chainRepo.Setup(r => r.GetOrCreateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid id, CancellationToken _) => new JobChainContext(id));
            chainRepo.Setup(r => r.SaveAsync(It.IsAny<JobChainContext>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            chainRepo.Setup(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var services = new ServiceCollection();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddSingleton(repo.Object);
            services.AddSingleton(chainRepo.Object);
            services.AddSingleton<IQueueScheduler>(_ => Scheduler!);
            services.AddScoped<ScopeMarker>();
            services.AddScoped<JobCancellationAccessor>();
            services.AddScoped<IJobCancellationAccessor>(sp => sp.GetRequiredService<JobCancellationAccessor>());
            services.AddScoped<JobProgressAccessor>();
            services.AddScoped<IJobProgressAccessor>(sp => sp.GetRequiredService<JobProgressAccessor>());
            services.AddScoped<JobChainContextAccessor>();
            services.AddScoped<IJobChainContextAccessor>(sp => sp.GetRequiredService<JobChainContextAccessor>());
            services.AddTransient<StepJob>();
            services.AddTransient<FinallyStepJob>();
            _provider = services.BuildServiceProvider();

            var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
            var buffer = new PersistenceBuffer(scopeFactory, NullLogger<PersistenceBuffer>.Instance, flushIntervalMs: 10);
            var concurrency = new ConcurrencyRegistry(new Dictionary<Type, int>(), new Dictionary<Type, string?>(), new Dictionary<string, int>());
            // No backoff, so a retry is ready as soon as the pool is signalled.
            var retry = new RetryPolicyResolver(new RetryPolicy { MaxRetries = maxRetries, BaseDelay = TimeSpan.Zero });
            ChainScopes = new ChainScopeRegistry(scopeFactory, NullLogger<ChainScopeRegistry>.Instance);
            var metrics = new QueueMetrics();
            var events = new QueueStateEventHandler();

            var orchestrator = new QueueOrchestrator(
                NullLogger<QueueOrchestrator>.Instance, buffer, scopeFactory, concurrency, retry,
                metrics, events, ChainScopes, maxTotalWorkers: 2);
            Scheduler = new QueueScheduler(orchestrator, ChainScopes, scopeFactory);

            Pool = new WorkerPool("ChainRetryPool", maxWorkers: 2, AcquisitionAttribute.LowestPriority, [typeof(StepJob), typeof(FinallyStepJob)], []);
            orchestrator.Initialize([], [Pool]);
            Pool.Start(_provider, orchestrator, metrics, events, ChainScopes);
        }

        public void Dispose()
        {
            Pool.Stop();
            ChainScopes.Dispose();
            _provider.Dispose();
        }
    }

    private static string NewName() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// Waits for the chain's scope to be let go of, which happens once nothing of it is left.
    /// </summary>
    private static async Task WaitForScopeCompleted(Harness harness, Guid chainId)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(_timeout);
        while (harness.ChainScopes.TryGetChainScope(chainId, out _))
            await Task.Delay(10, cts.Token);
    }

    #endregion

    #region Tests

    [Theory]
    [InlineData(FailMode.ThrowOnce)]
    [InlineData(FailMode.RequeueOnce)]
    public async Task Chain_StepRunsAgainInItsChain_AndTheChainCompletes(FailMode fail)
    {
        using var harness = new Harness();
        var (first, second, last) = (NewName(), NewName(), NewName());

        await harness.Scheduler.CreateJobChain()
            .Then<StepJob>(job => (job.Name, job.Fail) = (first, fail))
            .Then<StepJob>(job => job.Name = second)
            .Then<FinallyStepJob>(job => job.Name = last)
            .Enqueue();

        await Reached(first, 2);
        await Reached(second, 1);
        await Reached(last, 1);

        var runs = Runs(first).Concat(Runs(second)).Concat(Runs(last)).ToList();
        Assert.Equal(4, runs.Count);
        var chainId = Assert.IsType<Guid>(runs[0].ChainId);
        Assert.All(runs, run => Assert.Equal(new Run(chainId, runs[0].ScopeId), run));

        await WaitForScopeCompleted(harness, chainId);
    }

    [Fact]
    public async Task Chain_StepDiscarded_SkipsTheRestAndRunsFinally()
    {
        using var harness = new Harness(maxRetries: 1);
        var (first, second, last) = (NewName(), NewName(), NewName());

        await harness.Scheduler.CreateJobChain()
            .Then<StepJob>(job => (job.Name, job.Fail) = (first, FailMode.ThrowAlways))
            .Then<StepJob>(job => job.Name = second)
            .Then<FinallyStepJob>(job => job.Name = last)
            .Enqueue();

        await Reached(first, 2);
        await Reached(last, 1);

        var firstRuns = Runs(first).ToList();
        var chainId = Assert.IsType<Guid>(firstRuns[0].ChainId);
        Assert.All(firstRuns, run => Assert.Equal(chainId, run.ChainId));
        Assert.Equal(chainId, Assert.Single(Runs(last)).ChainId);
        Assert.Empty(Runs(second));

        await WaitForScopeCompleted(harness, chainId);
    }

    [Fact]
    public void TryRaisePriority_ToImmediate_KeepsTheChainAndActor()
    {
        var pool = new WorkerPool("PromotePool", maxWorkers: 1, AcquisitionAttribute.LowestPriority, [typeof(StepJob)], []);
        var job = new QueuedJob
        {
            Id = Guid.NewGuid(),
            JobType = typeof(StepJob).FullName + ", " + typeof(StepJob).Assembly.GetName().Name,
            JobKey = NewName(),
            QueuedAt = DateTimeOffset.UtcNow,
            ScheduledAt = DateTimeOffset.UtcNow.AddHours(1),
            ChainId = Guid.NewGuid(),
            IsChainFinally = true,
            ParentJobId = Guid.NewGuid(),
            Actor = new JobActor(7, "desktop"),
        };
        pool.AddToQueue(job);

        Assert.NotNull(pool.TryRaisePriority(job.Id, QueuePriority.Immediate));

        Assert.True(pool.RemoveFromQueue(job.Id, out var promoted));
        Assert.NotNull(promoted);
        Assert.Equal(int.MaxValue, promoted.Priority);
        Assert.Null(promoted.ScheduledAt);
        Assert.Equal(job.ChainId, promoted.ChainId);
        Assert.True(promoted.IsChainFinally);
        Assert.Equal(job.ParentJobId, promoted.ParentJobId);
        Assert.Equal(job.Actor, promoted.Actor);
    }

    #endregion
}
