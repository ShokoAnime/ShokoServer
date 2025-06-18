using System;
using System.Collections.Generic;
using System.Linq;
using Quartz;
using Quartz.Spi;

#nullable enable
namespace Shoko.Server.Scheduling;

public class QueueStateEventHandler(JobFactory jobFactory)
{
    private readonly JobFactory _jobFactory = jobFactory;

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

    public void OnJobsAdded(IEnumerable<IJobDetail> jobDetail, QueueStateContext queueContext)
    {
        var jobs = jobDetail.Select(jobDetail => new { jobDetail, job = _jobFactory.CreateJob(jobDetail) }).ToList();
        QueueItemsAdded?.Invoke(null, new()
        {
            AddedItems = jobs
                .Select(obj => new QueueItem()
                {
                    Key = obj.jobDetail.Key.ToString(),
                    JobType = obj.job?.TypeName ?? obj.jobDetail.JobType.Type.Name,
                    Title = obj.job?.Title,
                    Details = obj.job?.Details
                })
                .ToList(),
            WaitingJobsCount = queueContext.WaitingTriggersCount,
            BlockedJobsCount = queueContext.BlockedTriggersCount,
            TotalJobsCount = queueContext.TotalTriggersCount,
            ExecutingJobsCount = queueContext.CurrentlyExecuting.Length,
            WaitingItems = queueContext.Waiting.ToList(),
            ThreadCount = queueContext.ThreadCount
        });
    }

    public void OnJobExecuting(IEnumerable<(IOperableTrigger trigger, IJobDetail jobDetail)> triggerDetails, QueueStateContext queueContext)
    {
        var addedItems = triggerDetails.Select(detail =>
        {
            var job = _jobFactory.CreateJob(detail.jobDetail);
            return new QueueItem()
            {
                Key = detail.jobDetail.Key.ToString(),
                JobType = job?.TypeName ?? detail.jobDetail.JobType.Type.Name,
                Title = job?.Title,
                Details = job?.Details,
                Running = true,
                StartTime = detail.trigger.StartTimeUtc.LocalDateTime
            };
        }).ToList();

        ExecutingJobsChanged?.Invoke(null, new QueueChangedEventArgs
        {
            AddedItems = addedItems,
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
            RemovedItems =
            [
                new()
                {
                    Key = jobDetail.Key.ToString(), JobType = job?.TypeName ?? jobDetail.JobType.Type.Name, Title = job?.Title, Details = job?.Details
                }
            ],
            WaitingJobsCount = queueContext.WaitingTriggersCount,
            BlockedJobsCount = queueContext.BlockedTriggersCount,
            TotalJobsCount = queueContext.TotalTriggersCount,
            ThreadCount = queueContext.ThreadCount,
            ExecutingItems = queueContext.CurrentlyExecuting.ToList(),
            WaitingItems = queueContext.Waiting.ToList()
        });
    }
}
