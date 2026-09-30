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
/// The actor captured through <see cref="IJobActorAccessor"/> when a job is queued is stored with
/// it and restored around its execution, and the jobs it queues inherit it.
/// </summary>
public class JobActorTests
{
    #region Fixture

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// A stand-in for the host's actor: an async local the accessor captures and restores, and a
    /// switch for whether stored actors can be restored yet.
    /// </summary>
    public sealed class TestActors : IJobActorAccessor
    {
        private static readonly AsyncLocal<JobActor?> _current = new();

        private volatile bool _canRestore = true;

        public static JobActor? Current => _current.Value;

        public static IDisposable Begin(JobActor? actor)
        {
            var previous = _current.Value;
            _current.Value = actor;
            return new Scope(() => _current.Value = previous);
        }

        public JobActor? Capture() => _current.Value;

        public IDisposable Restore(JobActor? actor)
        {
            RestoredWhileUnable |= actor is not null && !_canRestore;
            return Begin(actor);
        }

        public bool RestoredWhileUnable { get; private set; }

        public bool CanRestore
        {
            get => _canRestore;
            set
            {
                _canRestore = value;
                CanRestoreChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? CanRestoreChanged;

        private sealed class Scope(Action dispose) : IDisposable
        {
            public void Dispose() => dispose();
        }
    }

    /// <summary>
    /// The actor each fixture job saw, by the job's name.
    /// </summary>
    private static readonly ConcurrentDictionary<string, TaskCompletionSource<JobActor?>> _seen = new();

    private static TaskCompletionSource<JobActor?> Seen(string name)
        => _seen.GetOrAdd(name, _ => new(TaskCreationOptions.RunContinuationsAsynchronously));

    public sealed class RecordJob(IQueueScheduler scheduler) : IQueueJob
    {
        public string Name { get; set; } = string.Empty;

        public string? Then { get; set; }

        public string TypeName => "Record";

        public string Title => Name;

        public async Task Process()
        {
            Seen(Name).TrySetResult(TestActors.Current);
            if (Then is { } next)
                await scheduler.RunAfterCurrent<RecordJob>(job => job.Name = next);
        }
    }

    #endregion

    #region Harness

    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider _provider;

        public WorkerPool Pool { get; }

        public QueueScheduler Scheduler { get; }

        public TestActors Actors { get; } = new();

        public ConcurrentDictionary<Guid, QueuedJob> Stored { get; } = new();

        public Mock<IJobRepository> Repository { get; } = new();

        /// <summary>
        /// Starts the pool, as at start-up, with <paramref name="persisted"/> loaded back.
        /// </summary>
        /// <param name="canRestore">Whether stored actors can be restored when the pool starts.</param>
        /// <param name="persisted">The jobs loaded back from the queue's database.</param>
        public Harness(bool canRestore = true, params QueuedJob[] persisted)
        {
            Actors.CanRestore = canRestore;
            var table = Stored;
            var repo = Repository;
            repo.Setup(r => r.InsertBatchAsync(It.IsAny<IReadOnlyCollection<QueuedJob>>(), It.IsAny<CancellationToken>()))
                .Callback((IReadOnlyCollection<QueuedJob> jobs, CancellationToken _) =>
                {
                    foreach (var job in jobs)
                        table[job.Id] = job;
                })
                .Returns(Task.CompletedTask);
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
            services.AddSingleton<IJobActorAccessor>(Actors);
            services.AddSingleton<IQueueScheduler>(_ => Scheduler!);
            services.AddScoped<JobCancellationAccessor>();
            services.AddScoped<IJobCancellationAccessor>(sp => sp.GetRequiredService<JobCancellationAccessor>());
            services.AddScoped<JobProgressAccessor>();
            services.AddScoped<IJobProgressAccessor>(sp => sp.GetRequiredService<JobProgressAccessor>());
            services.AddScoped<JobChainContextAccessor>();
            services.AddScoped<IJobChainContextAccessor>(sp => sp.GetRequiredService<JobChainContextAccessor>());
            services.AddTransient<RecordJob>();
            _provider = services.BuildServiceProvider();

            var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
            var buffer = new PersistenceBuffer(scopeFactory, NullLogger<PersistenceBuffer>.Instance, flushIntervalMs: 10);
            var concurrency = new ConcurrencyRegistry(new Dictionary<Type, int>(), new Dictionary<Type, string?>(), new Dictionary<string, int>());
            var retry = new RetryPolicyResolver(new RetryPolicy { MaxRetries = 3, BaseDelay = TimeSpan.FromHours(1) });
            var chainScopes = new ChainScopeRegistry(scopeFactory, NullLogger<ChainScopeRegistry>.Instance);
            var metrics = new QueueMetrics();
            var events = new QueueStateEventHandler();

            var orchestrator = new QueueOrchestrator(
                NullLogger<QueueOrchestrator>.Instance, buffer, scopeFactory, concurrency, retry,
                metrics, events, chainScopes, maxTotalWorkers: 2);
            Scheduler = new QueueScheduler(orchestrator, chainScopes, scopeFactory, Actors);

            Pool = new WorkerPool("ActorPool", maxWorkers: 2, AcquisitionAttribute.LowestPriority, [typeof(RecordJob)], []);
            orchestrator.Initialize([.. persisted], [Pool]);

            // The workers start inside an actor's flow, which none of them may keep.
            using (TestActors.Begin(new JobActor(99, "pool-starter")))
                Pool.Start(_provider, orchestrator, metrics, events, chainScopes);
        }

        public void Dispose()
        {
            Pool.Stop();
            _provider.Dispose();
        }
    }

    private static string NewName() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// A <see cref="RecordJob"/> as loaded back from the queue's database.
    /// </summary>
    /// <param name="name">The job's name.</param>
    /// <param name="actor">The actor stored with it.</param>
    /// <returns>The stored job.</returns>
    private static QueuedJob Persisted(string name, JobActor? actor)
    {
        var job = new RecordJob(null!) { Name = name };
        return new QueuedJob
        {
            Id = Guid.NewGuid(),
            JobType = JobTypeNames.Stored(typeof(RecordJob)),
            JobKey = JobKeyBuilder.BuildFor(job),
            JobDataJson = JobDataSerializer.Serialize(job),
            QueuedAt = DateTimeOffset.UtcNow,
            Actor = actor,
        };
    }

    #endregion

    #region Tests

    [Fact]
    public async Task Enqueue_StoresTheActorAndRestoresItForTheJob()
    {
        using var harness = new Harness();
        var name = NewName();
        var actor = new JobActor(7, "desktop");

        using (TestActors.Begin(actor))
            await harness.Scheduler.Enqueue<RecordJob>(job => job.Name = name, ct: TestContext.Current.CancellationToken);

        Assert.Equal(actor, await Seen(name).Task.WaitAsync(_timeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Enqueue_WritesTheUserAndDeviceToTheRow()
    {
        using var harness = new Harness();
        var name = NewName();

        // Held back, so the row is written before the job could run and be deleted.
        using (TestActors.Begin(new JobActor(7, "desktop")))
            await harness.Scheduler.Enqueue<RecordJob>(job => job.Name = name, scheduledAt: DateTimeOffset.UtcNow.AddHours(1), ct: TestContext.Current.CancellationToken);

        var stored = await WaitForStored(harness, name);
        Assert.Equal(7, stored.ActorUserId);
        Assert.Equal("desktop", stored.ActorDeviceName);
    }

    [Fact]
    public async Task Enqueue_WithoutActor_RunsForNoOne()
    {
        using var harness = new Harness();
        var name = NewName();

        await harness.Scheduler.Enqueue<RecordJob>(job => job.Name = name, ct: TestContext.Current.CancellationToken);

        Assert.Null(await Seen(name).Task.WaitAsync(_timeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RunAfterCurrent_InheritsTheActor()
    {
        using var harness = new Harness();
        var (parent, child) = (NewName(), NewName());
        var actor = new JobActor(9, "tablet");

        using (TestActors.Begin(actor))
            await harness.Scheduler.Enqueue<RecordJob>(job => (job.Name, job.Then) = (parent, child), ct: TestContext.Current.CancellationToken);

        Assert.Equal(actor, await Seen(parent).Task.WaitAsync(_timeout, TestContext.Current.CancellationToken));
        Assert.Equal(actor, await Seen(child).Task.WaitAsync(_timeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Chain_EveryStepRunsForTheActor()
    {
        using var harness = new Harness();
        var (first, second) = (NewName(), NewName());
        var actor = new JobActor(10, "tv");

        using (TestActors.Begin(actor))
        {
            await harness.Scheduler.CreateJobChain()
                .Then<RecordJob>(job => job.Name = first)
                .Then<RecordJob>(job => job.Name = second)
                .Enqueue();
        }

        Assert.Equal(actor, await Seen(first).Task.WaitAsync(_timeout, TestContext.Current.CancellationToken));
        Assert.Equal(actor, await Seen(second).Task.WaitAsync(_timeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Persisted_WithActor_WaitsUntilTheActorCanBeRestored()
    {
        var (held, free) = (NewName(), NewName());
        var actor = new JobActor(7, "desktop");
        using var harness = new Harness(canRestore: false, Persisted(held, actor), Persisted(free, null));

        // The job without an actor is not held, and runs for no one.
        Assert.Null(await Seen(free).Task.WaitAsync(_timeout, TestContext.Current.CancellationToken));

        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.False(Seen(held).Task.IsCompleted);
        Assert.Equal(1, harness.Pool.BlockedCount);
        Assert.Equal(0, harness.Pool.RunnableCount());

        harness.Actors.CanRestore = true;

        Assert.Equal(actor, await Seen(held).Task.WaitAsync(_timeout, TestContext.Current.CancellationToken));
        Assert.False(harness.Actors.RestoredWhileUnable);
        harness.Repository.Verify(r => r.UpdateRetryAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static async Task<QueuedJob> WaitForStored(Harness harness, string name)
    {
        using var cts = new CancellationTokenSource(_timeout);
        while (true)
        {
            if (harness.Stored.Values.FirstOrDefault(job => job.JobDataJson?.Contains(name) is true) is { } job)
                return job;
            await Task.Delay(10, cts.Token);
        }
    }

    #endregion
}
