using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
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
    private readonly IJobRepository _repo;
    private readonly ConcurrencyRegistry _concurrency;
    private readonly RetryPolicyResolver _retryPolicies;
    private readonly QueueMetrics _metrics;
    private readonly QueueStateEventHandler _events;
    private readonly int _maxTotalWorkers;

    // Executing state — all fields guarded by _gate
    private readonly Dictionary<Guid, ExecutingEntry> _executingSet = new();
    private readonly Dictionary<Type, int> _typeRunningCounts = new();
    private readonly Dictionary<string, int> _groupRunningCounts = new();
    private int _globalRunning;

    // Pool routing — populated by Initialize()
    private readonly Dictionary<Type, WorkerPool> _poolsByType = new();
    private IReadOnlyList<WorkerPool> _allPools = [];

    // O(1) type resolution — avoids Type.GetType() (assembly scan) on every enqueue/acquire call
    private readonly Dictionary<string, Type> _typeByName = new(StringComparer.Ordinal);

    // Friendly display names per type (type.Name → IQueueJob.TypeName) — built once at Initialize()
    private IReadOnlyDictionary<string, string> _typeFriendlyNames = new Dictionary<string, string>();

    // O(1) dedup index: JobKey → Id (covers waiting + executing + pending-insert)
    private readonly Dictionary<string, Guid> _jobKeyIndex = new(StringComparer.Ordinal);

    // Pending completion callbacks registered by EnqueueImmediate callers.
    // Keyed by JobKey; resolved in OnComplete or faulted in OnFailureAsync (real failures only).
    private readonly Dictionary<string, List<TaskCompletionSource<bool>>> _immediateCallbacks = new(StringComparer.Ordinal);

    private readonly object _gate = new();
    private volatile bool _paused;

    public bool IsPaused => _paused;

    public QueueOrchestrator(
        ILogger<QueueOrchestrator> logger,
        PersistenceBuffer persistenceBuffer,
        IJobRepository repo,
        ConcurrencyRegistry concurrency,
        RetryPolicyResolver retryPolicies,
        QueueMetrics metrics,
        QueueStateEventHandler events,
        int maxTotalWorkers)
    {
        _logger = logger;
        _persistenceBuffer = persistenceBuffer;
        _repo = repo;
        _concurrency = concurrency;
        _retryPolicies = retryPolicies;
        _metrics = metrics;
        _events = events;
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
        }

        var count = 0;
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

            pool.AddToQueue(job);
            _jobKeyIndex[job.JobKey] = job.Id;
            count++;
        }

        _typeFriendlyNames = BuildFriendlyNames();
        _logger.LogInformation("QueueOrchestrator initialized with {Count} jobs across {Pools} pools", count, pools.Count);
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

            _globalRunning++;
            _typeRunningCounts[type] = (_typeRunningCounts.GetValueOrDefault(type)) + 1;

            var group = _concurrency.GetGroup(type);
            if (group != null)
                _groupRunningCounts[group] = (_groupRunningCounts.GetValueOrDefault(group)) + 1;

            _poolsByType.TryGetValue(type, out var pool);
            _executingSet[job.Id] = new ExecutingEntry(
                job.Id, type, job.JobKey, job.JobDataJson,
                job.Priority, job.RetryCount, group,
                DateTime.UtcNow, pool?.Name ?? string.Empty);
        }
        return true;
    }

    /// <summary>
    /// Called by workers on success. Updates counts, buffers DB delete, signals pools.
    /// </summary>
    public void OnComplete(Guid id)
    {
        List<TaskCompletionSource<bool>>? completions = null;
        lock (_gate)
        {
            if (!_executingSet.Remove(id, out var entry)) return;
            DecrementCounts(entry.JobType, entry.ConcurrencyGroup);
            _jobKeyIndex.Remove(entry.JobKey);
            _immediateCallbacks.Remove(entry.JobKey, out completions);
        }

        completions?.ForEach(tcs => tcs.TrySetResult(true));
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
        lock (_gate)
        {
            if (!_executingSet.Remove(id, out entry)) return;
            DecrementCounts(entry.JobType, entry.ConcurrencyGroup);

            // For real failures, capture and clear callbacks so they can be faulted.
            // RequeueJobException (incrementRetry=false) leaves callbacks intact: the job
            // re-queues with the same key and the TCS resolves on eventual completion.
            if (incrementRetry)
                _immediateCallbacks.Remove(entry.JobKey, out completions);
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
            lock (_gate) _jobKeyIndex[entry.JobKey] = id;
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

            lock (_gate) _jobKeyIndex.Remove(entry.JobKey);
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
            await _repo.UpdateRetryAsync(id, newRetryCount, nextRun, ct);

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

    public async Task ClearAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            foreach (var pool in _allPools)
                pool.ClearQueue();
            _jobKeyIndex.Clear();
            // Remove non-executing keys from index (executing jobs keep their keys until complete)
            foreach (var entry in _executingSet.Values)
                _jobKeyIndex[entry.JobKey] = entry.Id;
        }
        await _repo.ClearAllAsync(ct);
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

    public int ExecutingCount { get { lock (_gate) return _executingSet.Count; } }

    public IReadOnlyList<ExecutingEntry> GetExecuting()
    {
        lock (_gate) return [.._executingSet.Values];
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
}
