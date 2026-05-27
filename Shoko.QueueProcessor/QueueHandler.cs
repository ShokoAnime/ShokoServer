#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Events;
using Shoko.QueueProcessor.Orchestration;
using Shoko.QueueProcessor.Workers;

namespace Shoko.QueueProcessor;

/// <summary>
/// High-level façade used by API controllers and SignalR emitters.
/// Replaces <c>Shoko.Server.Scheduling.QueueHandler</c>.
/// </summary>
public class QueueHandler
{
    private readonly IQueueScheduler _scheduler;
    private readonly QueueOrchestrator _orchestrator;
    private readonly WorkerPoolManager _poolManager;
    private readonly QueueStateEventHandler _events;

    // Cached state updated by event subscriptions — avoids recalculating on every API call
    private readonly Dictionary<string, QueueItem> _executingJobs = new();
    private readonly List<QueueItem> _waitingJobs = new();

    public QueueHandler(
        IQueueScheduler scheduler,
        QueueOrchestrator orchestrator,
        WorkerPoolManager poolManager,
        QueueStateEventHandler events)
    {
        _scheduler = scheduler;
        _orchestrator = orchestrator;
        _poolManager = poolManager;
        _events = events;

        _events.ExecutingJobsChanged += OnExecutingJobsChanged;
        _events.QueueItemsAdded += OnQueueItemsAdded;
    }

    ~QueueHandler()
    {
        _events.ExecutingJobsChanged -= OnExecutingJobsChanged;
        _events.QueueItemsAdded -= OnQueueItemsAdded;
    }

    // ── Event handlers ─────────────────────────────────────────────────────

    private void OnExecutingJobsChanged(object? sender, QueueChangedEventArgs e)
    {
        lock (_executingJobs)
        {
            _executingJobs.Clear();
            foreach (var item in e.ExecutingItems)
                _executingJobs[item.Key] = item;
        }

        WaitingCount = e.WaitingJobsCount;
        BlockedCount = e.BlockedJobsCount;
        TotalCount = e.TotalJobsCount;

        lock (_waitingJobs)
        {
            _waitingJobs.Clear();
            _waitingJobs.AddRange(e.WaitingItems);
        }
    }

    private void OnQueueItemsAdded(object? sender, QueueItemsAddedEventArgs e)
    {
        WaitingCount = e.WaitingJobsCount;
        BlockedCount = e.BlockedJobsCount;
        TotalCount = e.TotalJobsCount;

        lock (_waitingJobs)
        {
            _waitingJobs.Clear();
            _waitingJobs.AddRange(e.WaitingItems);
        }
    }

    // ── Queue control ──────────────────────────────────────────────────────

    public Task Pause()
    {
        _poolManager.Pause();
        return Task.CompletedTask;
    }

    public Task Resume()
    {
        _poolManager.Resume();
        return Task.CompletedTask;
    }

    public Task Clear() => _scheduler.Clear();

    public bool Paused => _scheduler.IsPaused;

    // ── State counters ─────────────────────────────────────────────────────

    public int WaitingCount { get; private set; }
    public int BlockedCount { get; private set; }
    public int TotalCount { get; private set; }

    /// <summary>Total worker slots across all pools (equivalent to old ThreadPoolSize).</summary>
    public int ThreadCount => _poolManager.Pools.Sum(p => p.MaxWorkers);

    // ── Job queries ────────────────────────────────────────────────────────

    public QueueItem[] GetExecutingJobs()
    {
        lock (_executingJobs) return _executingJobs.Values.ToArray();
    }

    public QueueItem[] GetWaitingJobs()
    {
        lock (_waitingJobs) return _waitingJobs.ToArray();
    }

    public int GetTotalWaitingJobCount() => _orchestrator.WaitingCount;

    public IReadOnlyList<QueueItem> GetJobs(int maxCount, int offset, bool excludeBlocked)
    {
        // Return executing first, then waiting
        var executing = GetExecutingJobs();
        var all = new List<QueueItem>(executing);

        var waitingItems = _orchestrator.GetWaiting(maxCount + executing.Length, 0);
        all.AddRange(waitingItems
            .Where(j => !excludeBlocked || j.ScheduledAt == null || j.ScheduledAt <= DateTimeOffset.UtcNow)
            .Select(j => new QueueItem
            {
                Key = j.Id.ToString(),
                JobType = System.Type.GetType(j.JobType)?.Name ?? j.JobType,
                RetryCount = j.RetryCount,
                Blocked = j.ScheduledAt.HasValue && j.ScheduledAt.Value > DateTimeOffset.UtcNow
            }));

        return all.Skip(offset).Take(maxCount).ToList();
    }

    // ── Analytics ─────────────────────────────────────────────────────────

    public QueueMetricsSnapshot GetMetrics() => _orchestrator.GetMetrics();

    public IReadOnlyDictionary<string, PoolStatus> GetPoolStatus() => _orchestrator.GetPoolStatus();

    public Dictionary<string, string[]> GetAcquisitionFilterResults()
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var pool in _poolManager.Pools)
        {
            foreach (var filter in pool.AcquisitionFilters)
            {
                var filterName = filter.GetType().Name;
                var excluded = filter.GetTypesToExclude().Select(t => t.Name).ToArray();
                if (excluded.Length == 0) continue;
                result[filterName] = excluded;
            }
        }
        return result;
    }
}
