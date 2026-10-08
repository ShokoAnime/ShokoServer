using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Builder;

namespace Shoko.Server.Actions;

/// <summary>
/// The base of a core scheduled action whose work is one queue job of type
/// <typeparamref name="TJob"/>. A run queues that job itself, in place of the
/// job that runs a scheduled action.
/// </summary>
/// <typeparam name="TJob">The job type.</typeparam>
/// <param name="scheduler">The queue.</param>
public abstract class QueueJobScheduledAction<TJob>(IQueueScheduler scheduler) : IScheduledAction, IQueueJobScheduledAction
    where TJob : class, IQueueJob
{
    /// <summary>
    /// The display name.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// The description, or <c>null</c> for none.
    /// </summary>
    public virtual string? Description => null;

    /// <summary>
    /// The category it is listed under.
    /// </summary>
    public virtual ActionCategory Category => ActionCategory.Miscellaneous;

    /// <summary>
    /// When it runs on its own until the admin says otherwise. None by
    /// default.
    /// </summary>
    public virtual IReadOnlyList<ActionTrigger> DefaultTriggers => [];

    /// <summary>
    /// The shortest time after a run before it runs on its own again, or
    /// <c>null</c> for a minute.
    /// </summary>
    public virtual TimeSpan? MinimumInterval => null;

    /// <summary>
    /// Whether a run by hand counts for the schedule. Not by default.
    /// </summary>
    public virtual bool ScheduleCountsManualRuns => false;

    /// <summary>
    /// Whether a run queues the job at its type's prioritized priority. Not by
    /// default: a run takes the higher of <see cref="QueuePriority.Scheduled"/>
    /// and the type's default priority.
    /// </summary>
    protected virtual bool Prioritize => false;

    /// <summary>
    /// The key of the job a run queues.
    /// </summary>
    public string JobKey
        => JobKeyBuilder<TJob>.Create().UsingJobData(Configure).Build();

    /// <summary>
    /// Sets the data of the job a run queues. Sets nothing by default.
    /// </summary>
    /// <param name="job">The job.</param>
    protected virtual void Configure(TJob job) { }

    /// <summary>
    /// Queues the job, unless one with the same key is already waiting or
    /// running.
    /// </summary>
    /// <param name="token">Cancels the queuing.</param>
    /// <returns>A task that completes once the job is queued.</returns>
    public Task EnqueueJob(CancellationToken token)
        => scheduler.EnqueueWithPriority<TJob>(Configure, QueuePriority.ScheduledFor(typeof(TJob), Prioritize), ct: token);

    /// <summary>
    /// Queues the job, as a run does.
    /// </summary>
    /// <param name="progress">Unused: the job reports its own progress.</param>
    /// <param name="token">Cancels the queuing.</param>
    /// <returns>A task that completes once the job is queued.</returns>
    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => EnqueueJob(token);
}
