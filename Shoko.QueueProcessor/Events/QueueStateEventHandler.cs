using System;
using System.Collections.Generic;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Orchestration;

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

    /// <summary>
    /// Fired when a running job reports new progress, at most once per job every 100 milliseconds.
    /// </summary>
    public event EventHandler<QueueJobProgressEventArgs>? JobProgressChanged;

    /// <summary>
    /// Fired when a user asks for a running job to be cancelled.
    /// </summary>
    public event EventHandler<QueueJobCancellationEventArgs>? JobCancellationRequested;

    /// <summary>
    /// Fired when waiting jobs are removed from the queue.
    /// </summary>
    public event EventHandler<QueueItemsRemovedEventArgs>? QueueItemsRemoved;

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
        IReadOnlyList<QueueItem> addedItems,
        IReadOnlyList<QueueItem> waitingItems,
        int waitingCount,
        int blockedCount,
        int executingCount,
        int threadCount,
        QueueMetricsSnapshot? metrics = null)
    {
        QueueItemsAdded?.Invoke(null, new QueueItemsAddedEventArgs
        {
            AddedItems = addedItems,
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
        int waitingCount,
        int blockedCount,
        int threadCount,
        QueueMetricsSnapshot? metrics = null)
    {
        ExecutingJobsChanged?.Invoke(null, new QueueChangedEventArgs
        {
            AddedItems = [QueueItem.FromExecuting(entry)],
            ExecutingItems = executingItems,
            WaitingJobsCount = waitingCount,
            BlockedJobsCount = blockedCount,
            TotalJobsCount = waitingCount + blockedCount + executingItems.Count,
            ExecutingJobsCount = executingItems.Count,
            ThreadCount = threadCount,
            Metrics = metrics
        });
    }

    public void OnJobCompleted(
        ExecutingEntry entry,
        IReadOnlyList<QueueItem> executingItems,
        int waitingCount,
        int blockedCount,
        int threadCount,
        QueueMetricsSnapshot? metrics = null)
    {
        ExecutingJobsChanged?.Invoke(null, new QueueChangedEventArgs
        {
            RemovedItems = [QueueItem.FromExecuting(entry, running: false)],
            ExecutingItems = executingItems,
            WaitingJobsCount = waitingCount,
            BlockedJobsCount = blockedCount,
            TotalJobsCount = waitingCount + blockedCount + executingItems.Count,
            ExecutingJobsCount = executingItems.Count,
            ThreadCount = threadCount,
            Metrics = metrics
        });
    }

    /// <summary>
    /// Raises <see cref="JobProgressChanged"/>.
    /// </summary>
    /// <param name="jobKey">The key of the job that reported.</param>
    /// <param name="progress">The job's progress, as a percentage from 0 to 100.</param>
    public void OnJobProgressChanged(string jobKey, decimal progress)
        => JobProgressChanged?.Invoke(null, new QueueJobProgressEventArgs { Key = jobKey, Progress = progress });

    /// <summary>
    /// Raises <see cref="JobCancellationRequested"/>.
    /// </summary>
    /// <param name="item">The running job, marked as cancellation requested.</param>
    public void OnJobCancellationRequested(QueueItem item)
        => JobCancellationRequested?.Invoke(null, new QueueJobCancellationEventArgs { Item = item });

    /// <summary>
    /// Raises <see cref="QueueItemsRemoved"/>.
    /// </summary>
    /// <param name="removedKeys">The keys of the removed jobs, or empty when the queue was cleared.</param>
    public void OnJobsRemoved(IReadOnlyList<string> removedKeys)
        => QueueItemsRemoved?.Invoke(null, new QueueItemsRemovedEventArgs { RemovedKeys = removedKeys });
}
