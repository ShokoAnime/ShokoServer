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
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Chain;
using Shoko.QueueProcessor.Events;
using Shoko.QueueProcessor.Orchestration;
using Shoko.QueueProcessor.Scheduling;
using Shoko.QueueProcessor.Storage;
using Shoko.QueueProcessor.Workers;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>
/// Tests for removing waiting jobs, cancelling running ones and reading back their progress,
/// through real <see cref="Worker"/> threads over a real DI container with the repositories
/// mocked out.
/// </summary>
public class JobCancellationTests
{
    #region Fixture Jobs

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// What a fixture job did, and the gates a test holds it on, looked up by the job's name.
    /// </summary>
    public sealed class Probe
    {
        private static readonly ConcurrentDictionary<string, Probe> _all = new();

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Observed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Runs;

        public static Probe For(string name) => _all.GetOrAdd(name, _ => new Probe());
    }

    /// <summary>
    /// Waits on its token; when it is cancelled, waits for the test to release it, then stops by
    /// rethrowing the <see cref="OperationCanceledException"/>.
    /// </summary>
    public sealed class CooperativeJob(IJobCancellationAccessor cancellation) : IQueueJob
    {
        public string Name { get; set; } = string.Empty;

        public string TypeName => nameof(CooperativeJob);

        public string Title => Name;

        public async Task Process()
        {
            var probe = Probe.For(Name);
            Interlocked.Increment(ref probe.Runs);
            probe.Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                probe.Observed.TrySetResult();
                await probe.Release.Task;
                throw;
            }
        }
    }

    /// <summary>
    /// Takes the token but only looks at it once released: then completes or fails, as
    /// <see cref="Fail"/> says, as a job that finished before noticing would.
    /// </summary>
    public sealed class FinishAnywayJob(IJobCancellationAccessor cancellation) : IQueueJob
    {
        public string Name { get; set; } = string.Empty;

        public bool Fail { get; set; }

        public string TypeName => nameof(FinishAnywayJob);

        public string Title => Name;

        public async Task Process()
        {
            var probe = Probe.For(Name);
            Interlocked.Increment(ref probe.Runs);
            probe.Started.TrySetResult();
            await probe.Release.Task;
            _ = cancellation.Token;
            probe.Finished.TrySetResult();
            if (Fail)
                throw new InvalidOperationException("Failed for another reason.");
        }
    }

    /// <summary>
    /// Has no cancellation token, so it cannot be cancelled while it runs.
    /// </summary>
    public sealed class StubbornJob : IQueueJob
    {
        public string Name { get; set; } = string.Empty;

        public string TypeName => nameof(StubbornJob);

        public string Title => Name;

        public async Task Process()
        {
            var probe = Probe.For(Name);
            Interlocked.Increment(ref probe.Runs);
            probe.Started.TrySetResult();
            await probe.Release.Task;
            probe.Finished.TrySetResult();
        }
    }

    /// <summary>
    /// Reports 0 to 100 in steps of 0.1, then waits for the test to release it.
    /// </summary>
    public sealed class ReportingJob(IJobProgressAccessor progress) : IQueueJob
    {
        public string Name { get; set; } = string.Empty;

        public decimal StopAt { get; set; } = 100m;

        public string TypeName => nameof(ReportingJob);

        public string Title => Name;

        public async Task Process()
        {
            var probe = Probe.For(Name);
            for (var value = 0m; value <= StopAt; value += 0.1m)
                progress.Progress.Report(value);
            probe.Started.TrySetResult();
            await probe.Release.Task;
        }
    }

    /// <summary>
    /// A chain step that counts its runs.
    /// </summary>
    public sealed class StepJob : IQueueJob
    {
        public string Name { get; set; } = string.Empty;

        public string TypeName => nameof(StepJob);

        public string Title => Name;

        public Task Process()
        {
            var probe = Probe.For(Name);
            Interlocked.Increment(ref probe.Runs);
            probe.Finished.TrySetResult();
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// A chain step that runs even when the chain is aborted.
    /// </summary>
    [ChainFinally]
    public sealed class FinallyStepJob : IQueueJob
    {
        public string Name { get; set; } = string.Empty;

        public string TypeName => nameof(FinallyStepJob);

        public string Title => Name;

        public Task Process()
        {
            var probe = Probe.For(Name);
            Interlocked.Increment(ref probe.Runs);
            probe.Finished.TrySetResult();
            return Task.CompletedTask;
        }
    }

    #endregion

    #region Harness

    private static string NewName() => Guid.NewGuid().ToString("N");

    private static string KeyOf<T>(string name, Action<T>? configure = null) where T : class, IQueueJob
        => JobKeyBuilder<T>.Create().UsingJobData(job =>
        {
            typeof(T).GetProperty("Name")!.SetValue(job, name);
            configure?.Invoke(job);
        }).Build();

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(_timeout);
        while (!condition())
            await Task.Delay(10, cts.Token);
    }

    /// <summary>
    /// A single pool of real workers handling every fixture job, with the job and chain context
    /// repositories mocked out.
    /// </summary>
    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider _provider;

        public WorkerPool Pool { get; }

        public QueueOrchestrator Orchestrator { get; }

        public QueueScheduler Scheduler { get; }

        public QueueStateEventHandler Events { get; } = new();

        public ConcurrentQueue<(ChainStatus Status, IReadOnlyList<JobOutcome> Outcomes)> ChainSaves { get; } = new();

        public PersistenceBuffer Buffer { get; }

        /// <summary>
        /// The job table, as the mocked repository writes it.
        /// </summary>
        public ConcurrentDictionary<Guid, QueuedJob> Stored { get; }

        public Harness(bool start = true, ConcurrentDictionary<Guid, QueuedJob>? stored = null)
        {
            Stored = stored ?? new();
            var table = Stored;
            Type[] jobTypes = [typeof(CooperativeJob), typeof(FinishAnywayJob), typeof(StubbornJob), typeof(ReportingJob), typeof(StepJob), typeof(FinallyStepJob)];

            var repo = new Mock<IJobRepository>();
            repo.Setup(r => r.InsertBatchAsync(It.IsAny<IReadOnlyCollection<QueuedJob>>(), It.IsAny<CancellationToken>()))
                .Callback((IReadOnlyCollection<QueuedJob> jobs, CancellationToken _) =>
                {
                    foreach (var job in jobs)
                        table[job.Id] = Copy(job);
                })
                .Returns(Task.CompletedTask);
            repo.Setup(r => r.DeleteBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .Callback((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                {
                    foreach (var id in ids)
                        table.TryRemove(id, out QueuedJob? _);
                })
                .Returns(Task.CompletedTask);
            repo.Setup(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            repo.Setup(r => r.UpdateRetryAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            repo.Setup(r => r.ActivateChainChildrenAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .Callback((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                {
                    foreach (var id in ids)
                        if (table.TryGetValue(id, out var job))
                            job.ParentJobId = null;
                })
                .Returns(Task.CompletedTask);
            repo.Setup(r => r.ReparentChainChildrenAsync(It.IsAny<IReadOnlyCollection<(Guid, Guid)>>(), It.IsAny<CancellationToken>()))
                .Callback((IReadOnlyCollection<(Guid Id, Guid ParentJobId)> updates, CancellationToken _) =>
                {
                    foreach (var (id, parentJobId) in updates)
                        if (table.TryGetValue(id, out var job))
                            job.ParentJobId = parentJobId;
                })
                .Returns(Task.CompletedTask);

            var chainRepo = new Mock<IJobChainContextRepository>();
            chainRepo.Setup(r => r.GetOrCreateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid id, CancellationToken _) => new JobChainContext(id));
            chainRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((JobChainContext?)null);
            chainRepo.Setup(r => r.SaveAsync(It.IsAny<JobChainContext>(), It.IsAny<CancellationToken>()))
                .Callback((JobChainContext ctx, CancellationToken _) => ChainSaves.Enqueue((ctx.Status, [.. ctx.GetAllOutcomes()])))
                .Returns(Task.CompletedTask);
            chainRepo.Setup(r => r.AddOutcomesAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<JobOutcome>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            chainRepo.Setup(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var services = new ServiceCollection();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddSingleton(repo.Object);
            services.AddSingleton(chainRepo.Object);
            services.AddScoped<JobCancellationAccessor>();
            services.AddScoped<IJobCancellationAccessor>(sp => sp.GetRequiredService<JobCancellationAccessor>());
            services.AddScoped<JobProgressAccessor>();
            services.AddScoped<IJobProgressAccessor>(sp => sp.GetRequiredService<JobProgressAccessor>());
            services.AddScoped<JobChainContextAccessor>();
            services.AddScoped<IJobChainContextAccessor>(sp => sp.GetRequiredService<JobChainContextAccessor>());
            foreach (var jobType in jobTypes)
                services.AddTransient(jobType);
            _provider = services.BuildServiceProvider();

            var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
            var buffer = Buffer = new PersistenceBuffer(scopeFactory, NullLogger<PersistenceBuffer>.Instance, flushIntervalMs: int.MaxValue);
            var concurrency = new ConcurrencyRegistry(new Dictionary<Type, int>(), new Dictionary<Type, string?>(), new Dictionary<string, int>());
            // Retries are spaced an hour apart, so a retried job waits instead of running again.
            var retry = new RetryPolicyResolver(new RetryPolicy { MaxRetries = 3, BaseDelay = TimeSpan.FromHours(1) });
            var chainScopes = new ChainScopeRegistry(scopeFactory, NullLogger<ChainScopeRegistry>.Instance);
            var metrics = new QueueMetrics();

            Orchestrator = new QueueOrchestrator(
                NullLogger<QueueOrchestrator>.Instance, buffer, scopeFactory, concurrency, retry,
                metrics, Events, chainScopes, maxTotalWorkers: 2);
            Scheduler = new QueueScheduler(Orchestrator, chainScopes, scopeFactory);

            Pool = new WorkerPool("TestPool", maxWorkers: 2, AcquisitionAttribute.LowestPriority, jobTypes, []);
            // Loaded as a restart would, from copies of the stored rows.
            Orchestrator.Initialize(Stored.Values.Select(Copy).OrderBy(job => job.QueuedAt).ToList(), [Pool]);
            if (start)
                Pool.Start(_provider, Orchestrator, metrics, Events, chainScopes);
        }

        private static QueuedJob Copy(QueuedJob job) => new()
        {
            Id = job.Id,
            JobType = job.JobType,
            JobKey = job.JobKey,
            JobDataJson = job.JobDataJson,
            Priority = job.Priority,
            QueuedAt = job.QueuedAt,
            ScheduledAt = job.ScheduledAt,
            RetryCount = job.RetryCount,
            ChainId = job.ChainId,
            IsChainFinally = job.IsChainFinally,
            ParentJobId = job.ParentJobId,
        };

        public ExecutingEntry? Executing(string key)
            => Orchestrator.GetExecuting().Where(entry => entry.JobKey == key).Select(entry => (ExecutingEntry?)entry).FirstOrDefault();

        public void Dispose()
        {
            Pool.Stop();
            _provider.Dispose();
        }
    }

    #endregion

    #region Waiting Jobs

    [Fact]
    public async Task Cancel_UnknownKey_ReturnsNotFound()
    {
        using var harness = new Harness(start: false);

        Assert.Equal(JobCancellationResult.NotFound, await harness.Scheduler.Cancel("nothing", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cancel_WaitingJob_RemovesItAndFreesItsKey()
    {
        using var harness = new Harness(start: false);
        var name = NewName();
        var key = KeyOf<StepJob>(name);
        var removed = new List<string>();
        harness.Events.QueueItemsRemoved += (_, e) => removed.AddRange(e.RemovedKeys);
        await harness.Scheduler.Enqueue<StepJob>(job => job.Name = name, ct: TestContext.Current.CancellationToken);
        Assert.True(harness.Scheduler.IsQueued(key));

        var result = await harness.Scheduler.Cancel(key, TestContext.Current.CancellationToken);

        Assert.Equal(JobCancellationResult.Removed, result);
        Assert.False(harness.Scheduler.IsQueued(key));
        Assert.Equal(0, harness.Pool.WaitingCount);
        Assert.Equal([key], removed);

        // The key is free, so the same job can be queued again.
        await harness.Scheduler.Enqueue<StepJob>(job => job.Name = name, ct: TestContext.Current.CancellationToken);
        Assert.True(harness.Scheduler.IsQueued(key));
        Assert.Equal(1, harness.Pool.WaitingCount);
    }

    [Fact]
    public async Task Remove_RunningJob_LeavesItAlone()
    {
        using var harness = new Harness();
        var name = NewName();
        var key = KeyOf<StubbornJob>(name);
        await harness.Scheduler.Enqueue<StubbornJob>(job => job.Name = name, ct: TestContext.Current.CancellationToken);
        await Probe.For(name).Started.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Equal(JobCancellationResult.Running, await harness.Orchestrator.RemoveAsync(key, TestContext.Current.CancellationToken));
        Assert.True(harness.Scheduler.IsQueued(key));

        Probe.For(name).Release.TrySetResult();
        await WaitUntil(() => !harness.Scheduler.IsQueued(key));
    }

    [Fact]
    public async Task Remove_WaitingChainStep_AbortsTheRestButKeepsFinallyJobsAfterTheParent()
    {
        using var harness = new Harness();
        var (first, second, last) = (NewName(), NewName(), NewName());
        await harness.Scheduler.CreateJobChain()
            .Then<StubbornJob>(job => job.Name = first)
            .Then<StepJob>(job => job.Name = second)
            .Then<FinallyStepJob>(job => job.Name = last)
            .Enqueue();
        await Probe.For(first).Started.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        var result = await harness.Scheduler.Cancel(KeyOf<StepJob>(second), TestContext.Current.CancellationToken);

        Assert.Equal(JobCancellationResult.Removed, result);
        Assert.False(harness.Scheduler.IsQueued(KeyOf<StepJob>(second)));
        // The finally job still waits for the running first step.
        Assert.True(harness.Scheduler.IsQueued(KeyOf<FinallyStepJob>(last)));
        Assert.Equal(0, Probe.For(last).Runs);

        Probe.For(first).Release.TrySetResult();
        await Probe.For(last).Finished.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Equal(0, Probe.For(second).Runs);
        Assert.Equal(1, Probe.For(last).Runs);
        Assert.Contains(harness.ChainSaves, save => save.Status == ChainStatus.Aborted
            && save.Outcomes.Any(outcome => outcome.JobKey == KeyOf<StepJob>(second) && outcome.Status == JobOutcomeStatus.Cancelled));
    }

    [Fact]
    public async Task Remove_RacingAnEnqueueStillAddingToItsPool_DropsTheLateJob()
    {
        using var harness = new Harness(start: false);
        var name = NewName();
        var key = KeyOf<StepJob>(name);
        await harness.Scheduler.Enqueue<StepJob>(job => job.Name = name, ct: TestContext.Current.CancellationToken);
        var late = Assert.Single(harness.Orchestrator.GetWaiting(10, 0));

        // The removal lands between the enqueue claiming the key and the job reaching its pool.
        Assert.True(harness.Pool.RemoveFromQueue(late.Id));
        Assert.Equal(JobCancellationResult.Removed, await harness.Orchestrator.RemoveAsync(key, TestContext.Current.CancellationToken));
        harness.Pool.AddToQueue(late);

        // The same key is queued anew, and only that job runs.
        await harness.Scheduler.Enqueue<StepJob>(job => job.Name = name, ct: TestContext.Current.CancellationToken);
        var fresh = harness.Orchestrator.GetWaiting(10, 0).Single(job => job.Id != late.Id);
        var acquired = harness.Pool.TryAcquire();

        Assert.Equal(fresh.Id, acquired?.Id);
        Assert.Equal(0, harness.Pool.WaitingCount);
        Assert.Null(harness.Pool.TryAcquire());
        harness.Orchestrator.OnComplete(fresh.Id);
        Assert.False(harness.Scheduler.IsQueued(key));
    }

    [Fact]
    public async Task Remove_WaitingChainStep_KeepsFinallyJobsAfterTheParentAcrossARestart()
    {
        var stored = new ConcurrentDictionary<Guid, QueuedJob>();
        var (first, second, last) = (NewName(), NewName(), NewName());
        using (var harness = new Harness(start: false, stored))
        {
            await harness.Scheduler.CreateJobChain()
                .Then<StepJob>(job => job.Name = first)
                .Then<StepJob>(job => job.Name = second)
                .Then<FinallyStepJob>(job => job.Name = last)
                .Enqueue();
            await harness.Buffer.FlushNowAsync(TestContext.Current.CancellationToken);

            Assert.Equal(JobCancellationResult.Removed, await harness.Orchestrator.RemoveAsync(KeyOf<StepJob>(second), TestContext.Current.CancellationToken));
            await harness.Buffer.FlushNowAsync(TestContext.Current.CancellationToken);
        }

        var firstRow = stored.Values.Single(job => job.JobKey == KeyOf<StepJob>(first));
        Assert.Equal(firstRow.Id, stored.Values.Single(job => job.JobKey == KeyOf<FinallyStepJob>(last)).ParentJobId);

        using var restarted = new Harness(start: false, stored);
        Assert.True(restarted.Scheduler.IsQueued(KeyOf<FinallyStepJob>(last)));
        Assert.Equal(firstRow.Id, Assert.Single(restarted.Orchestrator.GetWaiting(10, 0)).Id);
    }

    [Fact]
    public async Task Cancel_WaitingJobSomeoneAwaits_CancelsTheirWait()
    {
        using var harness = new Harness(start: false);
        var name = NewName();
        Exception? reported = null;
        var waiting = harness.Scheduler.EnqueueImmediate<StepJob>(job => job.Name = name, error =>
        {
            reported = error;
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        Assert.Equal(JobCancellationResult.Removed, await harness.Scheduler.Cancel(KeyOf<StepJob>(name), TestContext.Current.CancellationToken));

        // With a callback, the wait ends when the callback does, and the callback hears of the cancel.
        await waiting.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        Assert.IsType<OperationCanceledException>(reported);
    }

    #endregion

    #region Running Jobs

    [Fact]
    public async Task Cancel_RunningCooperativeJob_ShowsRequestedThenEndsCancelledWithoutRetry()
    {
        using var harness = new Harness();
        var name = NewName();
        var key = KeyOf<CooperativeJob>(name);
        var requested = new List<QueueItem>();
        harness.Events.JobCancellationRequested += (_, e) => requested.Add(e.Item);
        await harness.Scheduler.Enqueue<CooperativeJob>(job => job.Name = name, ct: TestContext.Current.CancellationToken);
        await Probe.For(name).Started.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        Assert.True(harness.Executing(key)?.IsCancellable);
        Assert.False(harness.Executing(key)?.CancellationRequested);

        var result = await harness.Scheduler.Cancel(key, TestContext.Current.CancellationToken);

        Assert.Equal(JobCancellationResult.CancellationRequested, result);
        await Probe.For(name).Observed.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.True(harness.Executing(key)?.CancellationRequested);
        Assert.True(Assert.Single(requested).CancellationRequested);
        Assert.Equal(JobCancellationResult.CancellationRequested, await harness.Scheduler.Cancel(key, TestContext.Current.CancellationToken));
        Assert.Single(requested);

        Probe.For(name).Release.TrySetResult();
        await WaitUntil(() => !harness.Scheduler.IsQueued(key));

        // Ended as cancelled: not retried, and its key is free.
        Assert.Null(harness.Executing(key));
        Assert.Equal(0, harness.Pool.WaitingCount);
        Assert.Equal(1, Probe.For(name).Runs);
    }

    [Fact]
    public async Task Cancel_RunningJobSomeoneAwaits_CancelsTheirWait()
    {
        using var harness = new Harness();
        var name = NewName();
        var waiting = harness.Scheduler.EnqueueImmediate<CooperativeJob>(job => job.Name = name, ct: TestContext.Current.CancellationToken);
        await Probe.For(name).Started.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        await harness.Scheduler.Cancel(KeyOf<CooperativeJob>(name), TestContext.Current.CancellationToken);
        Probe.For(name).Release.TrySetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(_timeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cancel_RunningJobThatFinishesAnyway_EndsAsCompleted()
    {
        using var harness = new Harness();
        var name = NewName();
        var key = KeyOf<FinishAnywayJob>(name);
        Exception? reported = null;
        var waiting = harness.Scheduler.EnqueueImmediate<FinishAnywayJob>(job => job.Name = name, error =>
        {
            reported = error;
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);
        await Probe.For(name).Started.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Equal(JobCancellationResult.CancellationRequested, await harness.Scheduler.Cancel(key, TestContext.Current.CancellationToken));
        Probe.For(name).Release.TrySetResult();

        await waiting.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        await WaitUntil(() => !harness.Scheduler.IsQueued(key));
        Assert.Null(reported);
        Assert.Equal(0, harness.Pool.WaitingCount);
        Assert.Equal(1, Probe.For(name).Runs);
    }

    [Fact]
    public async Task Cancel_RunningJobThatFailsForAnotherReason_EndsAsFailedWithoutRetry()
    {
        using var harness = new Harness();
        var name = NewName();
        var key = KeyOf<FinishAnywayJob>(name, job => job.Fail = true);
        Exception? reported = null;
        var waiting = harness.Scheduler.EnqueueImmediate<FinishAnywayJob>(job => (job.Name, job.Fail) = (name, true), error =>
        {
            reported = error;
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);
        await Probe.For(name).Started.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Equal(JobCancellationResult.CancellationRequested, await harness.Scheduler.Cancel(key, TestContext.Current.CancellationToken));
        Probe.For(name).Release.TrySetResult();
        await waiting.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        await WaitUntil(() => !harness.Scheduler.IsQueued(key));

        // The request cannot be taken back: not re-queued on the retry ladder, and its key is free.
        Assert.IsType<InvalidOperationException>(reported);
        Assert.Null(harness.Executing(key));
        Assert.Equal(0, harness.Pool.WaitingCount);
        Assert.Empty(harness.Orchestrator.GetWaiting(10, 0));
        Assert.Equal(1, Probe.For(name).Runs);
    }

    [Fact]
    public async Task Cancel_RunningJobWithoutAToken_IsNotCancellable()
    {
        using var harness = new Harness();
        var name = NewName();
        var key = KeyOf<StubbornJob>(name);
        await harness.Scheduler.Enqueue<StubbornJob>(job => job.Name = name, ct: TestContext.Current.CancellationToken);
        await Probe.For(name).Started.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.False(harness.Executing(key)?.IsCancellable);
        Assert.Equal(JobCancellationResult.NotCancellable, await harness.Scheduler.Cancel(key, TestContext.Current.CancellationToken));
        Assert.False(harness.Executing(key)?.CancellationRequested);

        Probe.For(name).Release.TrySetResult();
        await Probe.For(name).Finished.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The step ends as cancelled, or as failed when it failed for another reason first.
    /// </summary>
    /// <param name="fails">Whether the step fails for another reason once released.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancel_RunningChainStep_AbortsTheChainAndRunsFinallyJobs(bool fails)
    {
        using var harness = new Harness();
        var (first, second, last) = (NewName(), NewName(), NewName());
        IJobChainBuilder chain = harness.Scheduler.CreateJobChain();
        chain = fails
            ? chain.Then<FinishAnywayJob>(job => (job.Name, job.Fail) = (first, true))
            : chain.Then<CooperativeJob>(job => job.Name = first);
        await chain
            .Then<StepJob>(job => job.Name = second)
            .Then<FinallyStepJob>(job => job.Name = last)
            .Enqueue();
        await Probe.For(first).Started.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        var key = fails ? KeyOf<FinishAnywayJob>(first, job => job.Fail = true) : KeyOf<CooperativeJob>(first);
        var status = fails ? JobOutcomeStatus.Failed : JobOutcomeStatus.Cancelled;

        Assert.Equal(JobCancellationResult.CancellationRequested, await harness.Scheduler.Cancel(key, TestContext.Current.CancellationToken));
        Probe.For(first).Release.TrySetResult();
        await Probe.For(last).Finished.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Equal(1, Probe.For(first).Runs);
        Assert.Equal(0, Probe.For(second).Runs);
        Assert.False(harness.Scheduler.IsQueued(KeyOf<StepJob>(second)));
        Assert.False(harness.Scheduler.IsQueued(key));
        Assert.Contains(harness.ChainSaves, save => save.Outcomes.Any(outcome => outcome.JobKey == key && outcome.Status == status));
        Assert.Contains(harness.ChainSaves, save => save.Status == ChainStatus.Aborted);
    }

    [Fact]
    public async Task Cancel_BeforeTheWorkerAttachesItsToken_CancelsTheTokenOnAttach()
    {
        using var harness = new Harness(start: false);
        var name = NewName();
        var key = KeyOf<CooperativeJob>(name);
        await harness.Scheduler.Enqueue<CooperativeJob>(job => job.Name = name, ct: TestContext.Current.CancellationToken);
        var acquired = harness.Pool.TryAcquire();
        Assert.NotNull(acquired);

        Assert.Equal(JobCancellationResult.CancellationRequested, await harness.Scheduler.Cancel(key, TestContext.Current.CancellationToken));

        using var cancellation = new CancellationTokenSource();
        var state = await harness.Orchestrator.BeginExecutionAsync(acquired.Id, cancellation);
        Assert.NotNull(state);
        Assert.True(cancellation.IsCancellationRequested);
    }

    [Fact]
    public async Task Shutdown_RunningCooperativeJob_IsRequeuedUnchanged()
    {
        using var harness = new Harness();
        var name = NewName();
        var key = KeyOf<CooperativeJob>(name);
        await harness.Scheduler.Enqueue<CooperativeJob>(job => job.Name = name, ct: TestContext.Current.CancellationToken);
        await Probe.For(name).Started.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        harness.Pool.Stop();
        await Probe.For(name).Observed.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        Probe.For(name).Release.TrySetResult();
        await harness.Pool.WhenStoppedAsync().WaitAsync(_timeout, TestContext.Current.CancellationToken);

        var requeued = Assert.Single(harness.Orchestrator.GetWaiting(10, 0));
        Assert.Equal(key, requeued.JobKey);
        Assert.Equal(0, requeued.RetryCount);
        Assert.Null(requeued.ScheduledAt);
        Assert.True(harness.Scheduler.IsQueued(key));
    }

    #endregion

    #region Progress

    [Fact]
    public async Task Progress_ReportedByARunningJob_IsReadBackAndThrottled()
    {
        using var harness = new Harness();
        var name = NewName();
        var key = KeyOf<ReportingJob>(name);
        var events = new ConcurrentQueue<decimal>();
        harness.Events.JobProgressChanged += (_, e) =>
        {
            if (e.Key == key)
                events.Enqueue(e.Progress);
        };
        await harness.Scheduler.Enqueue<ReportingJob>(job => job.Name = name, ct: TestContext.Current.CancellationToken);
        await Probe.For(name).Started.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Equal(100m, harness.Executing(key)?.Progress);
        // A thousand reports, but only the first, the last and a few in between go out.
        Assert.InRange(events.Count, 2, 50);
        Assert.Equal(0m, events.First());
        Assert.Equal(100m, events.Last());

        Probe.For(name).Release.TrySetResult();
        await WaitUntil(() => !harness.Scheduler.IsQueued(key));
    }

    [Fact]
    public async Task Progress_OfAJobThatDoesNotReport_IsNull()
    {
        using var harness = new Harness();
        var name = NewName();
        var key = KeyOf<StubbornJob>(name);
        await harness.Scheduler.Enqueue<StubbornJob>(job => job.Name = name, ct: TestContext.Current.CancellationToken);
        await Probe.For(name).Started.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.NotNull(harness.Executing(key));
        Assert.Null(harness.Executing(key)?.Progress);

        Probe.For(name).Release.TrySetResult();
        await Probe.For(name).Finished.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Progress_HeldBackByTheThrottle_GoesOutOnceTheIntervalIsUp()
    {
        var reported = new ConcurrentQueue<decimal>();
        var state = new JobExecutionState(Guid.NewGuid(), "key", (_, value) => reported.Enqueue(value));

        state.Report(0m);
        state.Report(5m);
        Assert.Equal([0m], reported);

        // The job then spends a long time in one step: the 5 must not wait for another report.
        await WaitUntil(() => reported.Count == 2);
        Assert.Equal([0m, 5m], reported);
    }

    [Fact]
    public void Progress_IsClampedAndIgnoredOnceTheJobEnded()
    {
        var reported = new List<decimal>();
        var state = new JobExecutionState(Guid.NewGuid(), "key", (_, value) => reported.Add(value));

        state.Report(150m);
        Assert.Equal(100m, state.Progress);

        state.End();
        state.Report(10m);

        Assert.Equal(100m, state.Progress);
        Assert.Equal([100m], reported);
    }

    #endregion

    #region Capabilities

    [Fact]
    public void IsCancellable_FollowsTheConstructor()
    {
        Assert.True(JobCapabilities.IsCancellable(typeof(CooperativeJob)));
        Assert.False(JobCapabilities.IsCancellable(typeof(StubbornJob)));
        Assert.False(JobCapabilities.IsCancellable(typeof(ReportingJob)));
    }

    [Fact]
    public void IsCancellation_RecognisesWrappedCancellations()
    {
        Assert.True(Worker.IsCancellation(new OperationCanceledException()));
        Assert.True(Worker.IsCancellation(new TaskCanceledException()));
        Assert.True(Worker.IsCancellation(new InvalidOperationException("wrapped", new OperationCanceledException())));
        Assert.True(Worker.IsCancellation(new AggregateException(new TaskCanceledException())));
        Assert.False(Worker.IsCancellation(new InvalidOperationException()));
        Assert.False(Worker.IsCancellation(new AggregateException(new TaskCanceledException(), new InvalidOperationException())));
    }

    #endregion
}
