#nullable enable
using System;
using System.Collections.Generic;

namespace Shoko.QueueProcessor.Abstractions;

/// <summary>
/// Dynamically excludes job types from acquisition based on external conditions
/// (e.g., AniDB rate limit active, network unavailable, database not ready).
/// Implementations live in Shoko.Server and are registered in DI.
/// </summary>
public interface IAcquisitionFilter
{
    /// <summary>
    /// Returns the set of job types that must NOT be acquired while this filter is active.
    /// Called by <see cref="Workers.WorkerPool.TryAcquire"/> under the pool's sub-queue lock;
    /// implementations should return a cached value — do not compute on every call.
    /// </summary>
    IEnumerable<Type> GetTypesToExclude();

    /// <summary>
    /// Fires when the set of excluded types changes (e.g., rate limit lifted, network restored).
    /// The orchestrator uses this to signal affected pools to retry acquisition.
    /// </summary>
    event EventHandler StateChanged;

    /// <summary>
    /// The attribute type that, when present on a job class, associates that job with this filter's pool.
    /// Return <c>null</c> if this filter applies to all pools (e.g., database-required filter).
    /// </summary>
    Type? WatchedAttributeType => null;
}
