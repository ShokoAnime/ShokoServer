using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Chain;
using Shoko.QueueProcessor.Events;
using Shoko.QueueProcessor.Orchestration;
using Shoko.QueueProcessor.Storage;

namespace Shoko.QueueProcessor.Workers;

/// <summary>
/// Manages a set of <see cref="Worker"/> tasks dedicated to a specific concurrency group.
/// Owns the per-pool <see cref="SortedSet{T}"/> sub-queue and performs independent acquisition.
/// </summary>
public sealed class WorkerPool : IWorkerPool
{
    // Sub-queue: sorted by (Priority DESC, QueuedAt ASC) via the custom comparer
    private readonly SortedSet<QueuedJob> _subQueue = new(QueuedJobComparer.Instance);
    // Lock order: taken before QueueOrchestrator._gate, which TryAcquire enters through
    // TryRegisterExecuting. Nothing may take this lock while holding _gate or another pool's lock.
    private readonly object _subQueueLock = new();

    // O(1) type resolution — avoids Type.GetType() (assembly scan) on every TryAcquire call
    private readonly Dictionary<string, Type> _typeByName;

    // Per-pool wake signal. Capacity matches MaxWorkers so multiple idle workers can be woken
    // by a single Signal() call. With capacity=1 (the previous design), only one worker would
    // ever wake per signal — when a pool had concurrency > 1 and jobs were short, workers
    // would alternate (W1 completes → signals → W1 goes back to wait → only one ever active
    // at a time) instead of running in parallel, capping effective concurrency at 1 regardless
    // of the [LimitConcurrency] attribute.
    private readonly Channel<bool> _wakeChannel;

    private readonly List<Worker> _workers = [];
    private CancellationTokenSource? _cts;

    // Cached exclusion set from acquisition filters; rebuilt on filter StateChanged
    private volatile HashSet<Type> _filterExclusions = [];

    // Taken in Start; while it cannot restore stored actors, jobs stored with one are held.
    private IJobActorAccessor? _actorAccessor;
    private volatile bool _holdActorJobs;

    // Interlocked counters for IdleWorkers / ActiveWorkers
    private int _idleWorkers;
    private int _activeWorkers;

    // Cached result of RunnableCount(MaxWorkers). -1 = dirty; recomputed on next read.
    // Invalidated on any queue or filter change; ScheduledAt expiry is tolerated as a
    // minor timing skew (workers re-signal on their periodic poll tick).
    private volatile int _cachedRunnableCount = -1;

    // Cached result of BlockedCount. -1 = dirty; recomputed on next read.
    // Same invalidation triggers as _cachedRunnableCount.
    private volatile int _cachedBlockedCount = -1;

    // Mirror of _subQueue.Count for lock-free reads (WaitingCount).
    // All mutations happen under _subQueueLock; volatile provides visibility outside it.
    private volatile int _waitingCount;

    // O(1) secondary indexes on _subQueue. Kept in sync with _subQueue under _subQueueLock.
    private readonly Dictionary<Guid, QueuedJob> _subQueueById = new();
    private readonly Dictionary<string, QueuedJob> _subQueueByKey = new(StringComparer.Ordinal);

    // UTC ticks of the most recent IncrementActive call; 0 = never active.
    // Stamped at job acquisition (not at job completion) so a downstream throttled push can
    // still report "the group did work in the last window" even when ActiveWorkers is back to 0.
    private long _lastActiveAtTicks;

    public string Name { get; }
    public int MaxWorkers { get; internal set; }
    public int WorkerPriority { get; }
    public IReadOnlyList<Type> HandledTypes { get; }
    public IReadOnlyList<IAcquisitionFilter> AcquisitionFilters { get; }

    /// <summary>
    /// Set by the orchestrator after pool construction. When non-null, workers call this before
    /// <see cref="TryAcquire"/> and skip acquisition (staying idle) if it returns <see langword="false"/>.
    /// Used to enforce pool priority: lower-priority pools yield when higher-priority pools have runnable jobs.
    /// </summary>
    public Func<bool>? ShouldAttemptAcquisition { get; set; }

    int IWorkerPool.IdleWorkers => _idleWorkers;
    int IWorkerPool.ActiveWorkers => _activeWorkers;

    public int IdleWorkers => _idleWorkers;
    public int ActiveWorkers => _activeWorkers;
    public int WaitingCount => _waitingCount;

    /// <summary>
    /// Set by <see cref="Orchestration.QueueOrchestrator.Initialize"/> after pool construction.
    /// Delegates the concurrency gate check to the orchestrator. Must be set before workers start.
    /// </summary>
    public Func<QueuedJob, bool>? TryRegisterExecuting { get; set; }

    /// <summary>
    /// Set by <see cref="Orchestration.QueueOrchestrator.Initialize"/>. Asked about a job that
    /// <see cref="TryRegisterExecuting"/> refused: <see langword="true"/> when the job has left
    /// the queue for good, such as one removed while its enqueue was still adding it here, so the
    /// pool drops it instead of keeping it forever.
    /// </summary>
    public Func<QueuedJob, bool>? IsDetached { get; set; }

    public WorkerPool(
        string name,
        int maxWorkers,
        int workerPriority,
        IReadOnlyList<Type> handledTypes,
        IReadOnlyList<IAcquisitionFilter> acquisitionFilters)
    {
        Name = name;
        MaxWorkers = maxWorkers;
        WorkerPriority = workerPriority;
        HandledTypes = handledTypes;
        AcquisitionFilters = acquisitionFilters;

        _wakeChannel = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(Math.Max(1, maxWorkers)) { FullMode = BoundedChannelFullMode.DropWrite });

        _typeByName = new Dictionary<string, Type>(handledTypes.Count, StringComparer.Ordinal);
        foreach (var t in handledTypes)
            _typeByName[JobTypeNames.Stored(t)] = t;

        foreach (var filter in acquisitionFilters)
            filter.StateChanged += OnFilterStateChanged;

        RebuildExclusions();
    }

    /// <summary>
    /// Adds <paramref name="job"/> to the sorted sub-queue. Called by the orchestrator on enqueue
    /// and on retry re-insertion.
    /// </summary>
    public void AddToQueue(QueuedJob job)
    {
        lock (_subQueueLock)
        {
            _subQueue.Add(job);
            _subQueueById[job.Id] = job;
            _subQueueByKey[job.JobKey] = job;
            _waitingCount++;
        }
        _cachedRunnableCount = -1;
        _cachedBlockedCount = -1;
    }

    /// <summary>
    /// Adds multiple jobs to the sub-queue under a single lock acquisition.
    /// Used by <see cref="Orchestration.QueueOrchestrator.EnqueueRangeAsync"/> for bulk enqueue.
    /// </summary>
    public void AddRangeToQueue(List<QueuedJob> jobs)
    {
        lock (_subQueueLock)
        {
            foreach (var job in jobs)
            {
                _subQueue.Add(job);
                _subQueueById[job.Id] = job;
                _subQueueByKey[job.JobKey] = job;
            }
            _waitingCount += jobs.Count;
        }
        _cachedRunnableCount = -1;
        _cachedBlockedCount = -1;
    }

    /// <summary>Removes <paramref name="id"/> from the sub-queue (called on forced discard).</summary>
    public bool RemoveFromQueue(Guid id) => RemoveFromQueue(id, out _);

    /// <summary>
    /// Removes <paramref name="id"/> from the sub-queue.
    /// </summary>
    /// <param name="id">The ID of the job to remove.</param>
    /// <param name="job">The removed job, or <see langword="null"/> if it was not waiting in this pool.</param>
    /// <returns><see langword="true"/> when the job was removed.</returns>
    public bool RemoveFromQueue(Guid id, out QueuedJob? job)
    {
        bool removed;
        lock (_subQueueLock)
        {
            if (_subQueueById.Remove(id, out job))
            {
                _subQueue.Remove(job);
                _subQueueByKey.Remove(job.JobKey);
                _waitingCount--;
                removed = true;
            }
            else
            {
                removed = false;
            }
        }
        if (removed)
        {
            _cachedRunnableCount = -1;
            _cachedBlockedCount = -1;
        }
        return removed;
    }

    /// <summary>
    /// Removes every job in <paramref name="ids"/> from the sub-queue under a single lock
    /// acquisition. Used when the queue is cleared.
    /// </summary>
    /// <param name="ids">The IDs to remove; those not waiting in this pool are skipped.</param>
    /// <returns>The number of jobs removed.</returns>
    public int RemoveFromQueue(IReadOnlyCollection<Guid> ids)
    {
        var removed = 0;
        lock (_subQueueLock)
        {
            foreach (var id in ids)
            {
                if (!_subQueueById.Remove(id, out var job))
                    continue;

                _subQueue.Remove(job);
                _subQueueByKey.Remove(job.JobKey);
                removed++;
            }
            _waitingCount -= removed;
        }
        if (removed > 0)
        {
            _cachedRunnableCount = -1;
            _cachedBlockedCount = -1;
        }
        return removed;
    }

    /// <summary>Returns a snapshot of the current sub-queue contents (for GetWaiting API calls).</summary>
    public IReadOnlyList<QueuedJob> GetWaitingSnapshot()
    {
        lock (_subQueueLock) return [.. _subQueue];
    }

    /// <summary>
    /// Resolves a waiting job's id to its job key, or returns <see langword="false"/> if no job
    /// with that id is currently waiting in this pool's sub-queue.
    /// </summary>
    public bool TryGetJobKey(Guid id, out string? jobKey)
    {
        lock (_subQueueLock)
        {
            if (_subQueueById.TryGetValue(id, out var job))
            {
                jobKey = job.JobKey;
                return true;
            }
        }
        jobKey = null;
        return false;
    }

    /// <summary>
    /// Promotes a waiting job to <paramref name="newPriority"/>, resets its queue time so it
    /// sorts before other jobs at the same priority, and clears any scheduled delay so it is
    /// eligible for immediate acquisition. Returns <see langword="false"/> if the job is not
    /// found in this pool's sub-queue (it may already be executing or belong to another pool).
    /// </summary>
    public bool TryPromotePriority(string jobKey, int newPriority)
    {
        lock (_subQueueLock)
        {
            if (!_subQueueByKey.TryGetValue(jobKey, out var job)) return false;
            _subQueue.Remove(job);
            var promoted = new QueuedJob
            {
                Id = job.Id,
                JobType = job.JobType,
                JobKey = job.JobKey,
                JobDataJson = job.JobDataJson,
                Priority = newPriority,
                QueuedAt = DateTimeOffset.UtcNow,
                ScheduledAt = null,
                RetryCount = job.RetryCount,
                ChainId = job.ChainId,
                IsChainFinally = job.IsChainFinally,
                ParentJobId = job.ParentJobId,
                Actor = job.Actor,
            };
            _subQueue.Add(promoted);
            _subQueueById[promoted.Id] = promoted;
            _subQueueByKey[promoted.JobKey] = promoted;
            return true;
        }
    }

    /// <summary>
    /// Finds a waiting job by <paramref name="id"/>, calls <paramref name="updater"/> with its
    /// current <see cref="QueuedJob.JobDataJson"/>, and if the updater returns a non-null string,
    /// replaces the job in the sub-queue with an updated copy (sort position is preserved since
    /// Priority/QueuedAt/Id are unchanged). Returns <see langword="true"/> if the job was found
    /// in this pool (regardless of whether the updater returned non-null).
    /// </summary>
    public bool TryGetAndUpdateData(Guid id, Func<string?, string?> updater)
    {
        lock (_subQueueLock)
        {
            if (!_subQueueById.TryGetValue(id, out var existing)) return false;

            var newJson = updater(existing.JobDataJson);
            if (newJson != null)
            {
                _subQueue.Remove(existing);
                var updated = new QueuedJob
                {
                    Id = existing.Id,
                    JobType = existing.JobType,
                    JobKey = existing.JobKey,
                    JobDataJson = newJson,
                    Priority = existing.Priority,
                    QueuedAt = existing.QueuedAt,
                    ScheduledAt = existing.ScheduledAt,
                    RetryCount = existing.RetryCount,
                    ChainId = existing.ChainId,
                    IsChainFinally = existing.IsChainFinally,
                    ParentJobId = existing.ParentJobId,
                    Actor = existing.Actor,
                };
                _subQueue.Add(updated);
                _subQueueById[id] = updated;
                _subQueueByKey[existing.JobKey] = updated;
            }
            return true;
        }
    }

    /// <summary>
    /// Clears all waiting jobs from the sub-queue.
    /// </summary>
    public void ClearQueue()
    {
        lock (_subQueueLock)
        {
            _subQueue.Clear();
            _subQueueById.Clear();
            _subQueueByKey.Clear();
            _waitingCount = 0;
        }
        _cachedRunnableCount = -1;
        _cachedBlockedCount = -1;
    }

    /// <summary>Number of waiting jobs with <c>RetryCount &gt; 0</c>.</summary>
    public int RetryingCount
    {
        get { lock (_subQueueLock) return _subQueue.Count(j => j.RetryCount > 0); }
    }

    /// <summary>
    /// Number of waiting jobs held back (their type excluded by an acquisition filter, or their
    /// actor not restorable yet) that are <em>not</em> deferred to a future
    /// <see cref="QueuedJob.ScheduledAt"/> — a job waiting on its scheduled time is counted as
    /// <see cref="ScheduledCount"/>, not blocked, so the two categories are disjoint. Cached and
    /// invalidated on any queue or filter mutation.
    /// </summary>
    public int BlockedCount
    {
        get
        {
            var cached = _cachedBlockedCount;
            if (cached >= 0) return cached;
            var exclusions = _filterExclusions;
            var holdActorJobs = _holdActorJobs;
            var now = DateTimeOffset.UtcNow;
            int count;
            lock (_subQueueLock)
                count = _subQueue.Count(j =>
                    !(j.ScheduledAt.HasValue && j.ScheduledAt.Value > now)
                    && IsHeld(j, exclusions, holdActorJobs));
            _cachedBlockedCount = count;
            return count;
        }
    }

    /// <summary>
    /// Number of waiting jobs deferred to a future <see cref="QueuedJob.ScheduledAt"/> (retry
    /// backoff or an intentionally delayed re-fetch). These are not yet ready to run and are not
    /// counted as waiting. Computed on demand — only read from status/API paths, never the hot path.
    /// </summary>
    public int ScheduledCount
    {
        get
        {
            var now = DateTimeOffset.UtcNow;
            lock (_subQueueLock)
                return _subQueue.Count(j => j.ScheduledAt.HasValue && j.ScheduledAt.Value > now);
        }
    }

    /// <summary>Returns true if <paramref name="type"/> is currently excluded by any acquisition filter on this pool.</summary>
    public bool IsTypeBlocked(Type type) => _filterExclusions.Contains(type);

    /// <summary>
    /// Whether <paramref name="job"/> is held back from dispatch: its type is excluded by an
    /// acquisition filter, or it was queued with an actor the host cannot restore yet.
    /// </summary>
    /// <param name="job">A job waiting in this pool.</param>
    /// <returns><see langword="true"/> when the job may not be acquired right now.</returns>
    public bool IsJobBlocked(QueuedJob job) => IsHeld(job, _filterExclusions, _holdActorJobs);

    /// <summary>
    /// Whether <paramref name="job"/> is held back by <paramref name="exclusions"/> or by its actor.
    /// </summary>
    /// <param name="job">The job.</param>
    /// <param name="exclusions">The job types excluded by the acquisition filters.</param>
    /// <param name="holdActorJobs">Whether jobs stored with an actor are held.</param>
    /// <returns><see langword="true"/> when the job may not be acquired right now.</returns>
    private bool IsHeld(QueuedJob job, HashSet<Type> exclusions, bool holdActorJobs)
        => (holdActorJobs && job.ActorUserId.HasValue)
            || (_typeByName.TryGetValue(job.JobType, out var type) && exclusions.Contains(type));

    /// <summary>
    /// Returns the number of waiting jobs that are not blocked and not scheduled in the future,
    /// stopping early once <paramref name="limit"/> is reached. Pass <see cref="MaxWorkers"/> as the
    /// limit to cap the scan at the pool's own concurrency ceiling — that is all the orchestrator
    /// needs for slot reservation math. Results for <c>limit == MaxWorkers</c> are cached and
    /// invalidated on any queue or filter mutation.
    /// </summary>
    public int RunnableCount(int limit = int.MaxValue)
    {
        if (limit == MaxWorkers)
        {
            var cached = _cachedRunnableCount;
            if (cached >= 0) return cached;
            var computed = ComputeRunnableCount(MaxWorkers);
            _cachedRunnableCount = computed;
            return computed;
        }
        return ComputeRunnableCount(limit);
    }

    private int ComputeRunnableCount(int limit)
    {
        var exclusions = _filterExclusions;
        var holdActorJobs = _holdActorJobs;
        var now = DateTimeOffset.UtcNow;
        var count = 0;
        lock (_subQueueLock)
        {
            foreach (var job in _subQueue)
            {
                if (job.ScheduledAt.HasValue && job.ScheduledAt.Value > now) continue;
                if (IsHeld(job, exclusions, holdActorJobs)) continue;
                if (++count >= limit) break;
            }
        }
        return count;
    }

    /// <summary>
    /// Scans the sub-queue for the next eligible job and attempts to register it with the orchestrator.
    /// Returns the claimed job or <c>null</c> if nothing is eligible right now.
    /// </summary>
    public QueuedJob? TryAcquire()
    {
        var exclusions = _filterExclusions;
        var holdActorJobs = _holdActorJobs;
        var now = DateTimeOffset.UtcNow;

        lock (_subQueueLock)
        {
            QueuedJob? acquired = null;
            List<QueuedJob>? detached = null;
            foreach (var job in _subQueue)
            {
                if (job.ScheduledAt.HasValue && job.ScheduledAt.Value > now) continue;

                if (IsHeld(job, exclusions, holdActorJobs)) continue;

                if (TryRegisterExecuting == null || !TryRegisterExecuting(job))
                {
                    if (IsDetached?.Invoke(job) is true)
                        (detached ??= []).Add(job);
                    continue;
                }

                acquired = job;
                break;
            }

            if (detached != null)
                foreach (var job in detached)
                    RemoveUnderLock(job);
            if (acquired != null)
                RemoveUnderLock(acquired);
            return acquired;
        }
    }

    /// <summary>
    /// Takes a job out of the sub-queue and its indexes. MUST be called under <see cref="_subQueueLock"/>.
    /// </summary>
    /// <param name="job">The job, which is in the sub-queue.</param>
    private void RemoveUnderLock(QueuedJob job)
    {
        _subQueue.Remove(job);
        _subQueueById.Remove(job.Id);
        // A detached job can share its key with the live job that took the key over.
        if (_subQueueByKey.TryGetValue(job.JobKey, out var byKey) && ReferenceEquals(byKey, job))
            _subQueueByKey.Remove(job.JobKey);
        _waitingCount--;
        _cachedRunnableCount = -1;
        _cachedBlockedCount = -1;
    }

    /// <summary>
    /// Wakes idle workers so they can attempt acquisition. Writes once per worker slot:
    /// the channel is bounded to <see cref="MaxWorkers"/>, so excess writes are dropped when
    /// other workers are already active or wakes are already pending. A spurious wake on a
    /// worker with no work to acquire just loops back to wait — cheap.
    /// </summary>
    public void Signal()
    {
        var writer = _wakeChannel.Writer;
        for (var i = 0; i < MaxWorkers; i++)
        {
            if (!writer.TryWrite(true)) break;
        }
    }

    /// <summary>
    /// Starts <see cref="MaxWorkers"/> worker tasks. The pool holds every job stored with an
    /// actor for as long as the <see cref="IJobActorAccessor"/> cannot restore it.
    /// </summary>
    public void Start(IServiceProvider serviceProvider, QueueOrchestrator orchestrator, QueueMetrics metrics, QueueStateEventHandler events,
        IChainScopeRegistry chainScopeRegistry)
    {
        _actorAccessor = serviceProvider.GetService<IJobActorAccessor>();
        if (_actorAccessor is not null)
            _actorAccessor.CanRestoreChanged += OnActorRestoreChanged;
        OnActorRestoreChanged(null, EventArgs.Empty);

        _cts = new CancellationTokenSource();
        _workers.Clear();
        for (var i = 0; i < MaxWorkers; i++)
        {
            var w = new Worker(this, i, serviceProvider, orchestrator, chainScopeRegistry, metrics, events, _wakeChannel.Reader, _actorAccessor);
            _workers.Add(w);
            w.Start(_cts.Token);
        }
    }

    /// <summary>Cancels all worker tasks. In-flight jobs run to completion.</summary>
    public void Stop()
    {
        if (_actorAccessor is not null)
            _actorAccessor.CanRestoreChanged -= OnActorRestoreChanged;
        _actorAccessor = null;
        _cts?.Cancel();
    }

    /// <summary>
    /// Awaits exit of every worker started by <see cref="Start"/>. Combined with <see cref="Stop"/>,
    /// lets shutdown wait for in-flight <c>Process()</c> calls so their <c>OnComplete</c>-buffered
    /// deletes are recorded before the <see cref="Orchestration.PersistenceBuffer"/> is flushed.
    /// </summary>
    public Task WhenStoppedAsync() =>
        _workers.Count == 0 ? Task.CompletedTask : Task.WhenAll(_workers.Select(w => w.Completion));

    /// <summary>Returns a status snapshot for the API.</summary>
    public PoolStatus GetStatus()
    {
        var exclusions = _filterExclusions;
        var lastActiveTicks = Interlocked.Read(ref _lastActiveAtTicks);
        // Disjoint buckets: WaitingCount reports only jobs ready to run now.
        var blocked = BlockedCount;
        var scheduled = ScheduledCount;
        return new PoolStatus
        {
            Name = Name,
            MaxWorkers = MaxWorkers,
            ActiveWorkers = _activeWorkers,
            IdleWorkers = _idleWorkers,
            WaitingCount = Math.Max(0, WaitingCount - blocked - scheduled),
            BlockedCount = blocked,
            ScheduledCount = scheduled,
            IsBlocked = HandledTypes.Count > 0 && HandledTypes.All(t => exclusions.Contains(t)),
            HandledTypeNames = HandledTypes.Select(t => t.Name).ToList(),
            LastActiveAt = lastActiveTicks == 0 ? null : new DateTimeOffset(lastActiveTicks, TimeSpan.Zero)
        };
    }

    internal void IncrementIdle() => Interlocked.Increment(ref _idleWorkers);
    internal void DecrementIdle() => Interlocked.Decrement(ref _idleWorkers);

    internal void IncrementActive()
    {
        Interlocked.Exchange(ref _lastActiveAtTicks, DateTimeOffset.UtcNow.UtcTicks);
        Interlocked.Increment(ref _activeWorkers);
    }

    internal void DecrementActive() => Interlocked.Decrement(ref _activeWorkers);

    internal ChannelReader<bool> WakeReader => _wakeChannel.Reader;

    /// <summary>
    /// Resolves a job type name to its <see cref="Type"/> using the pre-built cache.
    /// Avoids <c>Type.GetType()</c> (assembly scan) on the hot execution path.
    /// </summary>
    internal Type? ResolveJobType(string jobTypeName) =>
        _typeByName.TryGetValue(jobTypeName, out var t) ? t : null;

    private void OnFilterStateChanged(object? sender, EventArgs e) => RebuildExclusions();

    private void OnActorRestoreChanged(object? sender, EventArgs e)
    {
        _holdActorJobs = _actorAccessor?.CanRestore is false;
        _cachedRunnableCount = -1;
        _cachedBlockedCount = -1;
        Signal();
    }

    private void RebuildExclusions()
    {
        var set = new HashSet<Type>();
        foreach (var filter in AcquisitionFilters)
            foreach (var t in filter.GetTypesToExclude())
                set.Add(t);
        _filterExclusions = set;
        _cachedRunnableCount = -1;
        _cachedBlockedCount = -1;
        Signal(); // wake workers to retry acquisition with updated exclusions
    }

    /// <summary>Comparer for the sub-queue <see cref="SortedSet{T}"/>: Priority DESC, QueuedAt ASC, Id ASC (tie-break).</summary>
    private sealed class QueuedJobComparer : IComparer<QueuedJob>
    {
        public static readonly QueuedJobComparer Instance = new();

        public int Compare(QueuedJob? x, QueuedJob? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return 1;
            if (y == null) return -1;

            // Priority descending
            var c = y.Priority.CompareTo(x.Priority);
            if (c != 0) return c;

            // QueuedAt ascending
            c = x.QueuedAt.CompareTo(y.QueuedAt);
            if (c != 0) return c;

            // Tie-break by Id to ensure uniqueness in the SortedSet
            return x.Id.CompareTo(y.Id);
        }
    }
}
