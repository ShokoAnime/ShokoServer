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
}
