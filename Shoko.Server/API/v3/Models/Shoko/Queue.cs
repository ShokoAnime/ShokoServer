using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

public class Queue
{
    /// <summary>
    /// The number of jobs waiting to execute, but nothing is blocking it except available threads
    /// </summary>
    [Required]
    public int WaitingCount { get; set; }

    /// <summary>
    /// The number of jobs that can't run due to various circumstances, such as concurrency limits or bans
    /// </summary>
    [Required]
    public int BlockedCount { get; set; }

    /// <summary>
    /// The total number of jobs waiting to execute, regardless of state
    /// </summary>
    [Required]
    public int TotalCount { get; set; }

    /// <summary>
    /// The number of threads that the queue will use. This is the maximum number of concurrent jobs
    /// </summary>
    [Required]
    public int ThreadCount { get; set; }

    /// <summary>
    /// The currently executing jobs and their details
    /// </summary>
    [Required]
    public List<QueueItem> CurrentlyExecuting { get; set; } = [];

    /// <summary>
    /// Per-pool (concurrency-group) status. Lets clients show "this group is processing"
    /// without waiting for per-job events that may not arrive for very fast jobs — the pool's
    /// <see cref="PoolState.ActiveWorkers"/> increments the moment a job is acquired, well before
    /// DI scope construction, job-data deserialization, or the executing event fires.
    /// </summary>
    [Required]
    public List<PoolState> Pools { get; set; } = [];

    public class QueueItem
    {
        /// <summary>
        /// The JobKey (in quartz terms) of the queue item. This can be shared across multiple
        /// queue items over the life span of the queue, but only one item will
        /// exist with the same name at any given time.
        /// </summary>
        [Required]
        public string Key { get; init; } = string.Empty;

        /// <summary>
        /// The queue item type.
        /// </summary>
        [Required]
        public string Type { get; init; } = string.Empty;

        /// <summary>
        /// The Title line of a Queue Item, e.g. Hashing File
        /// </summary>
        [Required]
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// The details of the queue item. e.g. { "File Path": "/mnt/Drop/Steins Gate/Episode 1.mkv" }
        /// </summary>
        [Required]
        public Dictionary<string, object> Details { get; init; } = [];

        /// <summary>
        /// Indicates the item is currently actively running in the queue.
        /// </summary>
        [Required]
        public bool IsRunning { get; init; }

        /// <summary>
        /// The time that a currently executing job started, in UTC ±0 timezone.
        /// </summary>
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime? StartTime { get; init; }

        /// <summary>
        /// Indicates the item is currently disabled because it cannot run under
        /// the current conditions (e.g. a UDP or HTTP ban is active, etc.).
        /// </summary>
        [Required]
        public bool IsBlocked { get; init; }

        /// <summary>
        /// The name of the worker pool responsible for this job.
        /// </summary>
        public string? PoolName { get; init; }

        /// <summary>
        /// How many times this job has been retried after a failure.
        /// </summary>
        [Required]
        public int RetryCount { get; init; }
    }

    public class PoolState
    {
        /// <summary>
        /// Pool name — matches the concurrency-group name, or the job-type name when a
        /// type has no explicit group.
        /// </summary>
        [Required]
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Maximum number of concurrent workers in this pool.
        /// </summary>
        [Required]
        public int MaxWorkers { get; init; }

        /// <summary>
        /// Workers currently executing a job. Incremented at acquisition time, so this is the
        /// authoritative "is the group doing work right now" signal — independent of when
        /// per-job events get serialized to clients.
        /// </summary>
        [Required]
        public int ActiveWorkers { get; init; }

        /// <summary>
        /// Workers currently idle, awaiting a job.
        /// </summary>
        [Required]
        public int IdleWorkers { get; init; }

        /// <summary>
        /// Number of waiting jobs in this pool's sub-queue.
        /// </summary>
        [Required]
        public int WaitingCount { get; init; }

        /// <summary>
        /// True when every job type in this pool is currently excluded by an acquisition
        /// filter (e.g., AniDB banned, network unavailable).
        /// </summary>
        [Required]
        public bool IsBlocked { get; init; }

        /// <summary>
        /// Short names of job types handled by this pool.
        /// </summary>
        [Required]
        public IReadOnlyList<string> HandledTypeNames { get; init; } = [];

        /// <summary>
        /// UTC timestamp of the most recent job acquisition. Null if the pool has never run a job.
        /// Use this to render a "Processing" indicator under server-side throttling: when the
        /// throttled snapshot arrives with <see cref="ActiveWorkers"/> back to 0, this still
        /// shows how recently the group was busy.
        /// </summary>
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime? LastActiveAt { get; init; }
    }
}
