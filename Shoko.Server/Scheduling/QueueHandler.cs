using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartz;

namespace Shoko.Server.Scheduling;

public class QueueHandler
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly QueueStateEventHandler _queueStateEventHandler;
    private readonly Dictionary<string, QueueItem> _executingJobs = new();

    public QueueHandler(ISchedulerFactory schedulerFactory, QueueStateEventHandler queueStateEventHandler)
    {
        _schedulerFactory = schedulerFactory;
        _queueStateEventHandler = queueStateEventHandler;
        _queueStateEventHandler.ExecutingJobsChanged += ExecutingJobsStateEventHandlerOnExecutingJobsChanged;
    }

    ~QueueHandler()
    {
        _queueStateEventHandler.ExecutingJobsChanged -= ExecutingJobsStateEventHandlerOnExecutingJobsChanged;
    }

    private void ExecutingJobsStateEventHandlerOnExecutingJobsChanged(object sender, QueueChangedEventArgs e)
    {
        lock (_executingJobs)
        {
            foreach (var item in e.AddedItems)
            {
                _executingJobs[item.Key] = item;
            }

            foreach (var item in e.RemovedItems)
            {
                if (!_executingJobs.ContainsKey(item.Key)) continue;
                _executingJobs.Remove(item.Key);
            }
        }

        WaitingCount = e.WaitingJobsCount;
        BlockedCount = e.BlockedJobsCount;
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

    public int Count => WaitingCount + BlockedCount;

    public int WaitingCount { get; private set; }

    public int BlockedCount { get; private set; }

    private int _threadcount = -1;

    public int ThreadCount
    {
        get
        {
            if (_threadcount > -1) return _threadcount;
            var scheduler = _schedulerFactory.GetScheduler().Result;
            var metaData = scheduler.GetMetaData().Result;
            _threadcount = metaData.ThreadPoolSize;
            return _threadcount;
        }
    }

    public QueueItem[] GetExecutingJobs()
    {
        lock (_executingJobs)
        {
            return _executingJobs.Values.ToArray();
        }
    }
}
