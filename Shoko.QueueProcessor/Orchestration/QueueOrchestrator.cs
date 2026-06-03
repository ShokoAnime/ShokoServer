using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
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
    private readonly Dictionary<Type, int> _typeRunningCounts = new();
    private readonly Dictionary<string, int> _groupRunningCounts = new();
    private volatile int _globalRunning;

    // Pool routing — populated by Initialize()
    private readonly Dictionary<Type, WorkerPool> _poolsByType = new();
    private IReadOnlyList<WorkerPool> _allPools = [];

    // O(1) type resolution — avoids Type.GetType() (assembly scan) on every enqueue/acquire call
    private readonly Dictionary<string, Type> _typeByName = new(StringComparer.Ordinal);

    // Friendly display names per type (type.Name → IQueueJob.TypeName) — built once at Initialize()
    private IReadOnlyDictionary<string, string> _typeFriendlyNames = new Dictionary<string, string>();

    // O(1) dedup index: JobKey → Id (covers waiting + executing + pending-insert + after-parent)
    private readonly Dictionary<string, Guid> _jobKeyIndex = new(StringComparer.Ordinal);

    // Pending completion callbacks registered by EnqueueImmediate callers.
    // Keyed by JobKey; resolved in OnComplete or faulted in OnFailureAsync (real failures only).
    private readonly Dictionary<string, List<TaskCompletionSource<bool>>> _immediateCallbacks = new(StringComparer.Ordinal);

    // Jobs registered via RunAfterCurrent: held until their parent job completes, then released
    // at int.MaxValue priority. Keys are claimed in _jobKeyIndex immediately on registration to
    // prevent a concurrent Enqueue from scheduling the same job before the parent finishes.
    // Inner key is JobKey for O(1) dedup within one parent execution.
    private readonly Dictionary<Guid, Dictionary<string, (EnqueueContext Ctx, WorkerPool Pool)>>
        _afterParentCallbacks = new();

    // IDs of waiting jobs pulled from their pool sub-queues by RegisterAfterParent but not yet
    // physically removed (the removal happens outside _gate to avoid lock-order inversion with
    // WorkerPool._subQueueLock). TryRegisterExecuting rejects any ID in this set so a worker
    // cannot acquire the job during the brief window between the two operations.
    private readonly HashSet<Guid> _heldForParent = new();

    // All job IDs currently "in the system" regardless of state: waiting, executing, held, or
    // registered as an after-parent callback. Allows RegisterChainAfterJob to distinguish a
    // waiting parent (still in system) from a completed one (already removed).
    // Maintained in sync with _jobKeyIndex — every Add/Remove to _jobKeyIndex must mirror here.
    private readonly HashSet<Guid> _allKnownJobIds = new();

    private readonly object _gate = new();
    private volatile bool _paused;

    public bool IsPaused => _paused;

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
                _typeByName[type.FullName + ", " + type.Assembly.GetName().Name] = type;
            }
            pool.TryRegisterExecuting = TryRegisterExecuting;

            // Capture pool reference for the closure. Skip the check for highest-priority pools.
            var capturedPool = pool;
            pool.ShouldAttemptAcquisition = () => ShouldPoolAttemptAcquisition(capturedPool);
        }

        var activeCount = 0;
        var deferred = new Dictionary<Guid, QueuedJob>(); // jobId → job, for chain-deferred jobs

        foreach (var job in persistedJobs)
        {
            var type = ResolveType(job.JobType);
            if (type == null)
            {
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

        _typeFriendlyNames = BuildFriendlyNames();
        _logger.LogInformation("QueueOrchestrator initialized with {Count} active jobs and {Deferred} deferred across {Pools} pools",
            activeCount, deferred.Count - orphans.Count, pools.Count);
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

        lock (_gate)
        {
            if (_jobKeyIndex.ContainsKey(job.JobKey))
                return Task.CompletedTask;  // already queued or executing
            _jobKeyIndex[job.JobKey] = job.Id;
            _allKnownJobIds.Add(job.Id);
        }

        pool.AddToQueue(job);
        _persistenceBuffer.OnEnqueue(job);
        _metrics.RecordEnqueue(type.Name, pool.Name);

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
        }

        if (toEnqueue.Count == 0) return Task.CompletedTask;

        // Group by pool and batch-insert into each pool's sub-queue (one lock per pool).
        var poolBatches = new Dictionary<WorkerPool, List<QueuedJob>>();
        foreach (var (ctx, pool) in toEnqueue)
        {
            if (!poolBatches.TryGetValue(pool, out var batch))
                poolBatches[pool] = batch = new List<QueuedJob>();
            batch.Add(ctx.Job);
            _metrics.RecordEnqueue(ctx.Type.Name, pool.Name);
        }

        foreach (var (pool, batch) in poolBatches)
            pool.AddRangeToQueue(batch);

        // Single persistence-buffer call for the entire batch.
        _persistenceBuffer.OnEnqueueBatch(toEnqueue.Select(e => e.Ctx.Job));

        if (!_paused) SignalAllPools();

        var items = toEnqueue.Select(e => string.IsNullOrEmpty(e.Ctx.DisplayItem.PoolName)
            ? e.Ctx.DisplayItem with { PoolName = e.Pool.Name }
            : e.Ctx.DisplayItem).ToList();
        FireJobsAdded(items);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns <see langword="true"/> if any acquisition filter on the pool that handles
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
                _persistenceBuffer.OnEnqueue(job);
                _metrics.RecordEnqueue(type.Name, pool.Name);
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
            if (_globalRunning >= _maxTotalWorkers) return false;
            if (!_concurrency.CanRun(type, _typeRunningCounts, _groupRunningCounts)) return false;
            if (_heldForParent.Contains(job.Id)) return false;

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
                IsChainFinally: job.IsChainFinally);
        }
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
        // ID of a waiting job that must be pulled from its pool sub-queue. The removal happens
        // outside _gate (see below) to avoid lock-order inversion with WorkerPool._subQueueLock;
        // _heldForParent blocks acquisition in the interim.
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
                _heldForParent.Add(existingId);
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
            lock (_gate) _heldForParent.Remove(heldId);
        }

        if (immediateEnqueue != null)
        {
            foreach (var (c, pool) in immediateEnqueue)
            {
                pool.AddToQueue(c.Job);
                _persistenceBuffer.OnEnqueue(c.Job);
                _metrics.RecordEnqueue(c.Type.Name, pool.Name);
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
                _heldForParent.Add(existingId);
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
            }
        }

        if (heldId != Guid.Empty)
        {
            foreach (var pool in _allPools)
                pool.RemoveFromQueue(heldId);
            // Remove the old DB record so it doesn't re-appear as an orphan on restart.
            _persistenceBuffer.OnComplete(heldId);
            lock (_gate) _heldForParent.Remove(heldId);
        }

        if (registerAsDeferred)
        {
            // Persist with ParentJobId so the child survives a restart while waiting
            ctx.Job.ParentJobId = parentId;
            _persistenceBuffer.OnEnqueue(ctx.Job);
            _metrics.RecordEnqueue(ctx.Type.Name, targetPool.Name);
        }

        if (immediateEnqueue != null)
        {
            foreach (var (c, pool) in immediateEnqueue)
            {
                pool.AddToQueue(c.Job);
                _persistenceBuffer.OnEnqueue(c.Job);
                _metrics.RecordEnqueue(c.Type.Name, pool.Name);
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
            if (!_executingSet.Remove(id, out var entry)) return;
            DecrementCounts(entry.JobType, entry.ConcurrencyGroup);
            _jobKeyIndex.Remove(entry.JobKey);
            _allKnownJobIds.Remove(id);
            _immediateCallbacks.Remove(entry.JobKey, out completions);
            chainId = entry.ChainId;

            if (_afterParentCallbacks.Remove(id, out var deferredMap))
                deferred = [..deferredMap.Values];
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
                _metrics.RecordEnqueue(ctx.Type.Name, pool.Name);
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
    /// When <paramref name="incrementRetry"/> is false the job is re-queued immediately at its
    /// original priority without touching <see cref="QueuedJob.RetryCount"/> or the DB — used
    /// by <c>RequeueJobException</c> for filter-managed transient conditions.
    /// </summary>
    public async Task OnFailureAsync(Guid id, Exception ex, bool incrementRetry = true, CancellationToken ct = default)
    {
        ExecutingEntry entry;
        List<TaskCompletionSource<bool>>? completions = null;
        Dictionary<string, (EnqueueContext Ctx, WorkerPool Pool)>? discardedChildren = null;
        lock (_gate)
        {
            if (!_executingSet.TryGetValue(id, out entry)) return;
        }

        // Chain abort: short-circuit remaining chain jobs (except [ChainFinally] ones)
        if (ex is ChainAbortException)
        {
            await HandleChainAbortAsync(id, entry, ex, ct);
            return;
        }

        lock (_gate)
        {
            _executingSet.Remove(id, out _);
            DecrementCounts(entry.JobType, entry.ConcurrencyGroup);

            // For real failures, capture and clear callbacks so they can be faulted.
            // RequeueJobException (incrementRetry=false) leaves callbacks intact: the job
            // re-queues with the same key and the TCS resolves on eventual completion.
            // After-parent registrations follow the same rule: preserved on requeue, discarded
            // on real failure so _jobKeyIndex entries don't permanently block future enqueues.
            if (incrementRetry)
            {
                _immediateCallbacks.Remove(entry.JobKey, out completions);
                _afterParentCallbacks.Remove(id, out discardedChildren);
                _allKnownJobIds.Remove(id);
            }
        }

        if (discardedChildren != null)
        {
            lock (_gate)
            {
                foreach (var (jobKey, (ctx, _)) in discardedChildren)
                {
                    _jobKeyIndex.Remove(jobKey);
                    _allKnownJobIds.Remove(ctx.Job.Id);
                }
            }
            // Delete persisted chain children from DB (they were inserted with ParentJobId set)
            foreach (var (_, (ctx, _)) in discardedChildren)
                _persistenceBuffer.OnComplete(ctx.Job.Id);
        }

        // Re-queue without retry increment (RequeueJobException path)
        if (!incrementRetry)
        {
            var requeueJob = new QueuedJob
            {
                Id = id,
                JobType = entry.JobType.FullName + ", " + entry.JobType.Assembly.GetName().Name,
                JobKey = entry.JobKey,
                JobDataJson = entry.JobDataJson,
                Priority = entry.Priority,
                QueuedAt = DateTimeOffset.UtcNow,
                ScheduledAt = null,
                RetryCount = entry.RetryCount  // unchanged
            };
            lock (_gate) { _jobKeyIndex[entry.JobKey] = id; _allKnownJobIds.Add(id); }
            if (_poolsByType.TryGetValue(entry.JobType, out var requeuePool))
                requeuePool.AddToQueue(requeueJob);
            SignalAllPools();
            return;
        }

        // Fault any immediate callers waiting on this job
        completions?.ForEach(tcs => tcs.TrySetException(ex));

        var policy = _retryPolicies.For(entry.JobType);
        _metrics.RecordFailure(entry.JobType.Name, entry.PoolName);

        if (policy.ShouldDiscard(entry.RetryCount))
        {
            _logger.LogError(ex,
                "Job {JobKey} ({JobType}) discarded after {Retries} retries",
                entry.JobKey, entry.JobType.Name, policy.MaxRetries);

            lock (_gate) { _jobKeyIndex.Remove(entry.JobKey); _allKnownJobIds.Remove(id); }
            _persistenceBuffer.OnComplete(id);  // buffer the DELETE
        }
        else
        {
            var delay = policy.GetDelay(entry.RetryCount);
            var nextRun = DateTimeOffset.UtcNow.Add(delay);
            var newRetryCount = entry.RetryCount + 1;

            _logger.LogWarning(ex,
                "Job {JobKey} ({JobType}) failed — retry {N}/{Max} in {Delay:g}",
                entry.JobKey, entry.JobType.Name, newRetryCount, policy.MaxRetries, delay);

            // Write immediately so crash-restart preserves the backoff position
            using (var scope = _scopeFactory.CreateScope())
                await scope.ServiceProvider.GetRequiredService<IJobRepository>()
                    .UpdateRetryAsync(id, newRetryCount, nextRun, ct);

            // Re-queue in-memory with updated ScheduledAt and incremented RetryCount
            var retryJob = new QueuedJob
            {
                Id = id,
                JobType = entry.JobType.FullName + ", " + entry.JobType.Assembly.GetName().Name,
                JobKey = entry.JobKey,
                JobDataJson = entry.JobDataJson,
                Priority = entry.Priority,
                QueuedAt = DateTimeOffset.UtcNow,
                ScheduledAt = nextRun,
                RetryCount = newRetryCount
            };

            if (_poolsByType.TryGetValue(entry.JobType, out var pool))
                pool.AddToQueue(retryJob);
        }

        SignalAllPools();
    }

    public void Pause()
    {
        _paused = true;
        _logger.LogInformation("Queue paused");
    }

    public void Resume()
    {
        _paused = false;
        _logger.LogInformation("Queue resumed");
        SignalAllPools();
    }

    public async Task RemoveAsync(string jobKey, CancellationToken ct = default)
    {
        Guid id;
        lock (_gate)
        {
            if (!_jobKeyIndex.TryGetValue(jobKey, out id))
                return;

            // Don't remove executing jobs — they're already past the point of no return.
            if (_executingSet.ContainsKey(id))
                return;

            foreach (var pool in _allPools)
                pool.RemoveFromQueue(id);

            _jobKeyIndex.Remove(jobKey);
            _allKnownJobIds.Remove(id);
        }

        using var scope = _scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IJobRepository>().DeleteAsync(id, ct);
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            foreach (var pool in _allPools)
                pool.ClearQueue();
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
        using var scope = _scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IJobRepository>().ClearAllAsync(ct);
    }

    // ── State queries ──────────────────────────────────────────────────────────

    public int WaitingCount => _allPools.Sum(p => p.WaitingCount);

    /// <summary>
    /// Number of waiting jobs whose type is currently excluded by an acquisition filter.
    /// Computed on demand — do not call on the hot path.
    /// </summary>
    public int BlockedWaitingCount => _allPools.Sum(p => p.BlockedCount);

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
    /// Returns true if the job's type is currently excluded by an acquisition filter on its pool.
    /// Used to populate <see cref="Abstractions.QueueItem.Blocked"/> for waiting jobs.
    /// </summary>
    public bool IsJobBlocked(QueuedJob job)
    {
        var type = ResolveType(job.JobType);
        if (type == null) return false;
        return _poolsByType.TryGetValue(type, out var pool) && pool.IsTypeBlocked(type);
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

    public IReadOnlyList<ExecutingEntry> GetExecuting()
    {
        lock (_gate) return [.._executingSet.Values];
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

    public QueueMetricsSnapshot GetMetrics()
    {
        var poolStatus = GetPoolStatus();

        Dictionary<string, (int Waiting, int Executing)> typeCounts;
        lock (_gate)
        {
            typeCounts = _typeRunningCounts.ToDictionary(
                kv => kv.Key.Name,
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
                typeCounts.TryGetValue(type.Name, out var existing);
                typeCounts[type.Name] = (existing.Waiting + 1, existing.Executing);
            }
        }

        var totalBlocked = poolStatus.Values
            .Where(p => p.IsBlocked)
            .Sum(p => p.WaitingCount);

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

    private async Task HandleChainAbortAsync(Guid id, ExecutingEntry entry, Exception ex, CancellationToken ct)
    {
        List<TaskCompletionSource<bool>>? completions = null;
        List<(EnqueueContext Ctx, WorkerPool Pool)> finallyJobs;
        List<EnqueueContext> skippedJobs;

        lock (_gate)
        {
            _executingSet.Remove(id, out _);
            DecrementCounts(entry.JobType, entry.ConcurrencyGroup);
            _jobKeyIndex.Remove(entry.JobKey);
            _allKnownJobIds.Remove(id);
            _immediateCallbacks.Remove(entry.JobKey, out completions);
            (finallyJobs, skippedJobs) = CollectChainDescendants_UnderLock(id);
        }

        completions?.ForEach(tcs => tcs.TrySetException(ex));

        // Delete skipped children from DB and clear their keys
        foreach (var skipped in skippedJobs)
            _persistenceBuffer.OnComplete(skipped.Job.Id);

        // Activate finally jobs (promote from deferred to active)
        foreach (var (ctx, pool) in finallyJobs)
        {
            pool.AddToQueue(ctx.Job);
            _persistenceBuffer.OnActivateChainChild(ctx.Job.Id);
            _metrics.RecordEnqueue(ctx.Type.Name, pool.Name);
        }

        // Record skipped outcomes + mark chain aborted in persisted chain context
        if (entry.ChainId.HasValue)
        {
            var skippedOutcomes = skippedJobs.Select(j => new JobOutcome
            {
                JobId = j.Job.Id,
                JobType = j.Job.JobType,
                Status = JobOutcomeStatus.Skipped,
                CompletedAt = DateTimeOffset.UtcNow,
            }).ToList();

            try
            {
                if (_chainScopeRegistry.TryGetChainScope(entry.ChainId.Value, out var chainScope))
                {
                    var repo = chainScope.ServiceProvider.GetRequiredService<IJobChainContextRepository>();
                    var ctx = await repo.GetAsync(entry.ChainId.Value, ct) ?? new JobChainContext(entry.ChainId.Value);
                    ctx.SetStatus(ChainStatus.Aborted);
                    foreach (var outcome in skippedOutcomes) ctx.AddOutcome(outcome);
                    await repo.SaveAsync(ctx, CancellationToken.None);
                }
                else
                {
                    using var scope = _scopeFactory.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IJobChainContextRepository>();
                    await repo.AddOutcomesAsync(entry.ChainId.Value, skippedOutcomes, CancellationToken.None);
                }
            }
            catch (Exception chainEx)
            {
                _logger.LogError(chainEx, "Failed to record chain abort outcomes for chain {ChainId}", entry.ChainId);
            }

            if (finallyJobs.Count == 0)
                _chainScopeRegistry.CompleteChainScope(entry.ChainId.Value);
        }

        _persistenceBuffer.OnComplete(id);
        SignalAllPools();
    }

    /// <summary>
    /// Recursively collects descendants of <paramref name="parentId"/> from <see cref="_afterParentCallbacks"/>.
    /// Jobs marked <see cref="ChainFinallyAttribute"/> are returned as <c>finallyJobs</c> (to be activated);
    /// all others are returned as <c>skippedJobs</c> (to be discarded). Must be called under <see cref="_gate"/>.
    /// </summary>
    private (List<(EnqueueContext Ctx, WorkerPool Pool)> FinallyJobs, List<EnqueueContext> SkippedJobs)
        CollectChainDescendants_UnderLock(Guid parentId)
    {
        var finallyJobs = new List<(EnqueueContext, WorkerPool)>();
        var skippedJobs = new List<EnqueueContext>();

        if (!_afterParentCallbacks.Remove(parentId, out var children))
            return (finallyJobs, skippedJobs);

        foreach (var (_, (ctx, pool)) in children)
        {
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
    /// Builds a type.Name → IQueueJob.TypeName map for all registered job types using
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
                    map[type.Name] = friendly;
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
                JobType = type.Name,
                TypeName = type.Name,
                Title = string.Empty,
                Details = [],
                Running = false,
            },
        };
}
