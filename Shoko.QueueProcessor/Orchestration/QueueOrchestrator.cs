#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Analytics;
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
    private readonly int _maxTotalWorkers;

    // Executing state — all fields guarded by _gate
    private readonly Dictionary<Guid, ExecutingEntry> _executingSet = new();
    private readonly Dictionary<Type, int> _typeRunningCounts = new();
    private readonly Dictionary<string, int> _groupRunningCounts = new();
    private int _globalRunning;

    // Pool routing — populated by Initialize()
    private readonly Dictionary<Type, WorkerPool> _poolsByType = new();
    private readonly Dictionary<string, WorkerPool> _poolsByName = new();
    private IReadOnlyList<WorkerPool> _allPools = [];

    // O(1) type resolution — avoids Type.GetType() (assembly scan) on every enqueue/acquire call
    private readonly Dictionary<string, Type> _typeByName = new(StringComparer.Ordinal);

    // O(1) dedup index: JobKey → Id (covers waiting + executing + pending-insert)
    private readonly Dictionary<string, Guid> _jobKeyIndex = new(StringComparer.Ordinal);

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
        int maxTotalWorkers)
    {
        _logger = logger;
        _persistenceBuffer = persistenceBuffer;
        _repo = repo;
        _concurrency = concurrency;
        _retryPolicies = retryPolicies;
        _metrics = metrics;
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
            _poolsByName[pool.Name] = pool;
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

        _logger.LogInformation("QueueOrchestrator initialized with {Count} jobs across {Pools} pools", count, pools.Count);
    }

    /// <summary>
    /// Enqueues a job: dedup check (O(1)), routes to pool sub-queue, buffers insert, signals pool.
    /// </summary>
    public Task EnqueueAsync(QueuedJob job, CancellationToken ct = default)
    {
        var type = ResolveType(job.JobType)
            ?? throw new InvalidOperationException($"Cannot enqueue: type '{job.JobType}' not found.");

        if (!_poolsByType.TryGetValue(type, out var pool))
            throw new InvalidOperationException($"No pool handles job type '{job.JobType}'.");

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
        return Task.CompletedTask;
    }

    /// <summary>
    /// Enqueues a batch of jobs. Uses a single gate-lock pass for dedup, a single lock per
    /// affected pool for sub-queue insertion, and one persistence-buffer call for the whole batch —
    /// far cheaper than calling <see cref="EnqueueAsync"/> per job when enqueueing thousands of items.
    /// </summary>
    public Task EnqueueRangeAsync(IEnumerable<QueuedJob> jobs, CancellationToken ct = default)
    {
        // Resolve types and pools first — these are stable after Initialize(), no lock needed.
        var resolved = new List<(QueuedJob Job, Type Type, WorkerPool Pool)>();
        foreach (var job in jobs)
        {
            var type = ResolveType(job.JobType);
            if (type == null) continue;
            if (!_poolsByType.TryGetValue(type, out var pool)) continue;
            resolved.Add((job, type, pool));
        }

        if (resolved.Count == 0) return Task.CompletedTask;

        // Single gate-lock pass: dedup and register all keys atomically.
        var toEnqueue = new List<(QueuedJob Job, Type Type, WorkerPool Pool)>(resolved.Count);
        lock (_gate)
        {
            foreach (var entry in resolved)
            {
                if (_jobKeyIndex.ContainsKey(entry.Job.JobKey)) continue;
                _jobKeyIndex[entry.Job.JobKey] = entry.Job.Id;
                toEnqueue.Add(entry);
            }
        }

        if (toEnqueue.Count == 0) return Task.CompletedTask;

        // Group by pool and batch-insert into each pool's sub-queue (one lock per pool).
        var poolBatches = new Dictionary<WorkerPool, List<QueuedJob>>();
        foreach (var (job, type, pool) in toEnqueue)
        {
            if (!poolBatches.TryGetValue(pool, out var batch))
                poolBatches[pool] = batch = new List<QueuedJob>();
            batch.Add(job);
            _metrics.RecordEnqueue(type.Name, pool.Name);
        }

        foreach (var (pool, batch) in poolBatches)
            pool.AddRangeToQueue(batch);

        // Single persistence-buffer call for the entire batch.
        _persistenceBuffer.OnEnqueueBatch(System.Linq.Enumerable.Select(toEnqueue, e => e.Job));

        if (!_paused) SignalAllPools();

        return Task.CompletedTask;
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
        lock (_gate)
        {
            if (!_executingSet.Remove(id, out var entry)) return;
            DecrementCounts(entry.JobType, entry.ConcurrencyGroup);
            _jobKeyIndex.Remove(entry.JobKey);
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
        lock (_gate)
        {
            if (!_executingSet.Remove(id, out entry)) return;
            DecrementCounts(entry.JobType, entry.ConcurrencyGroup);
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

    /// <summary>Total worker slots across all pools.</summary>
    public int TotalWorkerCount => _allPools.Sum(p => p.MaxWorkers);

    /// <summary>
    /// Resolves a fully-qualified job type name to its <see cref="Type"/> using the
    /// pre-built startup cache. Returns <c>null</c> if the type is not registered.
    /// </summary>
    public Type? TryResolveType(string typeName) => ResolveType(typeName);

    /// <summary>
    /// Returns true if the job's type is currently excluded by an acquisition filter on its pool.
    /// Used to populate <see cref="Abstractions.QueueItem.Blocked"/> for waiting jobs.
    /// </summary>
    public bool IsJobBlocked(Storage.QueuedJob job)
    {
        var type = ResolveType(job.JobType);
        if (type == null) return false;
        return _poolsByType.TryGetValue(type, out var pool) && pool.IsTypeBlocked(type);
    }

    /// <summary>Returns waiting jobs across all pools in priority order, optionally paginated.</summary>
    public IReadOnlyList<Storage.QueuedJob> GetWaiting(int maxCount, int offset, Func<Storage.QueuedJob, bool>? filter = null)
    {
        var result = new List<Storage.QueuedJob>();
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
        int totalRetrying = 0;
        lock (_gate)
        {
            typeCounts = _typeRunningCounts.ToDictionary(
                kv => kv.Key.Name,
                kv => (0, kv.Value));
        }

        // Count retrying jobs from pool sub-queues
        foreach (var pool in _allPools)
            totalRetrying += pool.RetryingCount;

        var totalBlocked = poolStatus.Values
            .Where(p => p.IsBlocked)
            .Sum(p => p.WaitingCount);

        return _metrics.GetSnapshot(poolStatus, typeCounts, totalBlocked, totalRetrying);
    }

    /// <summary>
    /// Called by the worker after the job instance has been resolved and
    /// <see cref="Abstractions.IQueueJob.PostInit"/> has run, to store the display-friendly
    /// title and detail pairs on the executing entry.
    /// </summary>
    public void UpdateExecutingItem(Guid id, string title, Dictionary<string, object> details)
    {
        lock (_gate)
        {
            if (_executingSet.TryGetValue(id, out var entry))
                _executingSet[id] = entry with { Title = title, Details = details };
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _persistenceBuffer.DisposeAsync();
    }

    // ── Private helpers ────────────────────────────────────────────────────────

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
