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
    public Task SchedulerError(string msg, SchedulerException cause, CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ISchedulerListener.SchedulerInStandbyMode"/>
    public Task SchedulerInStandbyMode(CancellationToken cancellationToken = new CancellationToken())
    {
        _eventHandler.InvokeQueuePaused();
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ISchedulerListener.SchedulerStarted"/>
    public Task SchedulerStarted(CancellationToken cancellationToken = new CancellationToken())
    {
        _eventHandler.InvokeQueueStarted();
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ISchedulerListener.SchedulerStarting"/>
    public Task SchedulerStarting(CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ISchedulerListener.SchedulerShutdown"/>
    public Task SchedulerShutdown(CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ISchedulerListener.SchedulerShuttingdown"/>
    public Task SchedulerShuttingdown(CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ISchedulerListener.JobScheduled"/>
    public Task SchedulingDataCleared(CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ISchedulerListener.JobScheduled"/>
    public Task JobScheduled(ITrigger trigger, CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ISchedulerListener.JobUnscheduled"/>
    public Task JobUnscheduled(TriggerKey triggerKey, CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ISchedulerListener.TriggerFinalized"/>
    public Task TriggerFinalized(ITrigger trigger, CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ISchedulerListener.TriggerPaused"/>
    public Task TriggerPaused(TriggerKey triggerKey, CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ISchedulerListener.TriggersPaused"/>
    public Task TriggersPaused(string triggerGroup, CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ISchedulerListener.TriggerResumed"/>
    public Task TriggerResumed(TriggerKey triggerKey, CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ISchedulerListener.TriggersResumed"/>
    public Task TriggersResumed(string triggerGroup, CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ISchedulerListener.JobAdded"/>
    public Task JobAdded(IJobDetail jobDetail, CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ISchedulerListener.JobDeleted"/>
    public Task JobDeleted(JobKey jobKey, CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ISchedulerListener.JobPaused"/>
    public Task JobPaused(JobKey jobKey, CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ISchedulerListener.JobInterrupted"/>
    public Task JobInterrupted(JobKey jobKey, CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ISchedulerListener.JobsPaused"/>
    public Task JobsPaused(string jobGroup, CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ISchedulerListener.JobResumed"/>
    public Task JobResumed(JobKey jobKey, CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ISchedulerListener.JobsResumed"/>
    public Task JobsResumed(string jobGroup, CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.CompletedTask;
    }
}
