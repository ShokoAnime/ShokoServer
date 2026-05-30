using System;
using System.Threading.Tasks;

namespace Shoko.QueueProcessor.Abstractions;

/// <summary>
/// Creates and executes queue jobs outside of the worker pipeline.
/// Use for direct (non-queued) execution where the caller needs to await the result.
/// </summary>
public interface IJobFactory
{
    /// <summary>
    /// Returns <see langword="true"/> if no acquisition filter currently blocks jobs of type
    /// <typeparamref name="T"/>. This is a point-in-time check; filter state may change.
    /// </summary>
    bool CanRun<T>() where T : class, IQueueJob;

    /// <summary>
    /// Resolves a <typeparamref name="T"/> from DI, applies <paramref name="configure"/>,
    /// runs <see cref="IQueueJob.Setup"/> and <see cref="IQueueJob.PostInit"/>, then awaits
    /// <see cref="IQueueJob.Process"/> — all within a single DI scope.
    /// </summary>
    /// <exception cref="JobBlockedException">
    /// Thrown immediately (before DI resolution) if any acquisition filter blocks this job type.
    /// </exception>
    Task Execute<T>(Action<T>? configure = null) where T : class, IQueueJob;
}
