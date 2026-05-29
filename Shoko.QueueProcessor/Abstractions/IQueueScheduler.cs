using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.QueueProcessor.Abstractions;

/// <summary>
/// Public entry point for enqueueing and managing the job queue.
/// Replaces <c>IScheduler</c> / <c>QuartzExtensions.StartJob&lt;T&gt;</c> at all call sites.
/// </summary>
public interface IQueueScheduler
{
    /// <summary>
    /// Enqueue a single job of type <typeparamref name="T"/>. A no-op if a job with the same
    /// key is already waiting or executing (dedup is O(1) in-memory).
    /// </summary>
    Task Enqueue<T>(Action<T>? configure = null, bool prioritize = false, DateTimeOffset? scheduledAt = null, CancellationToken ct = default)
        where T : class, IQueueJob;

    /// <summary>Enqueue multiple jobs in a single batch operation.</summary>
    Task EnqueueRange(IEnumerable<(Type JobType, string JobKey, string DataJson, int Priority, DateTimeOffset? ScheduledAt)> jobs, CancellationToken ct = default);

    /// <summary>Remove all waiting jobs from the queue (executing jobs are unaffected).</summary>
    Task Clear(CancellationToken ct = default);

    /// <summary>Pause dispatching. Currently executing jobs run to completion.</summary>
    Task Pause();

    /// <summary>Resume dispatching after a <see cref="Pause"/>.</summary>
    Task Resume();

    /// <summary>True while the queue is paused.</summary>
    bool IsPaused { get; }

    /// <summary>Retrieve the current queue state snapshot for API/SignalR consumption.</summary>
    Task<QueueState> GetState(int maxWaiting = 100, int offset = 0, bool includeBlocked = true, CancellationToken ct = default);

    /// <summary>Returns true if a job with <paramref name="jobKey"/> is already waiting or executing.</summary>
    bool IsQueued(string jobKey);
}
