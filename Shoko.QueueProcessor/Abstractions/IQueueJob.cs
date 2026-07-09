using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shoko.QueueProcessor.Abstractions;

/// <summary>
/// Base interface for all queue jobs. Implementations live in Shoko.Server; this library
/// resolves them at runtime via <see cref="IServiceProvider"/> so there is no
/// compile-time dependency between the queue engine and job implementations.
/// </summary>
public interface IQueueJob
{
    /// <summary>Display name for the job type (used in API / SignalR responses).</summary>
    string TypeName { get; }

    /// <summary>Human-readable description of this specific job instance.</summary>
    string Title { get; }

    /// <summary>Key/value pairs surfaced in the queue API for display purposes.</summary>
    Dictionary<string, object> Details { get => []; }

    /// <summary>
    /// Called once by the worker immediately after the job is resolved from DI and before
    /// <see cref="PostInit"/> runs. Use to acquire infrastructure services (loggers, etc.)
    /// that cannot be constructor-injected due to the job's registration pattern.
    /// The default implementation is a no-op; override only when needed.
    /// </summary>
    void Setup(IServiceProvider serviceProvider) { }

    /// <summary>
    /// Called once by the worker after the job's properties have been populated from
    /// <c>JobDataJson</c> but before <see cref="Process"/> is invoked. Use to resolve
    /// any state that depends on the populated data.
    /// </summary>
    void PostInit() { }

    /// <summary>Execute the job. Exceptions are caught by the worker and routed to the retry policy.</summary>
    Task Process();
}
