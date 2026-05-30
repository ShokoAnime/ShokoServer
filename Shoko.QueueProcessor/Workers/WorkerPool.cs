using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
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

    // Interlocked counters for IdleWorkers / ActiveWorkers
    private int _idleWorkers;
    private int _activeWorkers;

    // UTC ticks of the most recent IncrementActive call; 0 = never active.
    // Stamped at job acquisition (not at job completion) so a downstream throttled push can
    // still report "the group did work in the last window" even when ActiveWorkers is back to 0.
    private long _lastActiveAtTicks;

    public string Name { get; }
    public int MaxWorkers { get; internal set; }
    public IReadOnlyList<Type> HandledTypes { get; }
    public IReadOnlyList<IAcquisitionFilter> AcquisitionFilters { get; }

    int IWorkerPool.IdleWorkers => _idleWorkers;
    int IWorkerPool.ActiveWorkers => _activeWorkers;

    public int IdleWorkers => _idleWorkers;
    public int ActiveWorkers => _activeWorkers;
    public int WaitingCount { get { lock (_subQueueLock) return _subQueue.Count; } }

    /// <summary>
    /// Set by <see cref="Orchestration.QueueOrchestrator.Initialize"/> after pool construction.
    /// Delegates the concurrency gate check to the orchestrator. Must be set before workers start.
    /// </summary>
    public Func<QueuedJob, bool>? TryRegisterExecuting { get; set; }

    public WorkerPool(
        string name,
        int maxWorkers,
        IReadOnlyList<Type> handledTypes,
        IReadOnlyList<IAcquisitionFilter> acquisitionFilters)
    {
        Name = name;
        MaxWorkers = maxWorkers;
        HandledTypes = handledTypes;
        AcquisitionFilters = acquisitionFilters;

        _wakeChannel = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(Math.Max(1, maxWorkers)) { FullMode = BoundedChannelFullMode.DropWrite });

        _typeByName = new Dictionary<string, Type>(handledTypes.Count, StringComparer.Ordinal);
        foreach (var t in handledTypes)
            _typeByName[t.FullName + ", " + t.Assembly.GetName().Name] = t;

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
            _subQueue.Add(job);
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
                _subQueue.Add(job);
        }
    }

    /// <summary>Removes <paramref name="id"/> from the sub-queue (called on forced discard).</summary>
    public bool RemoveFromQueue(Guid id)
    {
        lock (_subQueueLock)
        {
            var job = _subQueue.FirstOrDefault(j => j.Id == id);
            return job != null && _subQueue.Remove(job);
        }
    }

    /// <summary>Returns a snapshot of the current sub-queue contents (for GetWaiting API calls).</summary>
    public IReadOnlyList<QueuedJob> GetWaitingSnapshot()
    {
        lock (_subQueueLock) return [.._subQueue];
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
            var job = _subQueue.FirstOrDefault(j => j.JobKey == jobKey);
            if (job == null) return false;
            _subQueue.Remove(job);
            _subQueue.Add(new QueuedJob
            {
                Id = job.Id,
                JobType = job.JobType,
                JobKey = job.JobKey,
                JobDataJson = job.JobDataJson,
                Priority = newPriority,
                QueuedAt = DateTimeOffset.UtcNow,
                ScheduledAt = null,
                RetryCount = job.RetryCount,
            });
            return true;
        }
    }

    /// <summary>Clears all waiting jobs from the sub-queue (called on queue clear).</summary>
    public void ClearQueue()
    {
        lock (_subQueueLock) _subQueue.Clear();
    }

    /// <summary>Number of waiting jobs with <c>RetryCount &gt; 0</c>.</summary>
    public int RetryingCount
    {
        get { lock (_subQueueLock) return _subQueue.Count(j => j.RetryCount > 0); }
    }

    /// <summary>
    /// Number of waiting jobs whose type is currently excluded by an acquisition filter.
    /// Computed on demand — do not call on the hot path.
    /// </summary>
    public int BlockedCount
    {
        get
        {
            var exclusions = _filterExclusions;
            lock (_subQueueLock)
                return _subQueue.Count(j => _typeByName.TryGetValue(j.JobType, out var t) && exclusions.Contains(t));
        }
    }

    /// <summary>Returns true if <paramref name="type"/> is currently excluded by any acquisition filter on this pool.</summary>
    public bool IsTypeBlocked(Type type) => _filterExclusions.Contains(type);

    /// <summary>
    /// Scans the sub-queue for the next eligible job and attempts to register it with the orchestrator.
    /// Returns the claimed job or <c>null</c> if nothing is eligible right now.
    /// </summary>
    public QueuedJob? TryAcquire()
    {
        var exclusions = _filterExclusions;
        var now = DateTimeOffset.UtcNow;

        lock (_subQueueLock)
        {
            foreach (var job in _subQueue)
            {
                if (job.ScheduledAt.HasValue && job.ScheduledAt.Value > now) continue;

                if (_typeByName.TryGetValue(job.JobType, out var type) && exclusions.Contains(type)) continue;

                if (TryRegisterExecuting == null || !TryRegisterExecuting(job)) continue;

                _subQueue.Remove(job);
                return job;
            }
        }
        return null;
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

    /// <summary>Starts <see cref="MaxWorkers"/> worker tasks.</summary>
    public void Start(IServiceProvider serviceProvider, QueueOrchestrator orchestrator, QueueMetrics metrics, QueueStateEventHandler events)
    {
        _cts = new CancellationTokenSource();
        _workers.Clear();
        for (var i = 0; i < MaxWorkers; i++)
        {
            var w = new Worker(this, i, serviceProvider, orchestrator, metrics, events, _wakeChannel.Reader);
            _workers.Add(w);
            w.Start(_cts.Token);
        }
    }

    /// <summary>Cancels all worker tasks. In-flight jobs run to completion.</summary>
    public void Stop() => _cts?.Cancel();

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
        return new PoolStatus
        {
            Name = Name,
            MaxWorkers = MaxWorkers,
            ActiveWorkers = _activeWorkers,
            IdleWorkers = _idleWorkers,
            WaitingCount = WaitingCount,
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

    private void RebuildExclusions()
    {
        var set = new HashSet<Type>();
        foreach (var filter in AcquisitionFilters)
            foreach (var t in filter.GetTypesToExclude())
                set.Add(t);
        _filterExclusions = set;
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
