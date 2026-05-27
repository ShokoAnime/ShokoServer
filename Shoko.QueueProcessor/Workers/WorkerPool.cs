#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
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

    // Per-pool wake signal: capacity=1, drop on full (idempotent wake)
    private readonly Channel<bool> _wakeChannel =
        Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    private readonly List<Worker> _workers = [];
    private CancellationTokenSource? _cts;

    // Cached exclusion set from acquisition filters; rebuilt on filter StateChanged
    private volatile HashSet<Type> _filterExclusions = [];

    // Interlocked counters for IdleWorkers / ActiveWorkers
    private int _idleWorkers;
    private int _activeWorkers;

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

                var type = Type.GetType(job.JobType);
                if (type != null && exclusions.Contains(type)) continue;

                if (TryRegisterExecuting == null || !TryRegisterExecuting(job)) continue;

                _subQueue.Remove(job);
                return job;
            }
        }
        return null;
    }

    /// <summary>Signals a waiting worker to wake up and attempt acquisition.</summary>
    public void Signal() => _wakeChannel.Writer.TryWrite(true);

    /// <summary>Starts <see cref="MaxWorkers"/> worker tasks.</summary>
    public void Start(IServiceProvider serviceProvider, Orchestration.QueueOrchestrator orchestrator, Analytics.QueueMetrics metrics, Events.QueueStateEventHandler events)
    {
        _cts = new CancellationTokenSource();
        _workers.Clear();
        for (var i = 0; i < MaxWorkers; i++)
        {
            var w = new Worker(this, serviceProvider, orchestrator, metrics, events, _wakeChannel.Reader);
            _workers.Add(w);
            w.Start(_cts.Token);
        }
    }

    /// <summary>Cancels all worker tasks. In-flight jobs run to completion.</summary>
    public void Stop() => _cts?.Cancel();

    /// <summary>Returns a status snapshot for the API.</summary>
    public PoolStatus GetStatus()
    {
        var exclusions = _filterExclusions;
        return new PoolStatus
        {
            Name = Name,
            MaxWorkers = MaxWorkers,
            ActiveWorkers = _activeWorkers,
            IdleWorkers = _idleWorkers,
            WaitingCount = WaitingCount,
            IsBlocked = HandledTypes.Count > 0 && HandledTypes.All(t => exclusions.Contains(t)),
            HandledTypeNames = HandledTypes.Select(t => t.Name).ToList()
        };
    }

    internal void IncrementIdle() => Interlocked.Increment(ref _idleWorkers);
    internal void DecrementIdle() => Interlocked.Decrement(ref _idleWorkers);
    internal void IncrementActive() => Interlocked.Increment(ref _activeWorkers);
    internal void DecrementActive() => Interlocked.Decrement(ref _activeWorkers);

    internal ChannelReader<bool> WakeReader => _wakeChannel.Reader;

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
