using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartz;
using Quartz.Impl;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Jobs;

namespace Shoko.Server.Scheduling;

public class QueueHandler
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly QueueStateEventHandler _queueStateEventHandler;
    private readonly JobFactory _jobFactory;
    private readonly ThreadPooledJobStore _jobStore;
    private readonly Dictionary<string, QueueItem> _executingJobs = new();
    private readonly List<QueueItem> _waitingJobs = new();

    public QueueHandler(ISchedulerFactory schedulerFactory, QueueStateEventHandler queueStateEventHandler, JobFactory jobFactory, ThreadPooledJobStore jobStore)
    {
        _schedulerFactory = schedulerFactory;
        _queueStateEventHandler = queueStateEventHandler;
        _jobFactory = jobFactory;
        _jobStore = jobStore;
        _queueStateEventHandler.ExecutingJobsChanged += ExecutingJobsStateEventHandlerOnExecutingJobsChanged;
        _queueStateEventHandler.QueueItemAdded += QueueStateEventHandlerOnQueueItemAdded;
    }

    ~QueueHandler()
    {
        _queueStateEventHandler.ExecutingJobsChanged -= ExecutingJobsStateEventHandlerOnExecutingJobsChanged;
    }

    private void ExecutingJobsStateEventHandlerOnExecutingJobsChanged(object sender, QueueChangedEventArgs e)
    {
        lock (_executingJobs)
        {
            _executingJobs.Clear();
            foreach (var queueItem in e.ExecutingItems)
            {
                _executingJobs[queueItem.Key] = queueItem;
            }
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

    private void QueueStateEventHandlerOnQueueItemAdded(object sender, QueueItemAddedEventArgs e)
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

    public async Task Pause()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        if (scheduler.IsShutdown || !scheduler.IsStarted || scheduler.InStandbyMode) return;
        await scheduler.Standby();
    }

    public async Task Resume()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        if (scheduler.IsShutdown || !scheduler.IsStarted || !scheduler.InStandbyMode) return;
        await scheduler.Start();
    }

    public async Task Clear()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        if (scheduler.IsShutdown || !scheduler.IsStarted) return;
        await scheduler.Clear();
        await QuartzStartup.ScheduleRecurringJobs(false);
    }

    public bool Paused
    {
        get
        {
            var scheduler = _schedulerFactory.GetScheduler().Result;
            return scheduler.IsStarted && scheduler.InStandbyMode;
        }
    }

    public int WaitingCount { get; private set; }

    public int BlockedCount { get; private set; }

    public int TotalCount { get; private set; }

    private int _threadCount = -1;

    public int ThreadCount
    {
        get
        {
            if (_threadCount > -1) return _threadCount;
            var scheduler = _schedulerFactory.GetScheduler().Result;
            var metaData = scheduler.GetMetaData().Result;
            _threadCount = metaData.ThreadPoolSize;
            return _threadCount;
        }
    }

    public QueueItem[] GetExecutingJobs()
    {
        lock (_executingJobs) return _executingJobs.Values.ToArray();
    }

    public QueueItem[] GetWaitingJobs()
    {
        lock (_waitingJobs) return _waitingJobs.ToArray();
    }

    public ValueTask<int> GetTotalWaitingJobCount()
    {
        return _jobStore.GetTotalWaitingTriggersCount();
    }

    public async ValueTask<Dictionary<string, int>> GetJobCounts()
    {
        var jobs = await _jobStore.GetJobCounts();
        return jobs.Where(a => typeof(BaseJob).IsAssignableFrom(a.Key)).Select(a => (_jobFactory.CreateJob(new JobDetail(a.Key) { Name = Guid.NewGuid().ToString() })?.TypeName, a.Value))
            .Where(a => a.TypeName != null)
            .ToDictionary(a => a.TypeName, a => a.Value);
    }

    public ValueTask<List<QueueItem>> GetJobs(int maxCount, int offset, bool excludeBlocked)
    {
        return _jobStore.GetJobSummary(maxCount, offset, excludeBlocked);
    }

    public Dictionary<string, string[]> GetAcquisitionFilterResults() => _jobStore.GetAcquisitionFilterResults();
    public JobTypes GetTypes() => _jobStore.GetTypes();
}
