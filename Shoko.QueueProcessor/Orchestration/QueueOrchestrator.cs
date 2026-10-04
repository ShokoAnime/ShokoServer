using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Chain;
using Shoko.QueueProcessor.Events;
using Shoko.QueueProcessor.Storage;
using Shoko.QueueProcessor.Workers;

namespace Shoko.QueueProcessor.Orchestration;

/// <summary>
/// Singleton concurrency gatekeeper and state owner for the queue.
/// <para>
/// Pools own their sub-queues and do their own acquisition scanning (<see cref="WorkerPool.TryAcquire"/>).
/// This class:
/// <list type="bullet">
///   <item>Routes enqueued jobs to the correct pool sub-queue</item>
///   <item>Is the sole concurrency gate via <see cref="TryRegisterExecuting"/></item>
///   <item>Tracks executing state and per-type / per-group running counts</item>
///   <item>Signals affected pools on state changes</item>
///   <item>Drives <see cref="PersistenceBuffer"/> for coalesced DB writes</item>
/// </list>
/// </para>
/// </summary>
public sealed class QueueOrchestrator : IAsyncDisposable
{
    private readonly ILogger<QueueOrchestrator> _logger;
    private readonly PersistenceBuffer _persistenceBuffer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrencyRegistry _concurrency;
    private readonly RetryPolicyResolver _retryPolicies;
    private readonly QueueMetrics _metrics;
    private readonly QueueStateEventHandler _events;
    private readonly IChainScopeRegistry _chainScopeRegistry;
    private readonly int _maxTotalWorkers;

    // Executing state — all fields guarded by _gate
    private readonly Dictionary<Guid, ExecutingEntry> _executingSet = new();
    // Cancellation and progress of each executing job, keyed like _executingSet and removed with it
    private readonly Dictionary<Guid, JobExecutionState> _executionStates = new();
    private readonly Dictionary<Type, int> _typeRunningCounts = new();
    private readonly Dictionary<string, int> _groupRunningCounts = new();
    private volatile int _globalRunning;

    // Pool routing — populated by Initialize()
    private readonly Dictionary<Type, WorkerPool> _poolsByType = new();
    private IReadOnlyList<WorkerPool> _allPools = [];

    // O(1) type resolution — avoids Type.GetType() (assembly scan) on every enqueue/acquire call
    private readonly Dictionary<string, Type> _typeByName = new(StringComparer.Ordinal);

    // Friendly display names per type (short type name to IQueueJob.TypeName), built once at Initialize()
    private IReadOnlyDictionary<string, string> _typeFriendlyNames = new Dictionary<string, string>();

    // O(1) dedup index: JobKey → Id (covers waiting + executing + pending-insert + after-parent)
    private readonly Dictionary<string, Guid> _jobKeyIndex = new(StringComparer.Ordinal);

    // Merge handlers registered via RegisterMergeHandler. Registered handlers take priority over
    // IJobMerge. ConcurrentDictionary because RegisterMergeHandler may be called from plugin
    // registration code concurrently with queue initialization.
    private readonly ConcurrentDictionary<Type, Func<IQueueJob, IQueueJob, bool>> _mergeHandlers = new();

    // Pending completion callbacks registered by EnqueueImmediate callers.
    // Keyed by JobKey; resolved in OnComplete or faulted in OnFailureAsync (real failures only).
    private readonly Dictionary<string, List<TaskCompletionSource<bool>>> _immediateCallbacks = new(StringComparer.Ordinal);

    // Jobs registered via RunAfterCurrent: held until their parent job completes, then released
    // at int.MaxValue priority. Keys are claimed in _jobKeyIndex immediately on registration to
    // prevent a concurrent Enqueue from scheduling the same job before the parent finishes.
    // Inner key is JobKey for O(1) dedup within one parent execution.
    private readonly Dictionary<Guid, Dictionary<string, (EnqueueContext Ctx, WorkerPool Pool)>>
        _afterParentCallbacks = new();

    // Waiting jobs being pulled from their pools outside _gate (per the lock order below).
    // TryRegisterExecuting rejects them so no worker acquires one between the two steps.
    private readonly HashSet<Guid> _heldFromAcquisition = new();

    // All job IDs currently "in the system" regardless of state: waiting, executing, held, or
    // registered as an after-parent callback. Allows RegisterChainAfterJob to distinguish a
    // waiting parent (still in system) from a completed one (already removed).
    // Maintained in sync with _jobKeyIndex — every Add/Remove to _jobKeyIndex must mirror here.
    private readonly HashSet<Guid> _allKnownJobIds = new();

    // Waiting jobs TryRegisterExecuting found no longer own their key, such as one removed while
    // its enqueue was still adding it to its pool. Their pool drops them through IsDetached.
    private readonly ConcurrentDictionary<Guid, byte> _detachedJobIds = new();

    // Lock order: a pool's _subQueueLock, then _gate. Never call into a pool while holding _gate
    // or hold two pool locks at once; PersistenceBuffer's lock is a leaf.
    private readonly object _gate = new();
    private volatile bool _paused;

    private volatile bool _halted;

    public bool IsPaused => _paused;

    /// <summary>True once the queue has been halted; a halt is a pause that <see cref="Resume"/> cannot undo.</summary>
    public bool IsHalted => _halted;

    public QueueOrchestrator(
        ILogger<QueueOrchestrator> logger,
        PersistenceBuffer persistenceBuffer,
        IServiceScopeFactory scopeFactory,
        ConcurrencyRegistry concurrency,
        RetryPolicyResolver retryPolicies,
        QueueMetrics metrics,
        QueueStateEventHandler events,
        IChainScopeRegistry chainScopeRegistry,
        int maxTotalWorkers)
    {
        _logger = logger;
        _persistenceBuffer = persistenceBuffer;
        _scopeFactory = scopeFactory;
        _concurrency = concurrency;
        _retryPolicies = retryPolicies;
        _metrics = metrics;
        _events = events;
        _chainScopeRegistry = chainScopeRegistry;
        _maxTotalWorkers = maxTotalWorkers;
    }

    /// <summary>
    /// Called once at startup. Routes persisted jobs into pool sub-queues and wires acquisition delegates.
    /// </summary>
    public void Initialize(IEnumerable<QueuedJob> persistedJobs, IReadOnlyList<WorkerPool> pools)
    {
        _allPools = pools;
        foreach (var pool in pools)
        {
            foreach (var type in pool.HandledTypes)
            {
                _poolsByType[type] = pool;
                _typeByName[JobTypeNames.Stored(type)] = type;
            }
            pool.TryRegisterExecuting = TryRegisterExecuting;
            pool.IsDetached = job => _detachedJobIds.TryRemove(job.Id, out _);

            // Capture pool reference for the closure. Skip the check for highest-priority pools.
            var capturedPool = pool;
            pool.ShouldAttemptAcquisition = () => ShouldPoolAttemptAcquisition(capturedPool);
        }

        var activeCount = 0;
        var deferred = new Dictionary<Guid, QueuedJob>(); // jobId → job, for chain-deferred jobs

        // A type missing from a loaded assembly was removed, so its jobs are dropped; one from an
        // unloaded assembly (a plugin that failed to load) may come back, so its jobs are kept.
        var loadedAssemblies = pools.SelectMany(pool => pool.HandledTypes)
            .SelectMany(type => JobTypeNames.AssemblyNames(JobTypeNames.Stored(type)))
            .ToHashSet();
        var removed = new List<Guid>();

        foreach (var job in persistedJobs)
        {
            var type = ResolveType(job.JobType);
            if (type == null)
            {
                // A closed generic also names its type arguments' assemblies, and all must be loaded.
                if (JobTypeNames.AssemblyNames(job.JobType) is { Count: > 0 } assemblyNames && assemblyNames.All(loadedAssemblies.Contains))
                {
                    _logger.LogInformation("Dropping persisted job {JobType}, a type that no longer exists", job.JobType);
                    removed.Add(job.Id);
                    continue;
                }

                _logger.LogWarning("Skipping persisted job {JobType} — type not found", job.JobType);
                continue;
            }
            if (!_poolsByType.TryGetValue(type, out var pool))
            {
                _logger.LogWarning("Skipping persisted job {JobType} — no pool handles this type", job.JobType);
                continue;
            }

            if (job.ParentJobId.HasValue)
            {
                deferred[job.Id] = job;
                continue;
            }

            pool.AddToQueue(job);
            _jobKeyIndex[job.JobKey] = job.Id;
            _allKnownJobIds.Add(job.Id);
            activeCount++;
        }

        // Topological registration of deferred chain children: repeat passes until stable.
        // Each pass registers children whose parent is already in _allKnownJobIds.
        var orphans = new List<QueuedJob>();
        var remaining = new Dictionary<Guid, QueuedJob>(deferred);
        var progress = true;
        while (progress && remaining.Count > 0)
        {
            progress = false;
            foreach (var (id, job) in remaining.ToList())
            {
                var parentId = job.ParentJobId!.Value;
                if (!_allKnownJobIds.Contains(parentId))
                {
                    // Parent might be another deferred job not yet registered — skip for now
                    if (!deferred.ContainsKey(parentId))
                        orphans.Add(job); // Parent is gone — crash recovery case
                    continue;
                }

                var type = ResolveType(job.JobType)!;
                var pool = _poolsByType[type];
                var ctx = BuildEnqueueContextFromDb(job, type);
                if (!_afterParentCallbacks.TryGetValue(parentId, out var map))
                    _afterParentCallbacks[parentId] = map = new Dictionary<string, (EnqueueContext, WorkerPool)>(StringComparer.Ordinal);
                map[job.JobKey] = (ctx, pool);
                _jobKeyIndex[job.JobKey] = job.Id;
                _allKnownJobIds.Add(job.Id);
                remaining.Remove(id);
                progress = true;
            }
        }

        // Any still in `remaining` after all passes also have broken parent refs — treat as orphans.
        orphans.AddRange(remaining.Values);

        // Orphaned deferred children: parent completed before crash; activate them as standalone jobs.
        if (orphans.Count > 0)
        {
            _logger.LogInformation("Recovering {Count} orphaned chain-deferred jobs (parent completed before crash)", orphans.Count);
            var orphanIds = new List<Guid>(orphans.Count);
            foreach (var job in orphans)
            {
                var type = ResolveType(job.JobType);
                if (type == null || !_poolsByType.TryGetValue(type, out var pool)) continue;
                job.ParentJobId = null;
                pool.AddToQueue(job);
                _jobKeyIndex[job.JobKey] = job.Id;
                _allKnownJobIds.Add(job.Id);
                orphanIds.Add(job.Id);
                activeCount++;
            }
            // Schedule async UPDATE to clear ParentJobId in DB for orphans
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IJobRepository>().ActivateChainChildrenAsync(orphanIds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to clear ParentJobId for {Count} orphaned chain-deferred jobs", orphanIds.Count);
                }
            });
        }

        if (removed.Count > 0)
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IJobRepository>().DeleteBatchAsync(removed);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete {Count} persisted jobs whose types no longer exist", removed.Count);
                }
            });

        _typeFriendlyNames = BuildFriendlyNames();
        _logger.LogInformation("QueueOrchestrator initialized with {Count} active jobs and {Deferred} deferred across {Pools} pools",
            activeCount, deferred.Count - orphans.Count, pools.Count);
    }

    /// <summary>
    /// Registers a merge handler for <paramref name="jobType"/>. When a new enqueue collides
    /// with a waiting job of this type, <paramref name="handler"/> is invoked with the existing
    /// and incoming instances. The handler mutates the existing instance and returns
    /// <c>true</c> if any parameter was upgraded.
    /// Takes priority over <see cref="IJobMerge"/> if both are present on the same type.
    /// </summary>
    public void RegisterMergeHandler(Type jobType, Func<IQueueJob, IQueueJob, bool> handler)
        => _mergeHandlers[jobType] = handler;

    private bool HasMergeHandler(Type type)
        => _mergeHandlers.ContainsKey(type) || typeof(IJobMerge).IsAssignableFrom(type);

    /// <summary>
    /// Creates uninitialized instances of <paramref name="type"/>, hydrates them from the provided
    /// JSON strings, and calls the registered handler (priority) or <see cref="IJobMerge.TryMerge"/>
    /// (fallback). Returns the new serialized JSON if data changed, or <c>null</c> if not.
    /// </summary>
    private string? ComputeMergedJson(Type type, string? existingJson, string? incomingJson)
    {
        var existing = (IQueueJob)RuntimeHelpers.GetUninitializedObject(type);
        JobDataSerializer.Apply(existing, existingJson);
        var incoming = (IQueueJob)RuntimeHelpers.GetUninitializedObject(type);
        JobDataSerializer.Apply(incoming, incomingJson);

        bool changed;
        if (_mergeHandlers.TryGetValue(type, out var handler))
            changed = handler(existing, incoming);
        else if (existing is IJobMerge mergeable)
            changed = mergeable.TryMerge(incoming);
        else
            return null;

        return changed ? JobDataSerializer.Serialize(existing) : null;
    }

    /// <summary>
    /// Searches <see cref="_afterParentCallbacks"/> for a deferred job with the given key.
    /// If found and a merge handler is registered, merges the incoming data in-place and
    /// buffers a persistence update. Returns <c>true</c> if the job was found
    /// in the deferred map (regardless of whether data changed).
    /// <para>MUST be called under <see cref="_gate"/>.</para>
    /// </summary>
    private bool TryUpgradeDeferredUnderLock(string jobKey, Guid existingId, EnqueueContext context)
    {
        foreach (var parentMap in _afterParentCallbacks.Values)
        {
            if (!parentMap.TryGetValue(jobKey, out var entry)) continue;
            var mergedJson = ComputeMergedJson(context.Type, entry.Ctx.Job.JobDataJson, context.Job.JobDataJson);
            if (mergedJson != null)
            {
                entry.Ctx.Job.JobDataJson = mergedJson;
                _persistenceBuffer.OnUpdate(existingId, mergedJson);
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Iterates all pools outside <see cref="_gate"/> to find and upgrade a waiting job's data.
    /// Also promotes priority if the incoming request has a higher priority value.
    /// <para>
    /// Race safety: if the job is acquired by a worker between the gate-check and this call,
    /// <see cref="WorkerPool.TryGetAndUpdateData"/> returns <c>false</c> for all
    /// pools — no upgrade applied, matching the "executing = no-op" contract.
    /// </para>
    /// </summary>
    private void TryUpgradeWaiting(Guid existingId, EnqueueContext context)
    {
        string? mergedJson = null;
        foreach (var pool in _allPools)
        {
            if (!pool.TryGetAndUpdateData(existingId, currentJson =>
            {
                mergedJson = ComputeMergedJson(context.Type, currentJson, context.Job.JobDataJson);
                return mergedJson;
            })) continue;

            if (mergedJson != null)
                _persistenceBuffer.OnUpdate(existingId, mergedJson);
            if (context.Job.Priority > 0 && pool.TryPromotePriority(context.Job.JobKey, context.Job.Priority))
                if (!_paused) pool.Signal();
            break;
        }
    }

    /// <summary>
    /// Enqueues a job: dedup check (O(1)), routes to pool sub-queue, buffers insert, signals pool.
    /// Everything the orchestrator needs is in <paramref name="context"/>; nothing is rebuilt
    /// here. The scheduler has already pre-resolved the <see cref="Type"/> and pulled
    /// <see cref="QueueItem.TypeName"/>/<see cref="QueueItem.Title"/>/<see cref="QueueItem.Details"/>
    /// from the live job instance, so this method is O(few lock acquisitions).
    /// </summary>
    public Task EnqueueAsync(EnqueueContext context, CancellationToken ct = default)
    {
        var job = context.Job;
        var type = context.Type;

        if (!_poolsByType.TryGetValue(type, out var pool))
            throw new InvalidOperationException($"No pool handles job type '{type.FullName}'.");

        var isExisting = false;
        var shouldUpgrade = false;
        var upgradeId = Guid.Empty;
        lock (_gate)
        {
            if (_jobKeyIndex.TryGetValue(job.JobKey, out var existingId))
            {
                isExisting = true;
                if (!_executingSet.ContainsKey(existingId) && HasMergeHandler(type))
                {
                    if (!TryUpgradeDeferredUnderLock(job.JobKey, existingId, context))
                    {
                        shouldUpgrade = true;
                        upgradeId = existingId;
                    }
                }
            }
            else
            {
                _jobKeyIndex[job.JobKey] = job.Id;
                _allKnownJobIds.Add(job.Id);
                _persistenceBuffer.OnEnqueue(job);
            }
        }

        if (isExisting)
        {
            if (shouldUpgrade)
                TryUpgradeWaiting(upgradeId, context);
            return Task.CompletedTask;
        }

        pool.AddToQueue(job);
        _metrics.RecordEnqueue(JobTypeNames.Key(type), pool.Name);

        if (!_paused) pool.Signal();

        // PoolName is the one display field the scheduler can't fill (it doesn't know pool routing).
        var item = string.IsNullOrEmpty(context.DisplayItem.PoolName)
            ? context.DisplayItem with { PoolName = pool.Name }
            : context.DisplayItem;
        FireJobsAdded([item]);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Enqueues a batch of contexts. Uses a single gate-lock pass for dedup, a single lock per
    /// affected pool for sub-queue insertion, and one persistence-buffer call for the whole batch —
    /// far cheaper than calling <see cref="EnqueueAsync"/> per job when enqueueing thousands of items.
    /// </summary>
    public Task EnqueueRangeAsync(IEnumerable<EnqueueContext> contexts, CancellationToken ct = default)
    {
        // Resolve pools first — _poolsByType is stable after Initialize(), no lock needed.
        var resolved = new List<(EnqueueContext Ctx, WorkerPool Pool)>();
        foreach (var ctx in contexts)
        {
            if (!_poolsByType.TryGetValue(ctx.Type, out var pool)) continue;
            resolved.Add((ctx, pool));
        }

        if (resolved.Count == 0) return Task.CompletedTask;

        // Single gate-lock pass: dedup and register all keys atomically.
        var toEnqueue = new List<(EnqueueContext Ctx, WorkerPool Pool)>(resolved.Count);
        lock (_gate)
        {
            foreach (var entry in resolved)
            {
                if (_jobKeyIndex.ContainsKey(entry.Ctx.Job.JobKey)) continue;
                _jobKeyIndex[entry.Ctx.Job.JobKey] = entry.Ctx.Job.Id;
                _allKnownJobIds.Add(entry.Ctx.Job.Id);
                toEnqueue.Add(entry);
            }
            // Buffer the batch while still under _gate so that any concurrent RegisterChainAfterJob /
            // RegisterAfterParent that steals a key can call OnComplete before our OnEnqueue fires,
            // guaranteeing the fast-path cancel-out works correctly.
            if (toEnqueue.Count > 0)
                _persistenceBuffer.OnEnqueueBatch(toEnqueue.Select(e => e.Ctx.Job));
        }

        if (toEnqueue.Count == 0) return Task.CompletedTask;

        // Group by pool and batch-insert into each pool's sub-queue (one lock per pool).
        var poolBatches = new Dictionary<WorkerPool, List<QueuedJob>>();
        foreach (var (ctx, pool) in toEnqueue)
        {
            if (!poolBatches.TryGetValue(pool, out var batch))
                poolBatches[pool] = batch = new List<QueuedJob>();
            batch.Add(ctx.Job);
            _metrics.RecordEnqueue(JobTypeNames.Key(ctx.Type), pool.Name);
        }

        foreach (var (pool, batch) in poolBatches)
            pool.AddRangeToQueue(batch);

        if (!_paused) SignalAllPools();

        var items = toEnqueue.Select(e => string.IsNullOrEmpty(e.Ctx.DisplayItem.PoolName)
            ? e.Ctx.DisplayItem with { PoolName = e.Pool.Name }
            : e.Ctx.DisplayItem).ToList();
        FireJobsAdded(items);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns <c>true</c> if any acquisition filter on the pool that handles
    /// <paramref name="jobType"/> currently excludes that type from dispatch.
    /// </summary>
    public bool IsJobTypeBlocked(Type jobType)
        => _poolsByType.TryGetValue(jobType, out var pool) && pool.IsTypeBlocked(jobType);

    /// <summary>
    /// Atomically registers a completion callback TCS for the given job key, then either:
    /// <list type="bullet">
    ///   <item>Enqueues a new job at max priority if no job with this key exists.</item>
    ///   <item>Promotes an existing <em>waiting</em> job to max priority.</item>
    ///   <item>Does nothing if the job is currently executing (just waits for completion).</item>
    /// </list>
    /// Returns a <see cref="Task"/> that completes when the job finishes.
    /// </summary>
    public Task PrepareAndEnqueueImmediate(EnqueueContext context)
    {
        var job = context.Job;
        var type = context.Type;

        if (!_poolsByType.TryGetValue(type, out var pool))
            throw new InvalidOperationException($"No pool handles job type '{type.FullName}'.");

        TaskCompletionSource<bool> tcs;
        var action = ImmediateAction.Enqueue;

        lock (_gate)
        {
            tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_immediateCallbacks.TryGetValue(job.JobKey, out var list))
                _immediateCallbacks[job.JobKey] = list = new List<TaskCompletionSource<bool>>();
            list.Add(tcs);

            if (!_jobKeyIndex.ContainsKey(job.JobKey))
            {
                _jobKeyIndex[job.JobKey] = job.Id;
                action = ImmediateAction.Enqueue;
                _persistenceBuffer.OnEnqueue(job);
            }
            else if (_jobKeyIndex.TryGetValue(job.JobKey, out var existingId) && _executingSet.ContainsKey(existingId))
            {
                action = ImmediateAction.Wait;
            }
            else
            {
                action = ImmediateAction.Promote;
            }
        }

        switch (action)
        {
            case ImmediateAction.Enqueue:
                pool.AddToQueue(job);
                _metrics.RecordEnqueue(JobTypeNames.Key(type), pool.Name);
                if (!_paused) pool.Signal();
                var item = string.IsNullOrEmpty(context.DisplayItem.PoolName)
                    ? context.DisplayItem with { PoolName = pool.Name }
                    : context.DisplayItem;
                FireJobsAdded([item]);
                break;

            case ImmediateAction.Promote:
                foreach (var p in _allPools)
                {
                    if (p.TryPromotePriority(job.JobKey, int.MaxValue))
                    {
                        p.Signal();
                        break;
                    }
                }
                break;

                // ImmediateAction.Wait: job is executing — the TCS is registered, nothing else needed
        }

        return tcs.Task;
    }

    private enum ImmediateAction { Enqueue, Promote, Wait }

    /// <summary>
    /// Fires <see cref="QueueStateEventHandler.OnJobsAdded"/> with pre-built display items.
    /// Passes only cheap counts: <see cref="BlockedWaitingCount"/> scans every pool's sub-queue,
    /// so we skip it on the hot path. Consumers needing blocked-count should re-query state.
    /// </summary>
    private void FireJobsAdded(IReadOnlyList<QueueItem> items)
    {
        _events.OnJobsAdded(
            items,
            [],
            WaitingCount,
            blockedCount: 0,
            ExecutingCount,
            MaxConcurrentJobs);
    }

    /// <summary>
    /// Called by <see cref="WorkerPool.TryAcquire"/> under the pool sub-queue lock.
    /// Checks global cap + per-type / per-group concurrency. Atomically registers on approval.
    /// Returns <c>true</c> if the job may proceed.
    /// </summary>
    public bool TryRegisterExecuting(QueuedJob job)
    {
        if (_paused) return false;

        var type = ResolveType(job.JobType);
        if (type == null) return false;

        lock (_gate)
        {
            if (_heldFromAcquisition.Contains(job.Id)) return false;
            // A job that no longer owns its key was removed, or replaced, after its enqueue claimed
            // the key but before it reached the pool. It must not run: its row and key are gone.
            if (!_jobKeyIndex.TryGetValue(job.JobKey, out var owner) || owner != job.Id)
            {
                _detachedJobIds[job.Id] = 0;
                _persistenceBuffer.OnComplete(job.Id);
                return false;
            }

            if (_globalRunning >= _maxTotalWorkers) return false;
            if (!_concurrency.CanRun(type, _typeRunningCounts, _groupRunningCounts)) return false;

            _globalRunning++;
            _typeRunningCounts[type] = (_typeRunningCounts.GetValueOrDefault(type)) + 1;

            var group = _concurrency.GetGroup(type);
            if (group != null)
                _groupRunningCounts[group] = (_groupRunningCounts.GetValueOrDefault(group)) + 1;

            _poolsByType.TryGetValue(type, out var pool);
            _executingSet[job.Id] = new ExecutingEntry(
                job.Id, type, job.JobKey, job.JobDataJson,
                job.Priority, job.RetryCount, group,
                DateTime.UtcNow, pool?.Name ?? string.Empty,
                ChainId: job.ChainId,
                IsChainFinally: job.IsChainFinally,
                IsCancellable: JobCapabilities.IsCancellable(type),
                Actor: job.Actor);
            _executionStates[job.Id] = new JobExecutionState(job.Id, job.JobKey, OnProgressReported);
        }
        return true;
    }

    /// <summary>
    /// Attaches the worker's cancellation source to an executing job, and cancels it right away
    /// when a user asked for the job to be cancelled before the worker got this far.
    /// </summary>
    /// <param name="id">The ID of the executing job.</param>
    /// <param name="cancellation">The job's cancellation source, linked to the pool's and disposed by the worker.</param>
    /// <returns>The job's execution state, or <c>null</c> if the job is not executing.</returns>
    internal async Task<JobExecutionState?> BeginExecutionAsync(Guid id, CancellationTokenSource cancellation)
    {
        JobExecutionState? state;
        bool alreadyRequested;
        lock (_gate)
        {
            if (!_executionStates.TryGetValue(id, out state))
                return null;
            alreadyRequested = state.Attach(cancellation);
        }

        if (alreadyRequested)
            await state.CancelAsync().ConfigureAwait(false);
        return state;
    }

    private void OnProgressReported(JobExecutionState state, decimal progress)
        => _events.OnJobProgressChanged(state.JobKey, progress);

    /// <summary>
    /// Frees <paramref name="jobKey"/> when it still maps to <paramref name="id"/>, so a job that
    /// ends never frees the key of another job queued under it since.
    /// <para>MUST be called under <see cref="_gate"/>.</para>
    /// </summary>
    /// <param name="jobKey">The key of the job that ends.</param>
    /// <param name="id">The ID of the job that ends.</param>
    private void RemoveKeyIfOwned_UnderLock(string jobKey, Guid id)
    {
        if (_jobKeyIndex.TryGetValue(jobKey, out var owner) && owner == id)
            _jobKeyIndex.Remove(jobKey);
    }

    /// <summary>
    /// Removes an executing job's entry and state and releases its concurrency slot.
    /// <para>MUST be called under <see cref="_gate"/>.</para>
    /// </summary>
    private bool TryRemoveExecuting_UnderLock(Guid id, out ExecutingEntry entry)
    {
        if (!_executingSet.Remove(id, out entry))
            return false;

        if (_executionStates.Remove(id, out var state))
            state.End();
        DecrementCounts(entry.JobType, entry.ConcurrencyGroup);
        return true;
    }

    /// <summary>
    /// Registers a child job to be enqueued at <see cref="int.MaxValue"/> priority immediately
    /// after <paramref name="parentId"/> completes. If a job with the same key is already waiting
    /// in a pool sub-queue, it is removed and held here instead. If the same key is already
    /// executing, this call is a no-op. Multiple registrations for the same key under the same
    /// parent are deduplicated.
    /// </summary>
    public void RegisterAfterParent(Guid parentId, EnqueueContext ctx)
    {
        if (!_poolsByType.TryGetValue(ctx.Type, out var targetPool))
            return;

        List<(EnqueueContext Ctx, WorkerPool Pool)>? immediateEnqueue = null;
        // A waiting job to pull from its pool outside _gate, per the lock order;
        // _heldFromAcquisition blocks acquisition in the interim.
        var heldId = Guid.Empty;

        lock (_gate)
        {
            if (_jobKeyIndex.TryGetValue(ctx.Job.JobKey, out var existingId))
            {
                // Already executing — leave it alone; it will complete with current data
                if (_executingSet.ContainsKey(existingId))
                    return;

                // Waiting — mark as held so TryRegisterExecuting rejects it while we pull it
                // out of the pool sub-queue below (outside the lock).
                _heldFromAcquisition.Add(existingId);
                heldId = existingId;

                // Swap tracking from old ID to new ID
                _allKnownJobIds.Remove(existingId);
                _allKnownJobIds.Add(ctx.Job.Id);

                // Key stays in _jobKeyIndex; we'll point it at the new context's ID below
            }
            else
            {
                // Claim the key so a concurrent Enqueue sees it as already registered
                _jobKeyIndex[ctx.Job.JobKey] = ctx.Job.Id;
                _allKnownJobIds.Add(ctx.Job.Id);
            }

            if (!_afterParentCallbacks.TryGetValue(parentId, out var map))
            {
                // Race guard: parent already completed — enqueue immediately
                if (!_executingSet.ContainsKey(parentId))
                {
                    immediateEnqueue = [(ctx, targetPool)];
                    _jobKeyIndex[ctx.Job.JobKey] = ctx.Job.Id;
                }
                else
                {
                    _afterParentCallbacks[parentId] = map = new Dictionary<string, (EnqueueContext, WorkerPool)>(StringComparer.Ordinal);
                    map[ctx.Job.JobKey] = (ctx, targetPool);
                    _jobKeyIndex[ctx.Job.JobKey] = ctx.Job.Id;
                }
            }
            else
            {
                map[ctx.Job.JobKey] = (ctx, targetPool);
                _jobKeyIndex[ctx.Job.JobKey] = ctx.Job.Id;
            }
        }

        // Physical removal happens outside _gate. TryRegisterExecuting already rejects heldId,
        // so no worker can acquire the job during this window.
        if (heldId != Guid.Empty)
        {
            foreach (var pool in _allPools)
                pool.RemoveFromQueue(heldId);
            // Remove the old DB record so it doesn't re-appear as a duplicate on restart.
            _persistenceBuffer.OnComplete(heldId);
            lock (_gate) _heldFromAcquisition.Remove(heldId);
        }

        if (immediateEnqueue != null)
        {
            foreach (var (c, pool) in immediateEnqueue)
            {
                pool.AddToQueue(c.Job);
                _persistenceBuffer.OnEnqueue(c.Job);
                _metrics.RecordEnqueue(JobTypeNames.Key(c.Type), pool.Name);
            }
            SignalAllPools();
        }
    }

    /// <summary>
    /// Registers a child job to run after <paramref name="parentId"/> completes, even when the
    /// parent is still <em>waiting</em> in a pool sub-queue (unlike
    /// <see cref="RegisterAfterParent"/> which only handles the currently-executing-parent case).
    /// This enables pre-built chains where A → B → C are registered before any of them execute.
    /// </summary>
    public void RegisterChainAfterJob(Guid parentId, EnqueueContext ctx)
    {
        if (!_poolsByType.TryGetValue(ctx.Type, out var targetPool))
            return;

        List<(EnqueueContext Ctx, WorkerPool Pool)>? immediateEnqueue = null;
        var heldId = Guid.Empty;
        var registerAsDeferred = false;

        lock (_gate)
        {
            if (_jobKeyIndex.TryGetValue(ctx.Job.JobKey, out var existingId))
            {
                // Already executing — leave it alone
                if (_executingSet.ContainsKey(existingId))
                    return;

                // Waiting — hold it while we remove it from the pool sub-queue
                _heldFromAcquisition.Add(existingId);
                _allKnownJobIds.Remove(existingId);
                _allKnownJobIds.Add(ctx.Job.Id);
                heldId = existingId;
            }
            else
            {
                _jobKeyIndex[ctx.Job.JobKey] = ctx.Job.Id;
                _allKnownJobIds.Add(ctx.Job.Id);
            }

            // KEY DIFFERENCE from RegisterAfterParent: use _allKnownJobIds instead of
            // _executingSet so waiting parents are handled correctly.
            if (_allKnownJobIds.Contains(parentId))
            {
                if (!_afterParentCallbacks.TryGetValue(parentId, out var map))
                    _afterParentCallbacks[parentId] = map = new Dictionary<string, (EnqueueContext, WorkerPool)>(StringComparer.Ordinal);
                map[ctx.Job.JobKey] = (ctx, targetPool);
                _jobKeyIndex[ctx.Job.JobKey] = ctx.Job.Id;
                registerAsDeferred = true;
            }
            else
            {
                // Parent already completed — enqueue immediately (no ParentJobId needed)
                ctx.Job.ParentJobId = null;
                immediateEnqueue = [(ctx, targetPool)];
                _jobKeyIndex[ctx.Job.JobKey] = ctx.Job.Id;
            }
        }

        if (heldId != Guid.Empty)
        {
            foreach (var pool in _allPools)
                pool.RemoveFromQueue(heldId);
            // Remove the old DB record so it doesn't re-appear as an orphan on restart.
            _persistenceBuffer.OnComplete(heldId);
            lock (_gate) _heldFromAcquisition.Remove(heldId);
        }

        if (registerAsDeferred)
        {
            // Persist with ParentJobId so the child survives a restart while waiting
            ctx.Job.ParentJobId = parentId;
            _persistenceBuffer.OnEnqueue(ctx.Job);
            _metrics.RecordEnqueue(JobTypeNames.Key(ctx.Type), targetPool.Name);
        }

        if (immediateEnqueue != null)
        {
            foreach (var (c, pool) in immediateEnqueue)
            {
                pool.AddToQueue(c.Job);
                _persistenceBuffer.OnEnqueue(c.Job);
                _metrics.RecordEnqueue(JobTypeNames.Key(c.Type), pool.Name);
            }
            SignalAllPools();
        }
    }

    /// <summary>
    /// Called by workers on success. Updates counts, buffers DB delete, signals pools.
    /// </summary>
    public void OnComplete(Guid id)
    {
        List<TaskCompletionSource<bool>>? completions = null;
        List<(EnqueueContext Ctx, WorkerPool Pool)>? deferred = null;
        Guid? chainId = null;
        lock (_gate)
        {
            if (!TryRemoveExecuting_UnderLock(id, out var entry)) return;
            RemoveKeyIfOwned_UnderLock(entry.JobKey, id);
            _allKnownJobIds.Remove(id);
            _immediateCallbacks.Remove(entry.JobKey, out completions);
            chainId = entry.ChainId;

            if (_afterParentCallbacks.Remove(id, out var deferredMap))
                deferred = [.. deferredMap.Values];
        }

        completions?.ForEach(tcs => tcs.TrySetResult(true));

        if (deferred != null)
        {
            foreach (var (ctx, pool) in deferred)
            {
                pool.AddToQueue(ctx.Job);
                if (ctx.Job.ParentJobId.HasValue)
                    // Chain child: already in DB with ParentJobId set — UPDATE to activate it.
                    _persistenceBuffer.OnActivateChainChild(ctx.Job.Id);
                else
                    // After-parent child (registered via RunAfterCurrent): never in DB — INSERT now.
                    _persistenceBuffer.OnEnqueue(ctx.Job);
                _metrics.RecordEnqueue(JobTypeNames.Key(ctx.Type), pool.Name);
            }
        }
        else if (chainId.HasValue)
        {
            // No deferred children — this was the last job in the chain; dispose chain scope
            _chainScopeRegistry.CompleteChainScope(chainId.Value);
        }

        _persistenceBuffer.OnComplete(id);
        SignalAllPools();
    }

    /// <summary>
    /// Called by workers on failure. Applies the retry policy: reschedules or discards.
    /// </summary>
    /// <remarks>
    /// A retried job keeps the rest of its own chain waiting for it, while what its failed attempt
    /// queued after itself is dropped. A discarded job ends its chain as a
    /// <see cref="ChainAbortException"/> would, so the chain's <see cref="ChainFinallyAttribute"/>
    /// jobs still run.
    /// </remarks>
    /// <param name="id">The ID of the failed job.</param>
    /// <param name="ex">The exception the job failed with.</param>
    /// <param name="incrementRetry">
    /// False re-queues the job at once at its original priority without touching
    /// <see cref="QueuedJob.RetryCount"/> or the DB, as <c>RequeueJobException</c> does for
    /// filter-managed transient conditions.
    /// </param>
    /// <param name="ct">Cancels persisting the retry state.</param>
    /// <returns>A task that completes once the failure is handled.</returns>
    public async Task OnFailureAsync(Guid id, Exception ex, bool incrementRetry = true, CancellationToken ct = default)
    {
        ExecutingEntry entry;
        lock (_gate)
        {
            if (!_executingSet.TryGetValue(id, out entry)) return;
        }

        // Chain abort: short-circuit remaining chain jobs (except [ChainFinally] ones)
        if (ex is ChainAbortException)
        {
            await HandleChainAbortAsync(id, entry, ex, cancelled: false, ct);
            return;
        }

        // RequeueJobException: same key and ID, so callbacks and after-parent registrations
        // stay and fire on its eventual completion.
        if (!incrementRetry)
        {
            lock (_gate)
            {
                TryRemoveExecuting_UnderLock(id, out _);
                _jobKeyIndex[entry.JobKey] = id;
                _allKnownJobIds.Add(id);
            }
            if (_poolsByType.TryGetValue(entry.JobType, out var requeuePool))
                requeuePool.AddToQueue(BuildRequeuedJob(entry, scheduledAt: null, entry.RetryCount));
            SignalAllPools();
            return;
        }

        var policy = _retryPolicies.For(entry.JobType);
        _metrics.RecordFailure(JobTypeNames.Key(entry.JobType), entry.PoolName);

        if (policy.ShouldDiscard(entry.RetryCount))
        {
            _logger.LogError(ex,
                "Job {JobKey} ({JobType}) discarded after {Retries} retries",
                entry.JobKey, JobTypeNames.Short(entry.JobType), policy.MaxRetries);

            // Frees the key, faults immediate callers, drops the job's row and after-parent
            // children, and ends its chain with the chain's finally jobs still to run.
            await HandleChainAbortAsync(id, entry, ex, cancelled: false, ct);
            return;
        }

        // The job's own chain successors wait for the retry; anything else registered after it
        // came from the failed attempt and is dropped, so the retry can claim those keys again.
        List<TaskCompletionSource<bool>>? completions;
        List<(EnqueueContext Ctx, WorkerPool Pool)> finallyJobs;
        List<EnqueueContext> skippedJobs;
        lock (_gate)
        {
            TryRemoveExecuting_UnderLock(id, out _);
            _immediateCallbacks.Remove(entry.JobKey, out completions);
            (finallyJobs, skippedJobs) = CollectChainDescendants_UnderLock(id, keepChainId: entry.ChainId);
        }

        completions?.ForEach(tcs => tcs.TrySetException(ex));

        // Delete dropped children from DB (chain children were inserted with ParentJobId set)
        foreach (var skipped in skippedJobs)
            _persistenceBuffer.OnComplete(skipped.Job.Id);
        ActivateChainFinallyJobs(finallyJobs);

        var delay = policy.GetDelay(entry.RetryCount);
        var nextRun = DateTimeOffset.UtcNow.Add(delay);
        var newRetryCount = entry.RetryCount + 1;

        _logger.LogWarning(ex,
            "Job {JobKey} ({JobType}) failed — retry {N}/{Max} in {Delay:g}",
            entry.JobKey, JobTypeNames.Short(entry.JobType), newRetryCount, policy.MaxRetries, delay);

        // Persisted now so a restart keeps the backoff. A failure must not escape the worker thread
        // (it would abort the process); the in-memory re-queue below keeps the job alive.
        try
        {
            using var scope = _scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IJobRepository>()
                .UpdateRetryAsync(id, newRetryCount, nextRun, ct);
        }
        catch (Exception persistEx)
        {
            _logger.LogWarning(persistEx,
                "Failed to persist retry backoff for job {JobKey} ({JobType}); continuing with in-memory re-queue",
                entry.JobKey, JobTypeNames.Short(entry.JobType));
        }

        if (_poolsByType.TryGetValue(entry.JobType, out var pool))
            pool.AddToQueue(BuildRequeuedJob(entry, nextRun, newRetryCount));

        SignalAllPools();
    }

    /// <summary>
    /// Builds the waiting copy of an executing job that runs it again, in its chain and for its
    /// actor. It has no <see cref="QueuedJob.ParentJobId"/>: a job only runs once its parent let
    /// it go.
    /// </summary>
    /// <param name="entry">The executing job.</param>
    /// <param name="scheduledAt">When it may run again, or <c>null</c> for right away.</param>
    /// <param name="retryCount">The retries it has used.</param>
    /// <returns>The job to put back in its pool.</returns>
    private static QueuedJob BuildRequeuedJob(ExecutingEntry entry, DateTimeOffset? scheduledAt, int retryCount) => new()
    {
        Id = entry.Id,
        JobType = JobTypeNames.Stored(entry.JobType),
        JobKey = entry.JobKey,
        JobDataJson = entry.JobDataJson,
        Priority = entry.Priority,
        QueuedAt = DateTimeOffset.UtcNow,
        ScheduledAt = scheduledAt,
        RetryCount = retryCount,
        ChainId = entry.ChainId,
        IsChainFinally = entry.IsChainFinally,
        Actor = entry.Actor,
    };

    public void Pause()
    {
        _paused = true;
        _logger.LogInformation("Queue paused");
    }

    public void Resume()
    {
        if (_halted)
        {
            _logger.LogWarning("Queue resume ignored; the queue is halted until the server is restarted");
            return;
        }

        _paused = false;
        _logger.LogInformation("Queue resumed");
        SignalAllPools();
    }

    /// <summary>
    /// Pause dispatching for the rest of the process lifetime. Used when the server can never become
    /// usable in this process, such as after a failed startup, so no job runs against a half-initialised
    /// server. Executing jobs run to completion.
    /// </summary>
    public void Halt(string reason)
    {
        _halted = true;
        _paused = true;
        _logger.LogWarning("Queue halted until the server is restarted: {Reason}", reason);
    }

    /// <summary>
    /// Removes the waiting job with <paramref name="jobKey"/> from the queue and the database,
    /// freeing its key. An executing job is left alone. A job waiting on its turn in a chain is
    /// removed too, and the chain is aborted from there on, as a <see cref="ChainAbortException"/>
    /// would, with <see cref="ChainFinallyAttribute"/> jobs still running.
    /// </summary>
    /// <param name="jobKey">The key of the job to remove.</param>
    /// <param name="ct">Cancels reading the chain context of an aborted chain.</param>
    /// <returns>
    /// <see cref="JobCancellationResult.Removed"/>, <see cref="JobCancellationResult.Running"/> when
    /// the job is executing, or <see cref="JobCancellationResult.NotFound"/>.
    /// </returns>
    public async Task<JobCancellationResult> RemoveAsync(string jobKey, CancellationToken ct = default)
    {
        Guid id;
        Guid? deferredParentId = null;
        QueuedJob? removedJob = null;
        List<TaskCompletionSource<bool>>? completions;
        List<(EnqueueContext Ctx, WorkerPool Pool)> finallyJobs;
        List<EnqueueContext> skippedJobs;
        List<Guid> reparented = [];
        lock (_gate)
        {
            if (!_jobKeyIndex.TryGetValue(jobKey, out id))
                return JobCancellationResult.NotFound;

            // Don't remove executing jobs — they're already past the point of no return.
            if (_executingSet.ContainsKey(id))
                return JobCancellationResult.Running;

            // A job waiting on its parent lives in the after-parent map, not in a pool.
            foreach (var (parentId, children) in _afterParentCallbacks)
            {
                if (!children.TryGetValue(jobKey, out var child) || child.Ctx.Job.Id != id)
                    continue;

                children.Remove(jobKey);
                if (children.Count == 0)
                    _afterParentCallbacks.Remove(parentId);
                deferredParentId = parentId;
                removedJob = child.Ctx.Job;
                break;
            }

            // Held so no worker acquires it before it leaves its pool below.
            if (deferredParentId is null)
                _heldFromAcquisition.Add(id);
            _jobKeyIndex.Remove(jobKey);
            _allKnownJobIds.Remove(id);
            _immediateCallbacks.Remove(jobKey, out completions);
            (finallyJobs, skippedJobs) = CollectChainDescendants_UnderLock(id);

            // The chain's finally jobs still have to wait for the job before the removed one.
            if (deferredParentId is { } newParentId && finallyJobs.Count > 0)
            {
                if (!_afterParentCallbacks.TryGetValue(newParentId, out var siblings))
                    _afterParentCallbacks[newParentId] = siblings = new Dictionary<string, (EnqueueContext, WorkerPool)>(StringComparer.Ordinal);
                foreach (var (ctx, pool) in finallyJobs)
                {
                    if (ctx.Job.ParentJobId.HasValue)
                    {
                        ctx.Job.ParentJobId = newParentId;
                        reparented.Add(ctx.Job.Id);
                    }
                    siblings[ctx.Job.JobKey] = (ctx, pool);
                }
                finallyJobs = [];
            }
        }

        // Persisted too, or a restart before the parent runs would start them as orphans.
        foreach (var reparentedId in reparented)
            _persistenceBuffer.OnReparentChainChild(reparentedId, deferredParentId!.Value);

        // Pools are only touched outside _gate, per the lock order.
        if (deferredParentId is null)
        {
            foreach (var pool in _allPools)
            {
                if (pool.RemoveFromQueue(id, out var job))
                {
                    removedJob = job;
                    break;
                }
            }
            lock (_gate) _heldFromAcquisition.Remove(id);
        }

        completions?.ForEach(tcs => tcs.TrySetCanceled());

        // Buffered like every other delete, so a job whose insert is still buffered is never written.
        _persistenceBuffer.OnComplete(id);
        foreach (var skipped in skippedJobs)
            _persistenceBuffer.OnComplete(skipped.Job.Id);

        ActivateChainFinallyJobs(finallyJobs);

        if (removedJob?.ChainId is { } chainId)
        {
            var removedOutcome = new JobOutcome
            {
                JobId = id,
                JobType = removedJob.JobType,
                JobKey = jobKey,
                Status = JobOutcomeStatus.Cancelled,
                CompletedAt = DateTimeOffset.UtcNow,
            };
            // A job still waiting on its parent leaves the parent's part of the chain to run.
            await RecordChainAbortAsync(
                chainId,
                SkippedOutcomes(skippedJobs).Prepend(removedOutcome),
                completeScope: deferredParentId is null && finallyJobs.Count == 0,
                ct
            );
        }

        if (finallyJobs.Count > 0)
            SignalAllPools();

        _logger.LogInformation("Removed waiting job {JobKey} from the queue", jobKey);
        _events.OnJobsRemoved([jobKey, .. skippedJobs.Select(skipped => skipped.Job.JobKey)]);
        return JobCancellationResult.Removed;
    }

    /// <summary>
    /// Cancels the job with <paramref name="jobKey"/>. A waiting job is removed, as by
    /// <see cref="RemoveAsync"/>. A running job that observes cancellation is asked to stop: it is
    /// marked as cancellation requested at once, its token is cancelled, and once it stops it ends
    /// as cancelled, is not retried, frees its key and aborts what was to run after it. A job
    /// that completes before noticing ends as completed; one that fails is not retried either.
    /// </summary>
    /// <param name="jobKey">The key of the job to cancel.</param>
    /// <param name="ct">Cancels reading the chain context when a waiting chain job is removed.</param>
    /// <returns>What was done.</returns>
    public async Task<JobCancellationResult> CancelAsync(string jobKey, CancellationToken ct = default)
    {
        // A waiting job can start between the two steps below, so look again when it did.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            JobExecutionState? state = null;
            ExecutingEntry entry = default;
            var newlyRequested = false;
            lock (_gate)
            {
                if (!_jobKeyIndex.TryGetValue(jobKey, out var id))
                    return JobCancellationResult.NotFound;

                if (_executingSet.TryGetValue(id, out entry))
                {
                    if (!entry.IsCancellable)
                        return JobCancellationResult.NotCancellable;

                    state = _executionStates[id];
                    newlyRequested = state.MarkCancellationRequested();
                    if (newlyRequested)
                        _executingSet[id] = entry = entry with { CancellationRequested = true };
                }
            }

            if (state is null)
            {
                var removed = await RemoveAsync(jobKey, ct);
                if (removed is JobCancellationResult.Running)
                    continue;
                return removed;
            }

            if (newlyRequested)
            {
                _logger.LogInformation("Cancellation requested for running job {JobKey} ({JobType})", jobKey, JobTypeNames.Short(entry.JobType));
                try
                {
                    await state.CancelAsync();
                }
                catch (AggregateException ex)
                {
                    _logger.LogWarning(ex, "A cancellation callback of job {JobKey} threw", jobKey);
                }
                _events.OnJobCancellationRequested(QueueItem.FromExecuting(entry with { Progress = state.Progress }));
            }

            return JobCancellationResult.CancellationRequested;
        }

        return JobCancellationResult.NotFound;
    }

    /// <summary>
    /// Removes every waiting and deferred job from the queue and the database. Executing jobs
    /// run to completion and keep their keys.
    /// </summary>
    /// <param name="ct">Cancels the database clear.</param>
    /// <returns>A task that completes once the database is cleared.</returns>
    public async Task ClearAsync(CancellationToken ct = default)
    {
        // Pools are only touched outside _gate, per the lock order: the waiting jobs are read
        // first, held from acquisition under _gate with every other known job, then removed.
        var waiting = _allPools.SelectMany(pool => pool.GetWaitingSnapshot()).Select(job => job.Id).ToList();
        HashSet<Guid> cleared;
        lock (_gate)
        {
            cleared = [.. waiting, .. _jobKeyIndex.Values, .. _allKnownJobIds];
            cleared.ExceptWith(_executingSet.Keys);
            _heldFromAcquisition.UnionWith(cleared);

            _afterParentCallbacks.Clear();
            _jobKeyIndex.Clear();
            _allKnownJobIds.Clear();
            // Restore keys for currently-executing jobs; all others are gone
            foreach (var entry in _executingSet.Values)
            {
                _jobKeyIndex[entry.JobKey] = entry.Id;
                _allKnownJobIds.Add(entry.Id);
            }
        }

        foreach (var pool in _allPools)
            pool.RemoveFromQueue(cleared);
        lock (_gate) _heldFromAcquisition.ExceptWith(cleared);

        using var scope = _scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IJobRepository>().ClearAllAsync(ct);
        _events.OnJobsRemoved([]);
    }

    // ── State queries ──────────────────────────────────────────────────────────

    public int WaitingCount => _allPools.Sum(p => p.WaitingCount);

    /// <summary>
    /// Number of waiting jobs whose type is currently excluded by an acquisition filter (and that
    /// are not deferred to a future scheduled time). Computed on demand — do not call on the hot path.
    /// </summary>
    public int BlockedWaitingCount => _allPools.Sum(p => p.BlockedCount);

    /// <summary>
    /// Number of waiting jobs deferred to a future <see cref="QueuedJob.ScheduledAt"/> — retry
    /// backoff or an intentionally delayed re-fetch. Not yet ready to run; disjoint from
    /// <see cref="BlockedWaitingCount"/> and <see cref="ReadyWaitingCount"/>.
    /// </summary>
    public int ScheduledWaitingCount => _allPools.Sum(p => p.ScheduledCount);

    /// <summary>
    /// Number of waiting jobs that are ready to run right now: not blocked by an acquisition filter
    /// and not deferred to a future scheduled time. This is the true "waiting" figure. The counts
    /// are read one after the other while jobs come and go, so each pool's share is kept from
    /// going below zero.
    /// </summary>
    public int ReadyWaitingCount => _allPools.Sum(p => Math.Max(0, p.WaitingCount - p.BlockedCount - p.ScheduledCount));

    /// <summary>Total worker slots across all pools (sum of every pool's <see cref="WorkerPool.MaxWorkers"/>).</summary>
    public int TotalWorkerCount => _allPools.Sum(p => p.MaxWorkers);

    /// <summary>
    /// Hard ceiling on concurrent execution across all pools. Even when <see cref="TotalWorkerCount"/>
    /// is larger, no more than this many jobs can be executing at once.
    /// </summary>
    public int MaxConcurrentJobs => _maxTotalWorkers;

    /// <summary>
    /// Resolves a fully-qualified job type name to its <see cref="Type"/> using the
    /// pre-built startup cache. Returns <c>null</c> if the type is not registered.
    /// </summary>
    public Type? TryResolveType(string typeName) => ResolveType(typeName);

    /// <summary>
    /// Returns true if the job is currently held back by its pool: its type excluded by an
    /// acquisition filter, or its actor not restorable yet.
    /// Used to populate <see cref="Abstractions.QueueItem.Blocked"/> for waiting jobs.
    /// </summary>
    public bool IsJobBlocked(QueuedJob job)
    {
        var type = ResolveType(job.JobType);
        if (type == null) return false;
        return _poolsByType.TryGetValue(type, out var pool) && pool.IsJobBlocked(job);
    }

    /// <summary>Returns waiting jobs across all pools in priority order, optionally paginated.</summary>
    public IReadOnlyList<QueuedJob> GetWaiting(int maxCount, int offset, Func<QueuedJob, bool>? filter = null)
    {
        var result = new List<QueuedJob>();
        // Collect from all pools (already sorted within each pool)
        var allWaiting = _allPools
            .SelectMany(p => p.GetWaitingSnapshot())
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.QueuedAt);
        if (filter != null) allWaiting = allWaiting.Where(filter).OrderByDescending(j => j.Priority).ThenBy(j => j.QueuedAt);
        result.AddRange(allWaiting.Skip(offset).Take(maxCount));
        return result;
    }

    public int ExecutingCount => _globalRunning;

    /// <summary>
    /// Returns a snapshot of the executing jobs, each with the progress it last reported.
    /// </summary>
    /// <returns>The executing jobs.</returns>
    public IReadOnlyList<ExecutingEntry> GetExecuting()
    {
        lock (_gate)
        {
            return [
                .. _executingSet.Values.Select(entry => _executionStates.TryGetValue(entry.Id, out var state) && state.Progress is { } progress
                    ? entry with { Progress = progress }
                    : entry),
            ];
        }
    }

    /// <summary>
    /// Returns debug info for all active chains: each entry lists executed, executing, and pending
    /// jobs in chain order, with the status of each.
    /// </summary>
    public IReadOnlyList<ChainDebugInfo> GetChainDebugInfo()
    {
        // Snapshot executing entries and pending chain relationships under lock
        List<(Guid ChainId, List<(Guid Id, string Key)> Executing, List<string> Pending)> snapshot;
        lock (_gate)
        {
            snapshot = _executingSet.Values
                .Where(e => e.ChainId.HasValue)
                .GroupBy(e => e.ChainId!.Value)
                .Select(g =>
                {
                    var executing = g.Select(e => (e.Id, e.JobKey)).ToList();
                    var pending = new List<string>();
                    foreach (var (id, _) in executing)
                        WalkChainChildren(id, pending);
                    return (g.Key, executing, pending);
                })
                .ToList();
        }

        var result = new List<ChainDebugInfo>();
        foreach (var (chainId, executing, pending) in snapshot)
        {
            var jobs = new List<ChainJobEntry>();

            // Executed jobs from the chain context (no lock needed — ConcurrentDictionary lookup)
            if (_chainScopeRegistry.TryGetChainScope(chainId, out var scope))
            {
                var context = scope.ServiceProvider.GetService<JobChainContextAccessor>()?.GetCurrentContext();
                if (context != null)
                {
                    foreach (var outcome in context.GetAllOutcomes())
                        jobs.Add(new ChainJobEntry(outcome.JobKey, outcome.Status.ToString()));
                }
            }

            foreach (var (_, key) in executing)
                jobs.Add(new ChainJobEntry(key, "Executing"));

            foreach (var key in pending)
                jobs.Add(new ChainJobEntry(key, "Pending"));

            result.Add(new ChainDebugInfo(chainId, jobs));
        }
        return result;
    }

    private void WalkChainChildren(Guid parentId, List<string> keys)
    {
        if (!_afterParentCallbacks.TryGetValue(parentId, out var children)) return;
        foreach (var (jobKey, (ctx, _)) in children)
        {
            keys.Add(jobKey);
            WalkChainChildren(ctx.Job.Id, keys);
        }
    }

    public IReadOnlyDictionary<string, PoolStatus> GetPoolStatus() =>
        _allPools.ToDictionary(p => p.Name, p => p.GetStatus());

    public bool IsQueued(string jobKey)
    {
        lock (_gate) return _jobKeyIndex.ContainsKey(jobKey);
    }

    /// <summary>
    /// Resolves a job id to its current job key by searching executing jobs first, then every
    /// pool sub-queue. Returns <c>null</c> if no job with that id is currently in the system
    /// (e.g. a parent that has already completed). Used to surface parent linkage by key.
    /// </summary>
    public string? TryGetJobKey(Guid id)
    {
        lock (_gate)
        {
            if (_executingSet.TryGetValue(id, out var entry)) return entry.JobKey;
        }
        foreach (var pool in _allPools)
            if (pool.TryGetJobKey(id, out var key)) return key;
        return null;
    }

    public QueueMetricsSnapshot GetMetrics()
    {
        var poolStatus = GetPoolStatus();

        Dictionary<string, (int Waiting, int Executing)> typeCounts;
        lock (_gate)
        {
            typeCounts = _typeRunningCounts.ToDictionary(
                kv => JobTypeNames.Key(kv.Key),
                kv => (0, kv.Value));
        }

        // Count waiting jobs per type from pool sub-queues; derive retrying count from same snapshot.
        var totalRetrying = 0;
        foreach (var pool in _allPools)
        {
            foreach (var job in pool.GetWaitingSnapshot())
            {
                if (job.RetryCount > 0) totalRetrying++;
                var type = ResolveType(job.JobType);
                if (type == null) continue;
                typeCounts.TryGetValue(JobTypeNames.Key(type), out var existing);
                typeCounts[JobTypeNames.Key(type)] = (existing.Waiting + 1, existing.Executing);
            }
        }

        var totalBlocked = poolStatus.Values.Sum(p => p.BlockedCount);

        return _metrics.GetSnapshot(poolStatus, typeCounts, _typeFriendlyNames, totalBlocked, totalRetrying);
    }

    /// <summary>
    /// Called by the worker after the job instance has been resolved and
    /// <see cref="Abstractions.IQueueJob.PostInit"/> has run, to store the display-friendly
    /// type name, title, and detail pairs on the executing entry.
    /// </summary>
    public void UpdateExecutingItem(Guid id, string typeName, string title, Dictionary<string, object> details)
    {
        lock (_gate)
        {
            if (_executingSet.TryGetValue(id, out var entry))
                _executingSet[id] = entry with { TypeName = typeName, Title = title, Details = details };
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _persistenceBuffer.DisposeAsync();
    }

    // ── Chain abort ────────────────────────────────────────────────────────────

    /// <summary>
    /// Ends an executing job that stopped after a user cancelled it: frees its key, deletes it,
    /// and aborts what was to run after it, as a <see cref="ChainAbortException"/> would, with
    /// <see cref="ChainFinallyAttribute"/> jobs still running. It is not retried, even when it
    /// failed for another reason.
    /// </summary>
    /// <param name="id">The ID of the executing job.</param>
    /// <param name="ex">The exception the job stopped with.</param>
    /// <param name="failed">
    /// Whether the job failed rather than stopped, in which case whoever awaits it gets
    /// <paramref name="ex"/> instead of a cancellation.
    /// </param>
    /// <returns>A task that completes once the chain outcome is recorded.</returns>
    internal async Task OnCancelledAsync(Guid id, Exception ex, bool failed = false)
    {
        ExecutingEntry entry;
        lock (_gate)
        {
            if (!_executingSet.TryGetValue(id, out entry)) return;
        }

        if (failed)
        {
            _metrics.RecordFailure(JobTypeNames.Key(entry.JobType), entry.PoolName);
            _logger.LogInformation("Job {JobKey} ({JobType}) failed after it was cancelled and is not retried", entry.JobKey, JobTypeNames.Short(entry.JobType));
        }
        else
        {
            _logger.LogInformation("Job {JobKey} ({JobType}) was cancelled", entry.JobKey, JobTypeNames.Short(entry.JobType));
        }

        await HandleChainAbortAsync(id, entry, ex, cancelled: !failed, CancellationToken.None);
    }

    private async Task HandleChainAbortAsync(Guid id, ExecutingEntry entry, Exception ex, bool cancelled, CancellationToken ct)
    {
        List<TaskCompletionSource<bool>>? completions = null;
        List<(EnqueueContext Ctx, WorkerPool Pool)> finallyJobs;
        List<EnqueueContext> skippedJobs;

        lock (_gate)
        {
            TryRemoveExecuting_UnderLock(id, out _);
            RemoveKeyIfOwned_UnderLock(entry.JobKey, id);
            _allKnownJobIds.Remove(id);
            _immediateCallbacks.Remove(entry.JobKey, out completions);
            (finallyJobs, skippedJobs) = CollectChainDescendants_UnderLock(id);
        }

        if (cancelled)
            completions?.ForEach(tcs => tcs.TrySetCanceled());
        else
            completions?.ForEach(tcs => tcs.TrySetException(ex));

        // Delete skipped children from DB and clear their keys
        foreach (var skipped in skippedJobs)
            _persistenceBuffer.OnComplete(skipped.Job.Id);

        ActivateChainFinallyJobs(finallyJobs);

        if (entry.ChainId.HasValue)
            await RecordChainAbortAsync(entry.ChainId.Value, SkippedOutcomes(skippedJobs), completeScope: finallyJobs.Count == 0, ct);

        _persistenceBuffer.OnComplete(id);
        SignalAllPools();
    }

    /// <summary>
    /// Moves <see cref="ChainFinallyAttribute"/> jobs of an aborted chain from deferred to waiting.
    /// </summary>
    private void ActivateChainFinallyJobs(List<(EnqueueContext Ctx, WorkerPool Pool)> finallyJobs)
    {
        foreach (var (ctx, pool) in finallyJobs)
        {
            pool.AddToQueue(ctx.Job);
            _persistenceBuffer.OnActivateChainChild(ctx.Job.Id);
            _metrics.RecordEnqueue(JobTypeNames.Key(ctx.Type), pool.Name);
        }
    }

    private static IEnumerable<JobOutcome> SkippedOutcomes(IEnumerable<EnqueueContext> skippedJobs)
        => skippedJobs.Select(j => new JobOutcome
        {
            JobId = j.Job.Id,
            JobType = j.Job.JobType,
            JobKey = j.Job.JobKey,
            Status = JobOutcomeStatus.Skipped,
            CompletedAt = DateTimeOffset.UtcNow,
        });

    /// <summary>
    /// Marks a chain as aborted in its persisted context and records the outcomes of the jobs
    /// that will not run. Failures are logged, never thrown.
    /// </summary>
    /// <param name="chainId">The chain's ID.</param>
    /// <param name="outcomes">The outcomes to record.</param>
    /// <param name="completeScope">Whether nothing of the chain is left to run, so its scope can go.</param>
    /// <param name="ct">Cancels reading the chain context.</param>
    private async Task RecordChainAbortAsync(Guid chainId, IEnumerable<JobOutcome> outcomes, bool completeScope, CancellationToken ct)
    {
        var outcomeList = outcomes.ToList();
        try
        {
            if (_chainScopeRegistry.TryGetChainScope(chainId, out var chainScope))
            {
                var repo = chainScope.ServiceProvider.GetRequiredService<IJobChainContextRepository>();
                var ctx = await repo.GetAsync(chainId, ct) ?? new JobChainContext(chainId);
                ctx.SetStatus(ChainStatus.Aborted);
                foreach (var outcome in outcomeList) ctx.AddOutcome(outcome);
                await repo.SaveAsync(ctx, CancellationToken.None);
            }
            else
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IJobChainContextRepository>();
                await repo.AddOutcomesAsync(chainId, outcomeList, CancellationToken.None);
            }
        }
        catch (Exception chainEx)
        {
            _logger.LogError(chainEx, "Failed to record chain abort outcomes for chain {ChainId}", chainId);
        }

        if (completeScope)
            _chainScopeRegistry.CompleteChainScope(chainId);
    }

    /// <summary>
    /// Recursively collects descendants of <paramref name="parentId"/> from <see cref="_afterParentCallbacks"/>.
    /// Jobs marked <see cref="ChainFinallyAttribute"/> are returned as <c>finallyJobs</c> (to be activated);
    /// all others are returned as <c>skippedJobs</c> (to be discarded). Children of the parent in
    /// <paramref name="keepChainId"/> stay registered, descendants and all. Must be called under <see cref="_gate"/>.
    /// </summary>
    /// <param name="parentId">The job whose descendants to collect.</param>
    /// <param name="keepChainId">The chain whose direct children of the parent stay, or <c>null</c> for none.</param>
    /// <returns>The finally jobs to activate and the jobs to drop.</returns>
    private (List<(EnqueueContext Ctx, WorkerPool Pool)> FinallyJobs, List<EnqueueContext> SkippedJobs)
        CollectChainDescendants_UnderLock(Guid parentId, Guid? keepChainId = null)
    {
        var finallyJobs = new List<(EnqueueContext, WorkerPool)>();
        var skippedJobs = new List<EnqueueContext>();

        if (!_afterParentCallbacks.Remove(parentId, out var children))
            return (finallyJobs, skippedJobs);

        Dictionary<string, (EnqueueContext Ctx, WorkerPool Pool)>? kept = null;
        foreach (var (jobKey, (ctx, pool)) in children)
        {
            if (keepChainId.HasValue && ctx.Job.ChainId == keepChainId)
            {
                kept ??= new Dictionary<string, (EnqueueContext, WorkerPool)>(StringComparer.Ordinal);
                kept[jobKey] = (ctx, pool);
                continue;
            }

            _jobKeyIndex.Remove(ctx.Job.JobKey);
            _allKnownJobIds.Remove(ctx.Job.Id);

            if (ctx.Job.IsChainFinally)
            {
                // Keep this job — it must run even after chain abort.
                // Re-claim its key so it stays in the system.
                _jobKeyIndex[ctx.Job.JobKey] = ctx.Job.Id;
                _allKnownJobIds.Add(ctx.Job.Id);
                finallyJobs.Add((ctx, pool));
            }
            else
            {
                skippedJobs.Add(ctx);
                var (subFinally, subSkipped) = CollectChainDescendants_UnderLock(ctx.Job.Id);
                finallyJobs.AddRange(subFinally);
                skippedJobs.AddRange(subSkipped);
            }
        }

        if (kept != null)
            _afterParentCallbacks[parentId] = kept;

        return (finallyJobs, skippedJobs);
    }

    // ── Pool priority ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if <paramref name="pool"/> should attempt job acquisition.
    /// Higher-priority pools (lower <see cref="WorkerPool.WorkerPriority"/> value) claim their
    /// <see cref="WorkerPool.MaxWorkers"/> slots first; this pool may proceed only if the
    /// remaining available slots exceed the total reserved for all higher-priority pools that
    /// currently have runnable jobs.
    /// </summary>
    private bool ShouldPoolAttemptAcquisition(WorkerPool pool)
    {
        var priority = pool.WorkerPriority;

        // Highest-priority tier — no higher pool to yield to.
        if (_allPools.All(p => p.WorkerPriority >= priority))
            return true;

        var available = _maxTotalWorkers - ExecutingCount;
        if (available <= 0) return false;

        // Each higher-priority pool reserves as many slots as it has runnable jobs, capped at MaxWorkers.
        var reserved = 0;
        foreach (var p in _allPools)
        {
            if (p.WorkerPriority >= priority) continue;
            reserved += p.RunnableCount(p.MaxWorkers);
        }

        return available > reserved;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a short type name → IQueueJob.TypeName map for all registered job types using
    /// uninitialized instances. TypeName is always a string-literal override so no injected
    /// services are needed — best-effort; failures are silently skipped.
    /// </summary>
    private IReadOnlyDictionary<string, string> BuildFriendlyNames()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var type in _poolsByType.Keys)
        {
            try
            {
                var inst = (IQueueJob)RuntimeHelpers.GetUninitializedObject(type);
                var friendly = inst.TypeName;
                if (!string.IsNullOrEmpty(friendly))
                    map[JobTypeNames.Key(type)] = friendly;
            }
            catch { /* best-effort */ }
        }
        return map;
    }

    private void DecrementCounts(Type jobType, string? group)
    {
        if (_typeRunningCounts.TryGetValue(jobType, out var tc) && tc > 0)
            _typeRunningCounts[jobType] = tc - 1;
        if (group != null && _groupRunningCounts.TryGetValue(group, out var gc) && gc > 0)
            _groupRunningCounts[group] = gc - 1;
        if (_globalRunning > 0) _globalRunning--;
    }

    private void SignalAllPools()
    {
        foreach (var pool in _allPools) pool.Signal();
    }

    private Type? ResolveType(string jobTypeName) =>
        _typeByName.TryGetValue(jobTypeName, out var t) ? t : null;

    /// <summary>
    /// Reconstructs an <see cref="EnqueueContext"/> from a persisted <see cref="QueuedJob"/> record
    /// at startup. Uses a stub <see cref="QueueItem"/> since the live instance display fields
    /// (TypeName, Title, Details) are populated by the worker when the job executes.
    /// </summary>
    private static EnqueueContext BuildEnqueueContextFromDb(QueuedJob job, Type type) =>
        new()
        {
            Job = job,
            Type = type,
            DisplayItem = new QueueItem
            {
                Key = job.JobKey,
                JobType = JobTypeNames.Short(type),
                TypeName = JobTypeNames.Short(type),
                Title = string.Empty,
                Details = [],
                Running = false,
            },
        };
}
