using System;
using System.Collections.Generic;

namespace Shoko.QueueProcessor.Abstractions;

/// <summary>A lightweight snapshot of a single queued or executing job for API/SignalR responses.</summary>
public struct QueueItem
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

    /// <summary>When the job started executing; null if still waiting.</summary>
    public DateTime? StartTime { get; init; }

    /// <summary>Name of the pool executing or queued for this job.</summary>
    public string? PoolName { get; init; }

    /// <summary>How many times this job has been retried.</summary>
    public int RetryCount { get; init; }
}
