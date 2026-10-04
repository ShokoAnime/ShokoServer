using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Shoko.QueueProcessor.Abstractions;

/// <summary>
/// The priorities a job is queued with. A higher one runs first; jobs of the
/// same priority run in the order they were queued.
/// </summary>
public static class QueuePriority
{
    #region Constants

    /// <summary>
    /// The priority of a job queued normally.
    /// </summary>
    public const int Default = 0;

    /// <summary>
    /// The priority of a scheduled action's run, ahead of the jobs queued
    /// normally but behind the prioritized ones.
    /// </summary>
    public const int Scheduled = 5;

    /// <summary>
    /// The priority of a job queued with <c>prioritize</c>.
    /// </summary>
    public const int Prioritized = 10;

    /// <summary>
    /// The priority of a job that runs next, as
    /// <see cref="IQueueScheduler.EnqueueImmediate{T}"/> and
    /// <see cref="IQueueScheduler.RunAfterCurrent{T}"/> queue them.
    /// </summary>
    public const int Immediate = int.MaxValue;

    #endregion

    #region Per Type

    private static readonly ConcurrentDictionary<Type, (int Default, int Prioritized)> _byType = new();

    /// <summary>
    /// Gets the priority a job of <paramref name="jobType"/> is queued with, from its
    /// <see cref="JobPriorityAttribute"/> or else <see cref="Default"/> and
    /// <see cref="Prioritized"/>.
    /// </summary>
    /// <param name="jobType">The job type.</param>
    /// <param name="prioritize">Whether the job is queued with <c>prioritize</c>.</param>
    /// <returns>The priority.</returns>
    /// <exception cref="InvalidOperationException">The type's <see cref="JobPriorityAttribute"/> is invalid.</exception>
    public static int For(Type jobType, bool prioritize)
    {
        var (defaultPriority, prioritizedPriority) = _byType.GetOrAdd(jobType, Resolve);
        return prioritize ? prioritizedPriority : defaultPriority;
    }

    /// <summary>
    /// Gets the priority a scheduled run queues a job of <paramref name="jobType"/> with: the
    /// higher of <see cref="Scheduled"/> and the type's default, or the type's prioritized
    /// priority when the run is prioritized.
    /// </summary>
    /// <param name="jobType">The job type.</param>
    /// <param name="prioritize">Whether the run is prioritized.</param>
    /// <returns>The priority.</returns>
    /// <exception cref="InvalidOperationException">The type's <see cref="JobPriorityAttribute"/> is invalid.</exception>
    public static int ScheduledFor(Type jobType, bool prioritize)
        => prioritize ? For(jobType, prioritize: true) : Math.Max(Scheduled, For(jobType, prioritize: false));

    /// <summary>
    /// Reads and checks the priorities <paramref name="jobType"/> declares.
    /// </summary>
    /// <param name="jobType">The job type.</param>
    /// <returns>The default and prioritized priorities.</returns>
    /// <exception cref="InvalidOperationException">The type's <see cref="JobPriorityAttribute"/> is invalid.</exception>
    private static (int Default, int Prioritized) Resolve(Type jobType)
    {
        if (jobType.GetCustomAttribute<JobPriorityAttribute>(inherit: true) is not { } attribute)
            return (Default, Prioritized);

        if (attribute.Prioritized < attribute.Default)
            throw new InvalidOperationException(
                $"Job type '{jobType.FullName}' declares a prioritized priority ({attribute.Prioritized}) below its default priority ({attribute.Default})."
            );
        if (attribute.Prioritized >= Immediate)
            throw new InvalidOperationException(
                $"Job type '{jobType.FullName}' declares a prioritized priority ({attribute.Prioritized}) of {nameof(Immediate)} or above."
            );

        return (attribute.Default, attribute.Prioritized);
    }

    #endregion
}
