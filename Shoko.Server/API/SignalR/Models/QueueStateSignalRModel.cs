using System.Collections.Generic;
using Shoko.Server.API.v3.Models.Shoko;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class QueueStateSignalRModel
{
    /// <summary>
    /// Is the queue running? When the server is starting, shutting down, or the queue is paused, this will be false.
    /// For the sake of notifying the user, there is no important difference between those states.
    /// </summary>
    public bool Running { get; set; }

    /// <summary>
    /// The number of jobs waiting to execute, but nothing is blocking it except available threads
    /// </summary>
    public int WaitingCount { get; set; }

    /// <summary>
    /// The number of jobs that can't run due to various circumstances, such as concurrency limits or bans
    /// </summary>
    public int BlockedCount { get; set; }

    /// <summary>
    /// The number of jobs deferred to a future scheduled time (not yet ready to run). Not counted in <see cref="WaitingCount"/>.
    /// </summary>
    public int ScheduledCount { get; set; }

    /// <summary>
    /// The total number of jobs in the queue, regardless of state
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// The number of threads that the queue will use. This is the maximum number of concurrent jobs
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// The currently executing jobs and their details
    /// </summary>
    public List<Queue.QueueItem> CurrentlyExecuting { get; set; } = [];

    /// <summary>
    /// Per-pool (concurrency-group) status. <see cref="Queue.PoolState.ActiveWorkers"/>
    /// is set at acquisition time, so clients can show "the group is processing" even when
    /// jobs complete before per-job events make it through the SignalR pipeline.
    /// </summary>
    public List<Queue.PoolState> Pools { get; set; } = [];
}
