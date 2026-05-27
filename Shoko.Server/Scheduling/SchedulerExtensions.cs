#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.QueueProcessor.Abstractions;

namespace Shoko.Server.Scheduling;

/// <summary>
/// Extension methods that adapt <see cref="IQueueScheduler"/> to the old
/// <c>IScheduler.StartJob&lt;T&gt;</c> call-site convention used throughout the server.
/// </summary>
public static class SchedulerExtensions
{
    /// <summary>
    /// Enqueue a job of type <typeparamref name="T"/>, optionally configuring its properties.
    /// This is a thin wrapper around <see cref="IQueueScheduler.Enqueue{T}"/> that preserves
    /// the existing <c>StartJob</c> call-site signatures.
    /// </summary>
    public static Task StartJob<T>(
        this IQueueScheduler scheduler,
        Action<T>? configure = null,
        bool prioritize = false,
        DateTimeOffset? startTime = null,
        CancellationToken ct = default)
        where T : class, IQueueJob
        => scheduler.Enqueue(configure, prioritize, startTime, ct);
}
