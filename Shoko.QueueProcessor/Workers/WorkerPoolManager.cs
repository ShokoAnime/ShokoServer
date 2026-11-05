using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Chain;
using Shoko.QueueProcessor.Events;
using Shoko.QueueProcessor.Orchestration;
using Shoko.QueueProcessor.Scheduling;
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
    private Meter? _poolGauges;
    private readonly QueueStateEventHandler _events;
    private readonly IEnumerable<IAcquisitionFilter> _acquisitionFilters;
    private readonly IEnumerable<IJobWatchdogThreshold> _watchdogThresholds;
    private readonly IChainScopeRegistry _chainScopeRegistry;
    private readonly IServiceProvider _serviceProvider;
    private readonly QueueJobTypeRegistry _jobTypeRegistry;
    private readonly QueueProcessorOptions _options;
    private readonly QueueHolds _holds;

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
        IEnumerable<IJobWatchdogThreshold> watchdogThresholds,
        IChainScopeRegistry chainScopeRegistry,
        IServiceProvider serviceProvider,
        QueueJobTypeRegistry jobTypeRegistry,
        QueueProcessorOptions options,
        QueueHolds? holds = null)
    {
        _holds = holds ?? new();
        _logger = logger;
        _repo = repo;
        _orchestrator = orchestrator;
        _poolDiscovery = poolDiscovery;
        _persistenceBuffer = persistenceBuffer;
        _metrics = metrics;
        _events = events;
        _acquisitionFilters = acquisitionFilters;
        _watchdogThresholds = watchdogThresholds;
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
        var (unknownPools, unknownJobTypes) = _holds.SetKnown(_pools.Select(pool => pool.Name), jobTypes.Select(JobTypeNames.Key));
        foreach (var name in unknownPools)
            _logger.LogWarning("The pool {PoolName} is held, but the queue has no such pool; the hold is kept until it is released.", name);
        foreach (var key in unknownJobTypes)
            _logger.LogWarning("The job type {JobType} is held, but the queue has no such job type; the hold is kept until it is released.", key);
        ApplyHolds();
        _holds.Changed += OnHoldsChanged;

        await UpgradeLegacyJobKeys(persistedJobs, _pools, ct);

        // Seed orchestrator
        _orchestrator.Initialize(persistedJobs, _pools);

        // Pre-warm the ThreadPool to avoid the 1-thread/second growth ramp-up under burst load.
        // Worker threads block in GetAwaiter().GetResult() while async continuations — including
        // job Process() calls — run on the ThreadPool. Without this, a sudden burst of completions
        // can stall Kestrel request handling until the pool catches up.
        var totalWorkers = _pools.Sum(p => p.MaxWorkers);
        ThreadPool.GetMinThreads(out var currentMin, out var currentIoMin);
        if (totalWorkers > currentMin)
            ThreadPool.SetMinThreads(totalWorkers, currentIoMin);

        // Start workers
        _cts?.Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        foreach (var pool in _pools)
            pool.Start(_serviceProvider, _orchestrator, _metrics, _events, _chainScopeRegistry);

        _poolGauges?.Dispose();
        _poolGauges = QueueTelemetry.CreatePoolGauges(_orchestrator.GetPoolStatus);

        // Start watchdog
        _watchdog = new JobWatchdog(
            _orchestrator,
            _serviceProvider.GetRequiredService<ILogger<JobWatchdog>>(),
            TimeSpan.FromSeconds(_options.WatchdogTimeoutSeconds),
            _jobTypeRegistry.JobTypes,
            _watchdogThresholds);
        _watchdog.Start(_cts.Token);

        _events.InvokeQueueStarted();
        _logger.LogInformation("WorkerPoolManager started with {PoolCount} pools", _pools.Count);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _logger.LogInformation("WorkerPoolManager stopping");
        _cts?.Cancel();
        _poolGauges?.Dispose();
        _poolGauges = null;

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

    private void OnHoldsChanged(object? sender, EventArgs e) => ApplyHolds();

    // Hands every pool the holds, so each excludes what is held of it.
    private void ApplyHolds()
    {
        var heldPools = _holds.HeldPools;
        var heldTypes = _holds.HeldJobTypes;
        foreach (var pool in _pools)
            pool.SetHold(heldPools.Contains(pool.Name), heldTypes);
    }

    public void Pause()
    {
        _orchestrator.Pause();
        _events.InvokeQueuePaused();
    }

    public void Resume()
    {
        // A halted queue stays paused, so don't announce a start that didn't happen.
        if (_orchestrator.IsHalted)
            return;

        _orchestrator.Resume();
        _events.InvokeQueueStarted();
    }

    private IEnumerable<Type> DiscoverJobTypes() => _jobTypeRegistry.JobTypes;

    /// <summary>
    /// Rewrites persisted job keys that still start with the short type name, in memory and in
    /// the database, before anything reads them for deduplication.
    /// </summary>
    private async Task UpgradeLegacyJobKeys(List<QueuedJob> persistedJobs, IReadOnlyList<WorkerPool> pools, CancellationToken ct)
    {
        var typeByName = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var type in pools.SelectMany(pool => pool.HandledTypes))
            typeByName[JobTypeNames.Stored(type)] = type;

        var updates = new List<(Guid Id, string NewKey)>();
        foreach (var job in persistedJobs)
        {
            if (!typeByName.TryGetValue(job.JobType, out var type) || JobKeyBuilder.UpgradeLegacyKey(type, job.JobKey) is not { } newKey)
                continue;

            job.JobKey = newKey;
            updates.Add((job.Id, newKey));
        }

        if (updates.Count is 0)
            return;

        await _repo.UpdateKeyBatchAsync(updates, ct);
        _logger.LogInformation("Upgraded {Count} persisted job keys to the full type name", updates.Count);
    }
}
