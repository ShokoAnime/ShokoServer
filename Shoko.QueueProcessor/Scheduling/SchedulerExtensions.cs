using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.QueueProcessor.Abstractions;

namespace Shoko.QueueProcessor.Scheduling;

/// <summary>
/// Extension methods that adapt <see cref="IQueueScheduler"/> to the <c>StartJob&lt;T&gt;</c>
/// call-site convention. Plugins and server-side callers use this as a thin wrapper around
/// <see cref="IQueueScheduler.Enqueue{T}"/>.
/// </summary>
public static class SchedulerExtensions
{
    /// <summary>
    /// Enqueue a job of type <typeparamref name="T"/>, optionally configuring its properties.
    /// </summary>
    public static Task StartJob<T>(
        this IQueueScheduler scheduler,
        Action<T>? configure = null,
        bool prioritize = false,
        DateTimeOffset? startTime = null,
        CancellationToken ct = default)
        where T : class, IQueueJob
        => scheduler.Enqueue(configure, prioritize, startTime, ct);

    /// <summary>
    /// Convenience alias for <see cref="IQueueScheduler.EnqueueImmediate{T}"/>.
    /// Enqueues at max priority, promotes an existing waiting job, and awaits completion.
    /// </summary>
    public static Task RunImmediately<T>(
        this IQueueScheduler scheduler,
        Action<T>? configure = null,
        Func<Exception?, Task>? onComplete = null,
        CancellationToken ct = default)
        where T : class, IQueueJob
        => scheduler.EnqueueImmediate(configure, onComplete, ct);
}
