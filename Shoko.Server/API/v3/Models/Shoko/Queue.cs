using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

public class Queue
{
    /// <summary>
    /// The number of jobs waiting to execute, but nothing is blocking it except available threads
    /// </summary>
    public int WaitingCount { get; set; }

    /// <summary>
    /// The number of jobs that can't run due to various circumstances, such as concurrency limits or bans
    /// </summary>
    public int BlockedCount { get; set; }

    /// <summary>
    /// The total number of jobs waiting to execute, regardless of state
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// The number of threads that the queue will use. This is the maximum number of concurrent jobs
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// The currently executing jobs and their details
    /// </summary>
    public List<QueueItem> CurrentlyExecuting { get; set; }

    public class QueueItem
    {
        /// <summary>
        /// The JobKey (in quartz terms) of the queue item. This can be shared across multiple
        /// queue items over the life span of the queue, but only one item will
        /// exist with the same name at any given time.
        /// </summary>
        public string Key { get; init; }

        /// <summary>
        /// The queue item type.
        /// </summary>
        public string Type { get; init; }

        /// <summary>
        /// The Title line of a Queue Item, e.g. Hashing File
        /// </summary>
        public string Title { get; init; }

        /// <summary>
        /// The details of the queue item. e.g. { "File Path": "/mnt/Drop/Steins Gate/Episode 1.mkv" }
        /// </summary>
        public Dictionary<string, object> Details { get; init; }

        /// <summary>
        /// Indicates the item is currently actively running in the queue.
        /// </summary>
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
        public bool IsBlocked { get; init; }
    }
}
