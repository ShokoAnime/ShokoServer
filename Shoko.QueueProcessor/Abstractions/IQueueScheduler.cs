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

    /// <summary>
    /// Enqueues a job at maximum priority and returns a <see cref="Task"/> that completes when
    /// the job finishes. If a job with the same key is already <em>waiting</em>, it is promoted
    /// to run immediately instead of being a no-op. If it is already <em>executing</em>, the
    /// returned task completes when that execution finishes.
    /// </summary>
    /// <param name="configure">Configures the job's data properties.</param>
    /// <param name="onComplete">
    /// Optional async callback invoked on completion. Receives <see langword="null"/> on success
    /// or the faulting exception on failure. The returned <see cref="Task"/> also reflects the
    /// outcome, so callers can choose to await it or use the callback (or both).
    /// </param>
    /// <param name="ct">Cancellation token for the await; does not cancel the job itself.</param>
    /// <exception cref="JobBlockedException">
    /// Thrown immediately if any acquisition filter currently blocks this job type.
    /// </exception>
    Task EnqueueImmediate<T>(
        Action<T>? configure = null,
        Func<Exception?, Task>? onComplete = null,
        CancellationToken ct = default)
        where T : class, IQueueJob;

    /// <summary>
    /// Registers a job to run at maximum priority immediately after the currently-executing job
    /// completes. If a job with the same key is already waiting in the queue it is pulled and
    /// held until the parent finishes, preventing it from running with stale data. If called
    /// outside a worker job context, falls back to <see cref="Enqueue{T}"/> with
    /// <c>prioritize: true</c>. Multiple calls for the same key within one parent execution
    /// are deduplicated.
    /// </summary>
    Task RunAfterCurrent<T>(Action<T>? configure = null, CancellationToken ct = default)
        where T : class, IQueueJob;

    /// <summary>Enqueue multiple jobs in a single batch operation.</summary>
    Task EnqueueRange(IEnumerable<(Type JobType, string JobKey, string DataJson, int Priority, DateTimeOffset? ScheduledAt)> jobs, CancellationToken ct = default);

    /// <summary>Remove a waiting job by key. No-op if the key is not found or the job is already executing.</summary>
    Task Remove(string jobKey, CancellationToken ct = default);

    /// <summary>Remove a waiting job by type and configuration. Derives the key the same way <see cref="Enqueue{T}"/> does.</summary>
    Task Remove<T>(Action<T>? configure = null, CancellationToken ct = default)
        where T : class, IQueueJob;

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

    /// <summary>
    /// Creates a builder for a sequential job chain. Any <see cref="IQueueJob"/> type can be
    /// added; the builder is completely provider-agnostic. Call
    /// <see cref="IJobChainBuilder.EnqueueAfterCurrent"/> or
    /// <see cref="IJobChainBuilder.Enqueue"/> to submit the chain.
    /// </summary>
    IJobChainBuilder CreateJobChain();

    /// <summary>
    /// Returns <see langword="true"/> if any active acquisition filter currently blocks
    /// <paramref name="jobType"/> from being dispatched to a worker. Uses the same filter
    /// evaluation as <see cref="EnqueueImmediate{T}"/>.
    /// </summary>
    bool IsJobTypeBlocked(Type jobType);

    /// <summary>
    /// Enqueue a job whose type is known only at runtime.
    /// Provider-agnostic general-purpose overload of <see cref="Enqueue{T}"/>.
    /// </summary>
    Task Enqueue(Type jobType, Action<IQueueJob>? configure = null, bool prioritize = false);

    /// <summary>
    /// Register a job (by runtime type) to run after the currently-executing job.
    /// Provider-agnostic general-purpose overload of <see cref="RunAfterCurrent{T}"/>.
    /// </summary>
    Task RunAfterCurrent(Type jobType, Action<IQueueJob>? configure = null);
}
