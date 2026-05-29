using System;
using System.Collections.Generic;

namespace Shoko.QueueProcessor.Abstractions;

/// <summary>
/// Describes a running worker pool. Instances are built by <see cref="Orchestration.PoolDiscovery"/>
/// at startup from concurrency attributes — they are never manually registered.
/// </summary>
public interface IWorkerPool
{
    /// <summary>Pool name; matches the <c>DisallowConcurrencyGroup</c> name or the job type name.</summary>
    string Name { get; }

    /// <summary>
    /// Maximum number of concurrent workers for this pool. Derived from
    /// <see cref="Concurrency.LimitConcurrencyAttribute"/>; mutable at runtime via
    /// <see cref="QueueProcessorOptions.LimitedConcurrencyOverrides"/>.
    /// </summary>
    int MaxWorkers { get; }

    /// <summary>Job types whose execution is managed by this pool.</summary>
    IReadOnlyList<Type> HandledTypes { get; }

    /// <summary>Acquisition filters associated with this pool.</summary>
    IReadOnlyList<IAcquisitionFilter> AcquisitionFilters { get; }

    /// <summary>Number of worker tasks currently awaiting a job (idle).</summary>
    int IdleWorkers { get; }

    /// <summary>Number of worker tasks currently executing a job.</summary>
    int ActiveWorkers { get; }
}
