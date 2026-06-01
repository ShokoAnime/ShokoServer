using System;
using System.Threading.Tasks;

namespace Shoko.QueueProcessor.Abstractions;

/// <summary>
/// Builds a sequential chain of jobs where each job runs immediately after the previous one
/// completes. The chain builder is provider-agnostic — any <see cref="IQueueJob"/> type can
/// be added to the chain.
/// </summary>
public interface IJobChainBuilder
{
    /// <summary>Append a typed job to the chain.</summary>
    IJobChainBuilder Then<T>(Action<T>? configure = null) where T : class, IQueueJob;

    /// <summary>
    /// Append a job whose type is known only at runtime. Uses reflection internally.
    /// Provider-agnostic general-purpose overload.
    /// </summary>
    IJobChainBuilder Then(Type jobType, Action<IQueueJob>? configure = null);

    /// <summary>
    /// Register the chain so entry[0] runs after the currently-executing job
    /// (<see cref="IQueueScheduler.RunAfterCurrent{T}"/> semantics), then each subsequent
    /// entry runs after the previous. Falls back to <see cref="Enqueue"/> if called outside
    /// a worker context.
    /// </summary>
    Task EnqueueAfterCurrent();

    /// <summary>
    /// Enqueue entry[0] normally into the queue; register the rest as a chain following it.
    /// </summary>
    Task Enqueue();
}
