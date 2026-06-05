using System;
using System.Collections.Generic;

namespace Shoko.QueueProcessor.Abstractions;

/// <summary>A lightweight snapshot of a single queued or executing job for API/SignalR responses.</summary>
public record struct QueueItem
{
    /// <summary>Unique string key identifying this job instance (pool/type/data-derived).</summary>
    public string Key { get; init; }

    /// <summary>Short name of the job type (e.g., <c>"HashFileJob"</c>).</summary>
    public string? JobType { get; init; }

    /// <summary>
    /// Display-friendly type name from <see cref="IQueueJob.TypeName"/>. Falls back to
    /// <see cref="JobType"/> when the job didn't override it. Populated after the job instance
    /// is resolved and <see cref="IQueueJob.PostInit"/> has run.
    /// </summary>
    public string? TypeName { get; init; }

    /// <summary>Human-readable description of this specific job instance.</summary>
    public string? Title { get; init; }

    /// <summary>Key/value pairs for display in the queue UI.</summary>
    public Dictionary<string, object>? Details { get; init; }

    /// <summary>True when this job is currently executing.</summary>
    public bool Running { get; init; }

    /// <summary>True when this job is waiting but blocked by an acquisition filter.</summary>
    public bool Blocked { get; init; }

    /// <summary>
    /// True when this job is deferred to a future <see cref="ScheduledAt"/> and is not yet ready
    /// to run (retry backoff or an intentionally delayed re-fetch). Distinct from <see cref="Blocked"/>.
    /// </summary>
    public bool Scheduled { get; init; }

    /// <summary>When the job started executing; null if still waiting.</summary>
    public DateTime? StartTime { get; init; }

    /// <summary>Name of the pool executing or queued for this job.</summary>
    public string? PoolName { get; init; }

    /// <summary>How many times this job has been retried.</summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// Earliest time this job may be dispatched. <c>null</c> means "as soon as a worker is free".
    /// A value in the future means the job is intentionally deferred (e.g. an AniDB re-download
    /// backoff or a retry delay) and is waiting for this time — it is not stuck.
    /// </summary>
    public DateTimeOffset? ScheduledAt { get; init; }

    /// <summary>
    /// The <see cref="Key"/> of the job that must complete before this one runs, when it was
    /// scheduled to follow another job. <c>null</c> for standalone jobs or when the parent is
    /// no longer in the queue.
    /// </summary>
    public string? ParentKey { get; init; }
}
