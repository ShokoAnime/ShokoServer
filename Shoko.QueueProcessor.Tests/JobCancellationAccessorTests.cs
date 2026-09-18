using System;
using System.Collections.Generic;
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
using Shoko.QueueProcessor.Storage;
using Shoko.QueueProcessor.Workers;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>
/// Tests for <see cref="JobCancellationAccessor"/>: the accessor itself, its DI registration,
/// and the end-to-end path where <see cref="Worker"/> stamps its pool's shutdown token onto the
/// job scope before <see cref="IQueueJob.Process"/> runs.
/// </summary>
public class JobCancellationAccessorTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    // ── The accessor on its own ───────────────────────────────────────────────

    [Fact]
    public void Token_BeforeSetCurrentToken_IsNone()
    {
        var accessor = new JobCancellationAccessor();

        Assert.Equal(CancellationToken.None, accessor.Token);
        Assert.False(accessor.Token.CanBeCanceled);
        Assert.False(accessor.Token.IsCancellationRequested);
    }

    [Fact]
    public void Token_AfterSetCurrentToken_ReturnsThatToken()
    {
        using var cts = new CancellationTokenSource();
        var accessor = new JobCancellationAccessor();

        accessor.SetCurrentToken(cts.Token);

        Assert.Equal(cts.Token, accessor.Token);
        Assert.True(accessor.Token.CanBeCanceled);
        Assert.False(accessor.Token.IsCancellationRequested);

        cts.Cancel();

        Assert.True(accessor.Token.IsCancellationRequested);
    }

    [Fact]
    public void SetCurrentToken_CalledAgain_ReplacesPreviousToken()
    {
        using var first = new CancellationTokenSource();
        using var second = new CancellationTokenSource();
        var accessor = new JobCancellationAccessor();

        accessor.SetCurrentToken(first.Token);
        accessor.SetCurrentToken(second.Token);
        first.Cancel();

        Assert.Equal(second.Token, accessor.Token);
        Assert.False(accessor.Token.IsCancellationRequested);
    }

    // ── DI registration ───────────────────────────────────────────────────────

    [Fact]
    public void AddQueueProcessor_ResolvesAccessorAsScoped_WithUnsetToken()
    {
        var services = new ServiceCollection();
        services.AddQueueProcessor(opts =>
        {
            opts.Provider = DatabaseProvider.SQLite;
            opts.ConnectionString = "Data Source=:memory:";
        });
        using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var asInterface = scope.ServiceProvider.GetRequiredService<IJobCancellationAccessor>();
        var asConcrete = scope.ServiceProvider.GetRequiredService<JobCancellationAccessor>();

        // Same instance within a scope, so the worker's stamp is visible to the job
        Assert.Same(asConcrete, asInterface);

        // Nothing stamped it: a valid token that can never be cancelled
        Assert.Equal(CancellationToken.None, asInterface.Token);
        Assert.False(asInterface.Token.CanBeCanceled);
        Assert.False(asInterface.Token.IsCancellationRequested);

        using var otherScope = provider.CreateScope();
        Assert.NotSame(asConcrete, otherScope.ServiceProvider.GetRequiredService<JobCancellationAccessor>());
    }

    // ── End-to-end through a real worker ──────────────────────────────────────

    [Fact]
    public async Task Worker_StampsPoolToken_JobObservesCancellationOnStop()
    {
        using var harness = new WorkerHarness(typeof(ShutdownObservingJob));
        harness.Enqueue(typeof(ShutdownObservingJob));

        await ShutdownObservingJob.Started.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        Assert.True(ShutdownObservingJob.CanBeCanceled);
        Assert.False(ShutdownObservingJob.CancelledOnEntry);

        harness.Pool.Stop();

        Assert.True(await ShutdownObservingJob.Observed.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken));
        await harness.Pool.WhenStoppedAsync().WaitAsync(_timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Worker_NormalRun_JobSeesUncancelledTokenAndCompletes()
    {
        using var harness = new WorkerHarness(typeof(UncancelledJob));
        harness.Enqueue(typeof(UncancelledJob));

        await UncancelledJob.Finished.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.True(UncancelledJob.CanBeCanceled);
        Assert.False(UncancelledJob.CancellationRequested);
        Assert.Equal(0, harness.Orchestrator.WaitingCount);

        harness.Pool.Stop();
        await harness.Pool.WhenStoppedAsync().WaitAsync(_timeout, TestContext.Current.CancellationToken);
    }

    // ── Fixture jobs ──────────────────────────────────────────────────────────

    /// <summary>Blocks on its token until the pool is stopped, then reports what it saw.</summary>
    public sealed class ShutdownObservingJob : IQueueJob
    {
        public static readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public static readonly TaskCompletionSource<bool> Observed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public static bool CanBeCanceled;
        public static bool CancelledOnEntry;

        private readonly IJobCancellationAccessor _cancellation;

        public ShutdownObservingJob(IJobCancellationAccessor cancellation) => _cancellation = cancellation;

        public string TypeName => nameof(ShutdownObservingJob);
        public string Title => "";
        public Dictionary<string, object> Details => [];

        public async Task Process()
        {
            var token = _cancellation.Token;
            CanBeCanceled = token.CanBeCanceled;
            CancelledOnEntry = token.IsCancellationRequested;
            Started.TrySetResult();

            try
            {
                await Task.Delay(Timeout.Infinite, token);
            }
            catch (OperationCanceledException)
            {
                // Polite cancellation: the job stops itself rather than throwing at the worker
            }

            Observed.TrySetResult(token.IsCancellationRequested);
        }
    }

    /// <summary>Runs to completion without cancellation and reports the token it was handed.</summary>
    public sealed class UncancelledJob : IQueueJob
    {
        public static readonly TaskCompletionSource Finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public static bool CanBeCanceled;
        public static bool CancellationRequested;

        private readonly IJobCancellationAccessor _cancellation;

        public UncancelledJob(IJobCancellationAccessor cancellation) => _cancellation = cancellation;

        public string TypeName => nameof(UncancelledJob);
        public string Title => "";
        public Dictionary<string, object> Details => [];

        public Task Process()
        {
            var token = _cancellation.Token;
            CanBeCanceled = token.CanBeCanceled;
            CancellationRequested = token.IsCancellationRequested;
            Finished.TrySetResult();
            return Task.CompletedTask;
        }
    }

    // ── Harness ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Boots a single real <see cref="WorkerPool"/> (and therefore real <see cref="Worker"/>
    /// threads) over a real DI container, with the job repository mocked out so nothing touches
    /// a database.
    /// </summary>
    private sealed class WorkerHarness : IDisposable
    {
        private readonly ServiceProvider _provider;

        public WorkerPool Pool { get; }

        public QueueOrchestrator Orchestrator { get; }

        public WorkerHarness(params Type[] jobTypes)
        {
            var repo = new Mock<IJobRepository>();
            repo.Setup(r => r.InsertBatchAsync(It.IsAny<IReadOnlyCollection<QueuedJob>>(), default)).Returns(Task.CompletedTask);
            repo.Setup(r => r.DeleteBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default)).Returns(Task.CompletedTask);
            repo.Setup(r => r.DeleteAsync(It.IsAny<Guid>(), default)).Returns(Task.CompletedTask);

            var services = new ServiceCollection();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddSingleton(repo.Object);
            services.AddScoped<JobCancellationAccessor>();
            services.AddScoped<IJobCancellationAccessor>(sp => sp.GetRequiredService<JobCancellationAccessor>());
            services.AddScoped<JobChainContextAccessor>();
            foreach (var jobType in jobTypes)
                services.AddTransient(jobType);
            _provider = services.BuildServiceProvider();

            var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
            var buffer = new PersistenceBuffer(scopeFactory, NullLogger<PersistenceBuffer>.Instance, flushIntervalMs: int.MaxValue);
            var concurrency = new ConcurrencyRegistry(new Dictionary<Type, int>(), new Dictionary<Type, string?>(), new Dictionary<string, int>());
            var retry = new RetryPolicyResolver(new RetryPolicy { MaxRetries = 0 });
            var chainScopes = new ChainScopeRegistry(scopeFactory, NullLogger<ChainScopeRegistry>.Instance);
            var metrics = new QueueMetrics();
            var events = new QueueStateEventHandler();

            Orchestrator = new QueueOrchestrator(
                NullLogger<QueueOrchestrator>.Instance, buffer, scopeFactory, concurrency, retry,
                metrics, events, chainScopes, maxTotalWorkers: 1);

            Pool = new WorkerPool("TestPool", maxWorkers: 1, AcquisitionAttribute.LowestPriority, jobTypes, []);
            Orchestrator.Initialize([], [Pool]);
            Pool.Start(_provider, Orchestrator, metrics, events, chainScopes);
        }

        public void Enqueue(Type jobType)
        {
            Pool.AddToQueue(new QueuedJob
            {
                Id = Guid.NewGuid(),
                JobType = jobType.FullName + ", " + jobType.Assembly.GetName().Name,
                JobKey = $"{jobType.Name}_{Guid.NewGuid()}",
                QueuedAt = DateTimeOffset.UtcNow,
            });
            Pool.Signal();
        }

        public void Dispose()
        {
            Pool.Stop();
            _provider.Dispose();
        }
    }
}
