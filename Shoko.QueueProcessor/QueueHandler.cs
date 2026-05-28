#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Builder;
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

    // Executing jobs cache — updated by ExecutingJobsChanged events
    private readonly Dictionary<string, QueueItem> _executingJobs = new();

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
    }

    private void OnQueueItemsAdded(object? sender, QueueItemsAddedEventArgs e)
    {
        WaitingCount = e.WaitingJobsCount;
        BlockedCount = e.BlockedJobsCount;
        TotalCount = e.TotalJobsCount;
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
    public int ThreadCount => _orchestrator.TotalWorkerCount;

    // ── Job queries ────────────────────────────────────────────────────────

    public QueueItem[] GetExecutingJobs()
    {
        lock (_executingJobs) return [.._executingJobs.Values];
    }

    /// <summary>
    /// Returns a page of jobs (executing first, then waiting), with correct pagination and
    /// populated <see cref="QueueItem.Title"/> / <see cref="QueueItem.Details"/> for all items.
    /// </summary>
    public IReadOnlyList<QueueItem> GetJobs(int maxCount, int offset, bool excludeBlocked)
    {
        var executing = GetExecutingJobs();
        var result = new List<QueueItem>(maxCount);

        // Fill from executing first
        if (offset < executing.Length)
        {
            foreach (var e in executing.Skip(offset).Take(maxCount))
                result.Add(e);
        }

        if (result.Count >= maxCount)
            return result;

        // Remaining slots filled from waiting
        var waitingOffset = Math.Max(0, offset - executing.Length);
        var waitingNeeded = maxCount - result.Count;
        var waitingItems = _orchestrator.GetWaiting(waitingNeeded + waitingOffset, 0);
        var now = DateTimeOffset.UtcNow;

        var skipped = 0;
        foreach (var j in waitingItems)
        {
            var isRetryDelayed = j.ScheduledAt.HasValue && j.ScheduledAt.Value > now;
            var isFilterBlocked = _orchestrator.IsJobBlocked(j);
            var isBlocked = isRetryDelayed || isFilterBlocked;
            if (excludeBlocked && isBlocked) continue;

            if (skipped < waitingOffset) { skipped++; continue; }
            if (result.Count >= maxCount) break;

            result.Add(BuildWaitingItem(j, isBlocked));
        }

        return result;
    }

    // ── Analytics ─────────────────────────────────────────────────────────

    public QueueMetricsSnapshot GetMetrics() => _orchestrator.GetMetrics();

    public IReadOnlyDictionary<string, PoolStatus> GetPoolStatus() => _orchestrator.GetPoolStatus();

    public Dictionary<string, string[]> GetAcquisitionFilterResults()
    {
        // Deduplicate by instance — the same filter singleton appears on every pool whose
        // types carry the watched attribute; we only want one entry per filter.
        var seen = new HashSet<IAcquisitionFilter>(ReferenceEqualityComparer.Instance);
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var pool in _poolManager.Pools)
        {
            foreach (var filter in pool.AcquisitionFilters)
            {
                if (!seen.Add(filter)) continue;
                var excluded = filter.GetTypesToExclude().Select(t => t.Name).ToArray();
                if (excluded.Length == 0) continue;
                result[filter.GetType().Name] = excluded;
            }
        }
        return result;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private QueueItem BuildWaitingItem(Storage.QueuedJob j, bool isRetryDelayed)
    {
        var type = _orchestrator.TryResolveType(j.JobType);
        var typeName = type?.Name ?? GetShortTypeName(j.JobType);
        var title = string.Empty;
        Dictionary<string, object> details = [];

        if (type != null)
        {
            try
            {
                // Bypass constructor — job data properties are set separately, injected services not needed
                var inst = (IQueueJob)RuntimeHelpers.GetUninitializedObject(type);
                JobDataSerializer.Apply(inst, j.JobDataJson);
                title = inst.Title;
                details = inst.Details;
            }
            catch { /* best-effort; leave title/details empty */ }
        }

        return new QueueItem
        {
            Key = j.JobKey,
            JobType = typeName,
            Title = title,
            Details = details,
            RetryCount = j.RetryCount,
            Blocked = isRetryDelayed
        };
    }

    /// <summary>
    /// Extracts the short class name from an assembly-qualified type name without reflection.
    /// e.g. "Shoko.Server.Scheduling.Jobs.AniDB.GetAniDBAnimeJob, Shoko.Server" → "GetAniDBAnimeJob"
    /// </summary>
    private static string GetShortTypeName(string assemblyQualifiedName)
    {
        var nameOnly = assemblyQualifiedName.Split(',')[0].Trim();
        var dot = nameOnly.LastIndexOf('.');
        return dot >= 0 ? nameOnly[(dot + 1)..] : nameOnly;
    }
}
