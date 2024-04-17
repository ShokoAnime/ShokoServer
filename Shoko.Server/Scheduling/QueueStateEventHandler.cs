using System;
using System.Collections.Generic;
using System.Linq;
using Quartz;

namespace Shoko.Server.Scheduling;

public class QueueStateEventHandler
{
    private readonly JobFactory _jobFactory;
    private bool _isPaused;
    public bool Running { get; private set; }

    public event EventHandler QueuePaused;
    public event EventHandler QueueStarted;
    public event EventHandler<QueueItemAddedEventArgs> QueueItemAdded;
    public event EventHandler<QueueChangedEventArgs> ExecutingJobsChanged;

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

    public void OnJobAdded(IJobDetail jobDetail, QueueStateContext queueContext)
    {
        var job = _jobFactory.CreateJob(jobDetail);

        QueueItemAdded?.Invoke(null, new QueueItemAddedEventArgs
        {
            AddedItems = new List<QueueItem>
            {
                new()
                {
                    Key = jobDetail.Key.ToString(), JobType = job?.TypeName ?? jobDetail.JobType.Type.Name, Title = job?.Title, Details = job?.Details
                }
            },
            WaitingJobsCount = queueContext.WaitingTriggersCount,
            BlockedJobsCount = queueContext.BlockedTriggersCount,
            TotalJobsCount = queueContext.TotalTriggersCount,
            ExecutingJobsCount = queueContext.CurrentlyExecuting.Length,
            WaitingItems = queueContext.Waiting.ToList(),
            ThreadCount = queueContext.ThreadCount
        });
    }

    public void OnJobExecuting(IJobDetail jobDetail, QueueStateContext queueContext)
    {
        var job = _jobFactory.CreateJob(jobDetail);

        ExecutingJobsChanged?.Invoke(null, new QueueChangedEventArgs
        {
            AddedItems = new List<QueueItem>
            {
                new()
                {
                    Key = jobDetail.Key.ToString(),
                    JobType = job?.TypeName ?? jobDetail.JobType.Type.Name,
                    Title = job?.Title,
                    Details = job?.Details,
                    Running = true
                }
            },
            WaitingJobsCount = queueContext.WaitingTriggersCount,
            BlockedJobsCount = queueContext.BlockedTriggersCount,
            TotalJobsCount = queueContext.TotalTriggersCount,
            ThreadCount = queueContext.ThreadCount,
            ExecutingItems = queueContext.CurrentlyExecuting.ToList(),
            WaitingItems = queueContext.Waiting.ToList()
        });
    }

    public void OnJobCompleted(IJobDetail jobDetail, QueueStateContext queueContext)
    {
        var job = _jobFactory.CreateJob(jobDetail);

        ExecutingJobsChanged?.Invoke(null, new QueueChangedEventArgs
        {
            RemovedItems = new List<QueueItem>
            {
                new()
                {
                    Key = jobDetail.Key.ToString(), JobType = job?.TypeName ?? jobDetail.JobType.Type.Name, Title = job?.Title, Details = job?.Details
                }
            },
            WaitingJobsCount = queueContext.WaitingTriggersCount,
            BlockedJobsCount = queueContext.BlockedTriggersCount,
            TotalJobsCount = queueContext.TotalTriggersCount,
            ThreadCount = queueContext.ThreadCount,
            ExecutingItems = queueContext.CurrentlyExecuting.ToList(),
            WaitingItems = queueContext.Waiting.ToList()
        });
    }

    public QueueStateEventHandler(JobFactory jobFactory)
    {
        _jobFactory = jobFactory;
    }
}
