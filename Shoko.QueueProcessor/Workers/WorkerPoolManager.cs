using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Chain;
using Shoko.QueueProcessor.Events;
using Shoko.QueueProcessor.Orchestration;
using Shoko.QueueProcessor.Storage;

namespace Shoko.QueueProcessor.Workers;

/// <summary>
/// <see cref="IHostedService"/> that bootstraps the queue on startup and tears it down cleanly.
/// Startup sequence:
/// <list type="number">
///   <item>Migrate / create the database schema</item>
///   <item>Load all persisted jobs via <see cref="IJobRepository.LoadAllAsync"/></item>
///   <item>Run <see cref="PoolDiscovery"/> to build pools from concurrency attributes</item>
///   <item>Seed <see cref="QueueOrchestrator"/> with persisted jobs and pool routes</item>
///   <item>Start worker tasks for each pool</item>
/// </list>
/// Shutdown: cancels workers, waits for in-flight <c>Process()</c> calls to finish (so their
/// <c>OnComplete</c>-buffered deletes are recorded), then flushes the
/// <see cref="PersistenceBuffer"/> so completed jobs are removed from the DB and any
/// not-yet-persisted inserts are committed. If workers don't exit within the host's shutdown
/// timeout, the flush runs anyway with an uncancellable token so the buffer always lands.
/// </summary>
public sealed class WorkerPoolManager : IHostedService
{
    private readonly ILogger<WorkerPoolManager> _logger;
    private readonly IJobRepository _repo;
    private readonly QueueOrchestrator _orchestrator;
    private readonly PoolDiscovery _poolDiscovery;
    private readonly PersistenceBuffer _persistenceBuffer;
    private readonly QueueMetrics _metrics;
    private readonly QueueStateEventHandler _events;
    private readonly IEnumerable<IAcquisitionFilter> _acquisitionFilters;
    private readonly IChainScopeRegistry _chainScopeRegistry;
    private readonly IServiceProvider _serviceProvider;
    private readonly QueueJobTypeRegistry _jobTypeRegistry;
    private readonly QueueProcessorOptions _options;

    private IReadOnlyList<WorkerPool> _pools = [];
    private CancellationTokenSource? _cts;
    private JobWatchdog? _watchdog;

    public IReadOnlyList<WorkerPool> Pools => _pools;

    public WorkerPoolManager(
        ILogger<WorkerPoolManager> logger,
        IJobRepository repo,
        QueueOrchestrator orchestrator,
        PoolDiscovery poolDiscovery,
        PersistenceBuffer persistenceBuffer,
        QueueMetrics metrics,
        QueueStateEventHandler events,
        IEnumerable<IAcquisitionFilter> acquisitionFilters,
        IChainScopeRegistry chainScopeRegistry,
        IServiceProvider serviceProvider,
        QueueJobTypeRegistry jobTypeRegistry,
        QueueProcessorOptions options)
    {
        _logger = logger;
        _repo = repo;
        _orchestrator = orchestrator;
        _poolDiscovery = poolDiscovery;
        _persistenceBuffer = persistenceBuffer;
        _metrics = metrics;
        _events = events;
        _acquisitionFilters = acquisitionFilters;
        _chainScopeRegistry = chainScopeRegistry;
        _serviceProvider = serviceProvider;
        _jobTypeRegistry = jobTypeRegistry;
        _options = options;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("WorkerPoolManager starting");

        // Apply any pending migrations (creates schema on first run)
        await _serviceProvider.MigrateQueueDatabaseAsync(ct);

        // Load persisted jobs
        var persistedJobs = await _repo.LoadAllAsync(ct);
        _logger.LogInformation("Loaded {Count} persisted jobs from database", persistedJobs.Count);

        // Discover job types from DI-registered IQueueJob implementations
        var jobTypes = DiscoverJobTypes();

        // Build pools
        _pools = _poolDiscovery.Discover(jobTypes, _acquisitionFilters);

        // Seed orchestrator
        _orchestrator.Initialize(persistedJobs, _pools);

        // Start workers
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        foreach (var pool in _pools)
            pool.Start(_serviceProvider, _orchestrator, _metrics, _events, _chainScopeRegistry);

        // Start watchdog
        _watchdog = new JobWatchdog(
            _orchestrator,
            _serviceProvider.GetRequiredService<ILogger<JobWatchdog>>(),
            TimeSpan.FromSeconds(_options.WatchdogTimeoutSeconds),
            _jobTypeRegistry.JobTypes);
        _watchdog.Start(_cts.Token);

        _events.InvokeQueueStarted();
        _logger.LogInformation("WorkerPoolManager started with {PoolCount} pools", _pools.Count);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _logger.LogInformation("WorkerPoolManager stopping");
        _cts?.Cancel();

        // Pause enqueue + concurrency gate so anything triggered by a completing job's
        // event handlers (e.g. job chains) doesn't enter the queue during shutdown.
        _orchestrator.Pause();

        // Cancel every pool's CTS first so idle workers exit immediately, then in parallel
        // wait for any worker still inside Process() to finish. This ordering is what
        // makes the subsequent flush actually capture in-flight completions: each Process()
        // ends with OnComplete → PersistenceBuffer.OnComplete (buffered DELETE). If we flushed
        // before workers exited, that DELETE would land in the buffer with nothing to flush it.
        foreach (var pool in _pools)
            pool.Stop();

        if (_watchdog is not null)
            await _watchdog.StopAsync().ConfigureAwait(false);

        try
        {
            await Task.WhenAll(_pools.Select(p => p.WhenStoppedAsync())).WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Workers did not exit within shutdown timeout — flushing buffer anyway");
        }

        // Use None so a cancelled shutdown ct doesn't abort the final DB write.
        await _persistenceBuffer.FlushNowAsync(CancellationToken.None).ConfigureAwait(false);

        _events.InvokeQueuePaused();
        _logger.LogInformation("WorkerPoolManager stopped");
    }

    public void Pause()
    {
        _orchestrator.Pause();
        _events.InvokeQueuePaused();
    }

    public void Resume()
    {
        _orchestrator.Resume();
        _events.InvokeQueueStarted();
    }

    private IEnumerable<Type> DiscoverJobTypes() => _jobTypeRegistry.JobTypes;
}
