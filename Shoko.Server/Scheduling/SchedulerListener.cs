using System.Threading;
using System.Threading.Tasks;
using Quartz;

namespace Shoko.Server.Scheduling;

/// <summary>
/// Most of these are not used. Job/Trigger operations are done from <see cref="ThreadPooledJobStore"/>.
/// It is easier to update the job counts that way.
/// </summary>
public class SchedulerListener : ISchedulerListener
{
    private readonly QueueStateEventHandler _eventHandler;

    public SchedulerListener(QueueStateEventHandler eventHandler)
    {
        _eventHandler = eventHandler;
    }

    /// <inheritdoc cref="ISchedulerListener.SchedulerError"/>
    public ValueTask SchedulerError(string msg, SchedulerException cause, CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask();
    }

    /// <inheritdoc cref="ISchedulerListener.SchedulerInStandbyMode"/>
    public ValueTask SchedulerInStandbyMode(CancellationToken cancellationToken = new CancellationToken())
    {
        _eventHandler.InvokeQueuePaused();
        return new ValueTask();
    }

    /// <inheritdoc cref="ISchedulerListener.SchedulerStarted"/>
    public ValueTask SchedulerStarted(CancellationToken cancellationToken = new CancellationToken())
    {
        _eventHandler.InvokeQueueStarted();
        return new ValueTask();
    }

    /// <inheritdoc cref="ISchedulerListener.SchedulerStarting"/>
    public ValueTask SchedulerStarting(CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask();
    }

    /// <inheritdoc cref="ISchedulerListener.SchedulerShutdown"/>
    public ValueTask SchedulerShutdown(CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask();
    }

    /// <inheritdoc cref="ISchedulerListener.SchedulerShuttingdown"/>
    public ValueTask SchedulerShuttingdown(CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask();
    }

    /// <inheritdoc cref="ISchedulerListener.JobScheduled"/>
    public ValueTask SchedulingDataCleared(CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask();
    }

    /// <inheritdoc cref="ISchedulerListener.JobScheduled"/>
    public ValueTask JobScheduled(ITrigger trigger, CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask();
    }

    /// <inheritdoc cref="ISchedulerListener.JobUnscheduled"/>
    public ValueTask JobUnscheduled(TriggerKey triggerKey, CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask();
    }

    /// <inheritdoc cref="ISchedulerListener.TriggerFinalized"/>
    public ValueTask TriggerFinalized(ITrigger trigger, CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask();
    }

    /// <inheritdoc cref="ISchedulerListener.TriggerPaused"/>
    public ValueTask TriggerPaused(TriggerKey triggerKey, CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask();
    }

    /// <inheritdoc cref="ISchedulerListener.TriggersPaused"/>
    public ValueTask TriggersPaused(string triggerGroup, CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask();
    }

    /// <inheritdoc cref="ISchedulerListener.TriggerResumed"/>
    public ValueTask TriggerResumed(TriggerKey triggerKey, CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask();
    }

    /// <inheritdoc cref="ISchedulerListener.TriggersResumed"/>
    public ValueTask TriggersResumed(string triggerGroup, CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask();
    }

    /// <inheritdoc cref="ISchedulerListener.JobAdded"/>
    public ValueTask JobAdded(IJobDetail jobDetail, CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask();
    }

    /// <inheritdoc cref="ISchedulerListener.JobDeleted"/>
    public ValueTask JobDeleted(JobKey jobKey, CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask();
    }

    /// <inheritdoc cref="ISchedulerListener.JobPaused"/>
    public ValueTask JobPaused(JobKey jobKey, CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask();
    }

    /// <inheritdoc cref="ISchedulerListener.JobInterrupted"/>
    public ValueTask JobInterrupted(JobKey jobKey, CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask();
    }

    /// <inheritdoc cref="ISchedulerListener.JobsPaused"/>
    public ValueTask JobsPaused(string jobGroup, CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask();
    }

    /// <inheritdoc cref="ISchedulerListener.JobResumed"/>
    public ValueTask JobResumed(JobKey jobKey, CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask();
    }

    /// <inheritdoc cref="ISchedulerListener.JobsResumed"/>
    public ValueTask JobsResumed(string jobGroup, CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask();
    }
}
