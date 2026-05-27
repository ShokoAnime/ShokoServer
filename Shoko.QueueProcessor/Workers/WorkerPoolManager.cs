#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
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
/// Shutdown: cancels workers (in-flight jobs complete naturally) then flushes the
/// <see cref="PersistenceBuffer"/> so completed jobs are removed from the DB.
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
    private readonly IServiceProvider _serviceProvider;

    private IReadOnlyList<WorkerPool> _pools = [];
    private CancellationTokenSource? _cts;

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
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _repo = repo;
        _orchestrator = orchestrator;
        _poolDiscovery = poolDiscovery;
        _persistenceBuffer = persistenceBuffer;
        _metrics = metrics;
        _events = events;
        _acquisitionFilters = acquisitionFilters;
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("WorkerPoolManager starting");

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
            pool.Start(_serviceProvider, _orchestrator, _metrics, _events);

        _events.InvokeQueueStarted();
        _logger.LogInformation("WorkerPoolManager started with {PoolCount} pools", _pools.Count);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _logger.LogInformation("WorkerPoolManager stopping");
        _cts?.Cancel();

        foreach (var pool in _pools)
            pool.Stop();

        // Flush pending DB writes before exit
        await _persistenceBuffer.FlushNowAsync(ct);

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

    private System.Collections.Generic.IEnumerable<System.Type> DiscoverJobTypes()
    {
        // All types registered in DI as IQueueJob (registered as transient by AddQueueProcessor)
        var descriptor = _serviceProvider.GetService(typeof(System.Collections.Generic.IEnumerable<IQueueJob>));
        if (descriptor is System.Collections.Generic.IEnumerable<IQueueJob> instances)
            foreach (var i in instances)
                yield return i.GetType();
    }
}
