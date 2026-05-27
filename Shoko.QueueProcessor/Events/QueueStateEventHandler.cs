#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Orchestration;
using Shoko.QueueProcessor.Storage;

namespace Shoko.QueueProcessor.Events;

/// <summary>
/// Broadcasts queue state changes to SignalR / other subscribers.
/// Replaces the Quartz-coupled <c>QueueStateEventHandler</c> in Shoko.Server.
/// </summary>
public class QueueStateEventHandler
{
    private bool _isPaused;

    public bool Running { get; private set; }

    public event EventHandler? QueuePaused;
    public event EventHandler? QueueStarted;
    public event EventHandler<QueueItemsAddedEventArgs>? QueueItemsAdded;
    public event EventHandler<QueueChangedEventArgs>? ExecutingJobsChanged;

    public void InvokeQueuePaused()
    {
        if (_isPaused) return;
        Running = false;
        _isPaused = true;
        QueuePaused?.Invoke(null, EventArgs.Empty);
    }

    public void InvokeQueueStarted()
    {
        if (Running) return;
        _isPaused = false;
        Running = true;
        QueueStarted?.Invoke(null, EventArgs.Empty);
    }

    public void OnJobsAdded(
        IEnumerable<QueuedJob> added,
        IReadOnlyList<QueueItem> waitingItems,
        int waitingCount,
        int blockedCount,
        int executingCount,
        int threadCount,
        QueueMetricsSnapshot? metrics = null)
    {
        QueueItemsAdded?.Invoke(null, new QueueItemsAddedEventArgs
        {
            AddedItems = added.Select(ToItem).ToList(),
            WaitingItems = waitingItems,
            WaitingJobsCount = waitingCount,
            BlockedJobsCount = blockedCount,
            TotalJobsCount = waitingCount + blockedCount + executingCount,
            ExecutingJobsCount = executingCount,
            ThreadCount = threadCount,
            Metrics = metrics
        });
    }

    public void OnJobExecuting(
        ExecutingEntry entry,
        IReadOnlyList<QueueItem> executingItems,
        IReadOnlyList<QueueItem> waitingItems,
        int waitingCount,
        int blockedCount,
        int threadCount,
        QueueMetricsSnapshot? metrics = null)
    {
        ExecutingJobsChanged?.Invoke(null, new QueueChangedEventArgs
        {
            AddedItems = [new QueueItem
            {
                Key = entry.Id.ToString(),
                JobType = entry.JobType.Name,
                Running = true,
                StartTime = entry.StartedAt,
                PoolName = entry.PoolName,
                RetryCount = entry.RetryCount
            }],
            ExecutingItems = executingItems,
            WaitingItems = waitingItems,
            WaitingJobsCount = waitingCount,
            BlockedJobsCount = blockedCount,
            TotalJobsCount = waitingCount + blockedCount + executingItems.Count,
            ExecutingJobsCount = executingItems.Count,
            ThreadCount = threadCount,
            Metrics = metrics
        });
    }

    public void OnJobCompleted(
        Guid completedId,
        IReadOnlyList<QueueItem> executingItems,
        IReadOnlyList<QueueItem> waitingItems,
        int waitingCount,
        int blockedCount,
        int threadCount,
        QueueMetricsSnapshot? metrics = null)
    {
        ExecutingJobsChanged?.Invoke(null, new QueueChangedEventArgs
        {
            RemovedItems = [new QueueItem { Key = completedId.ToString() }],
            ExecutingItems = executingItems,
            WaitingItems = waitingItems,
            WaitingJobsCount = waitingCount,
            BlockedJobsCount = blockedCount,
            TotalJobsCount = waitingCount + blockedCount + executingItems.Count,
            ExecutingJobsCount = executingItems.Count,
            ThreadCount = threadCount,
            Metrics = metrics
        });
    }

    private static QueueItem ToItem(QueuedJob job) => new()
    {
        Key = job.Id.ToString(),
        JobType = Type.GetType(job.JobType)?.Name ?? job.JobType,
        RetryCount = job.RetryCount
    };
}
