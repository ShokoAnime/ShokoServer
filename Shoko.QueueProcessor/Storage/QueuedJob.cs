using System;

namespace Shoko.QueueProcessor.Storage;

/// <summary>
/// Persistence entity for a queued job. Rows exist only for jobs that have not yet been
/// successfully completed. No status column — executing state lives in-memory only.
/// </summary>
public class QueuedJob
{
    /// <summary>Unique job instance identifier. Never changes after creation.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Short assembly-qualified type name: <c>"TypeName, AssemblyShortName"</c>.
    /// Interned at runtime (<see cref="string.Intern"/>) to save memory when many jobs
    /// share the same type.
    /// </summary>
    public string JobType { get; set; } = string.Empty;

    /// <summary>
    /// Stable dedup key derived from <c>[JobKeyMember]</c>-annotated properties.
    /// Unique index on the table prevents duplicate rows; primary dedup is in-memory.
    /// </summary>
    public string JobKey { get; set; } = string.Empty;

    /// <summary>
    /// Newtonsoft JSON of all public settable job properties.
    /// Applied to the job instance before <c>Process()</c> via
    /// <see cref="Builder.JobDataSerializer.Apply"/>.
    /// </summary>
    public string? JobDataJson { get; set; }

    /// <summary>Job priority. Higher values run first (FIFO within the same priority).</summary>
    public int Priority { get; set; }

    /// <summary>When the job was first enqueued.</summary>
    public DateTimeOffset QueuedAt { get; set; }

    /// <summary>
    /// Earliest time the job may be dispatched. <c>null</c> means dispatch immediately.
    /// Used for deferred jobs and retry backoff delays.
    /// </summary>
    public DateTimeOffset? ScheduledAt { get; set; }

    /// <summary>
    /// Number of times this job has been retried. Persisted so crash-restart does not
    /// reset the exponential backoff schedule.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// The chain this job belongs to. Null for standalone (non-chain) jobs.
    /// Set by <c>JobChainBuilder</c> at chain-build time; used to locate the shared
    /// chain DI scope and <c>JobChainContext</c>.
    /// </summary>
    public Guid? ChainId { get; set; }

    /// <summary>
    /// When true, this job runs even if the chain is aborted via <c>ChainAbortException</c>.
    /// Derived from <c>[ChainFinally]</c> on the job class; stored here so the orchestrator
    /// can act on it without reflection during chain-abort handling.
    /// </summary>
    public bool IsChainFinally { get; set; }

    /// <summary>
    /// The ID of the job that must complete before this job is activated.
    /// Non-null means the job is currently deferred in <c>_afterParentCallbacks</c>;
    /// cleared (set to null) when the job is promoted to the active waiting queue.
    /// Used on startup to reconstruct <c>_afterParentCallbacks</c> from persisted state.
    /// </summary>
    public Guid? ParentJobId { get; set; }
}
