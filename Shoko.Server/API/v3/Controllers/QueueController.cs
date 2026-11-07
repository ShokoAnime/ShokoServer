using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.Web.Attributes;
using Shoko.QueueProcessor;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Settings;

using ChainInfo = Shoko.QueueProcessor.Analytics.ChainDebugInfo;

namespace Shoko.Server.API.v3.Controllers;

// ReSharper disable once UnusedMember.Global
/// <summary>
/// The queue controller. Used for controlling the queues.
/// </summary>
[ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
[DatabaseBlockedExempt]
[Authorize(Roles = "user,admin,init")]
public class QueueController : BaseController
{
    private readonly QueueHandler _queueHandler;

    private readonly IQueueScheduler _scheduler;

    private readonly QueueHolds _holds;

    public QueueController(ISettingsProvider settingsProvider, QueueHandler queueHandler, IQueueScheduler scheduler, QueueHolds holds) : base(settingsProvider)
    {
        _queueHandler = queueHandler;
        _scheduler = scheduler;
        _holds = holds;
    }

    /// <summary>
    /// Get info about the queue.
    /// </summary>
    /// <returns>Info about the queue</returns>
    [HttpGet]
    [InitFriendly]
    public Queue GetQueue()
    {
        return new Queue
        {
            WaitingCount = _queueHandler.WaitingCount,
            BlockedCount = _queueHandler.BlockedCount,
            ScheduledCount = _queueHandler.ScheduledCount,
            TotalCount = _queueHandler.TotalCount,
            ThreadCount = _queueHandler.ThreadCount,
            CurrentlyExecuting = _queueHandler.GetExecutingJobs().Select(ToQueueItem).OrderBy(a => a.StartTime).ToList(),
            Pools = _queueHandler.GetPoolStatus().Values.Select(ToPoolState).OrderBy(p => p.Name).ToList()
        };
    }

    /// <summary>
    /// Get all the queued and active command types across the queue.
    /// </summary>
    /// <returns>A dictionary of all the queued and active command types, and the count for each type.</returns>
    [HttpGet("Types")]
    [InitFriendly]
    public ActionResult<Dictionary<string, int>> GetTypesForItemsInAllQueues()
    {
        var metrics = _queueHandler.GetMetrics();
        var result = new Dictionary<string, int>();
        foreach (var (typeName, typeMetrics) in metrics.ByType)
            result[typeName] = typeMetrics.Waiting + typeMetrics.Executing;
        return result;
    }

    /// <summary>
    /// Start the queue.
    /// </summary>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpPost("Resume")]
    [InitFriendly]
    public async Task<ActionResult> Resume()
    {
        await _queueHandler.Resume();
        return Ok();
    }

    /// <summary>
    /// Pause the queue.
    /// </summary>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpPost("Pause")]
    [InitFriendly]
    public async Task<ActionResult> Pause()
    {
        await _queueHandler.Pause();
        return Ok();
    }

    /// <summary>
    /// Get the pools and job types held by hand, and which of them the queue does not have, such as
    /// those of a plugin that is not loaded.
    /// </summary>
    /// <returns>The holds.</returns>
    [Authorize("admin")]
    [HttpGet("Holds")]
    [InitFriendly]
    public Queue.HoldList GetHolds()
    {
        var pools = _scheduler.HeldPools.Order(StringComparer.OrdinalIgnoreCase).ToList();
        var jobTypes = _scheduler.HeldJobTypes.Order(StringComparer.Ordinal).ToList();
        return new()
        {
            Pools = pools,
            JobTypes = jobTypes,
            UnknownPools = [.. pools.Where(name => !_holds.IsKnownPool(name))],
            UnknownJobTypes = [.. jobTypes.Where(key => !_holds.IsKnownJobType(key))],
        };
    }

    /// <summary>
    /// Hold worker pools and job types: their jobs wait, shown as blocked, while the rest of the
    /// queue runs. Jobs already running finish. The holds are kept across restarts.
    /// </summary>
    /// <param name="body">The pools and job types to hold.</param>
    /// <returns>Void, or a <c>400</c> validation problem naming what the queue does not have, holding nothing.</returns>
    [Authorize("admin")]
    [HttpPost("Holds")]
    [InitFriendly]
    public ActionResult Hold([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Queue.Input.HoldsBody body)
    {
        var pools = body.Pools.Where(name => !string.IsNullOrWhiteSpace(name)).ToList();
        var jobTypes = body.JobTypes.Where(key => !string.IsNullOrWhiteSpace(key)).ToList();
        foreach (var name in pools.Where(name => !_holds.IsKnownPool(name)))
            ModelState.AddModelError(nameof(body.Pools), $"The queue has no pool named \"{name}\".");
        foreach (var key in jobTypes.Where(key => !_holds.IsKnownJobType(key)))
            ModelState.AddModelError(nameof(body.JobTypes), $"The queue has no job type \"{key}\".");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        foreach (var name in pools)
            _scheduler.SetPoolHeld(name, true);
        foreach (var key in jobTypes)
            _scheduler.SetJobTypeHeld(key, true);
        return Ok();
    }

    /// <summary>
    /// Release held worker pools and job types. Any name is taken, so a hold the queue no longer
    /// knows can be cleared.
    /// </summary>
    /// <param name="body">The pools and job types to release.</param>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpDelete("Holds")]
    [InitFriendly]
    public ActionResult Release([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Queue.Input.HoldsBody body)
    {
        foreach (var name in body.Pools.Where(name => !string.IsNullOrWhiteSpace(name)))
            _scheduler.SetPoolHeld(name, false);
        foreach (var key in body.JobTypes.Where(key => !string.IsNullOrWhiteSpace(key)))
            _scheduler.SetJobTypeHeld(key, false);
        return Ok();
    }

    /// <summary>
    /// Clear the queue. The scheduled actions with a queue-cleared trigger are
    /// queued again right after it.
    /// </summary>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpPost("Clear")]
    [InitFriendly]
    public async Task<ActionResult> Clear()
    {
        await _queueHandler.Clear();
        return Ok();
    }

    /// <summary>
    /// Get the current items in the queue, in the order they will be processed,
    /// assuming they don't become blocked or a higher priority job is scheduled
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="showScheduled">Also include jobs deferred to a future scheduled time (not yet ready to run). Hidden by default.</param>
    /// <param name="excludeBlocked">Hide jobs that can't run right now because an acquisition filter excludes them (e.g. a ban). Shown by default.</param>
    /// <returns>A full or partial representation of the queued items, depending on the page and page size used, and the remaining items in the queue.</returns>
    [Authorize("admin")]
    [HttpGet("Items")]
    public ActionResult<ListResult<Queue.QueueItem>> GetItemsInQueue(
        [FromQuery, Range(0, 1000)] int pageSize = 10,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool showScheduled = false,
        [FromQuery] bool excludeBlocked = false
    )
    {
        var offset = (page - 1) * pageSize;
        var items = _queueHandler.GetJobs(pageSize, offset, includeScheduled: showScheduled, excludeBlocked)
            .Select(ToQueueItem).ToList();
        // Total reflects exactly the categories being returned.
        var total = _queueHandler.GetExecutingJobs().Length
            + _queueHandler.WaitingCount
            + (excludeBlocked ? 0 : _queueHandler.BlockedCount)
            + (showScheduled ? _queueHandler.ScheduledCount : 0);
        return new ListResult<Queue.QueueItem>(total, items);
    }

    /// <summary>
    /// Cancel a queue item. A waiting item is removed from the queue. A running item whose job
    /// observes cancellation stays queued, marked as cancellation requested (not undoable), until
    /// the job notices; it then ends as cancelled, or as completed if it finished first, and is
    /// never retried. Cancelling an item of a chain aborts the rest of the chain.
    /// </summary>
    /// <remarks>
    /// The key goes in the query string, as queue item keys may contain slashes.
    /// </remarks>
    /// <param name="key">The key of the queue item.</param>
    /// <returns>Whether the item was removed or asked to stop.</returns>
    /// <response code="404">No waiting or running item has the key.</response>
    /// <response code="409">The item is running and its job does not observe cancellation.</response>
    [Authorize("admin")]
    [HttpPost("Items/Cancel")]
    public async Task<ActionResult<Queue.CancelResult>> CancelItem([FromQuery, Required] string key)
    {
        return await _queueHandler.Cancel(key) switch
        {
            JobCancellationResult.Removed => new Queue.CancelResult { Key = key, Result = Queue.CancelResultType.Removed },
            JobCancellationResult.CancellationRequested => new Queue.CancelResult { Key = key, Result = Queue.CancelResultType.CancellationRequested },
            JobCancellationResult.NotCancellable => Conflict("The queue item is running and cannot be cancelled."),
            _ => NotFound("No queue item has the key."),
        };
    }

    /// <summary>
    /// Remove a waiting queue item. A running item is left alone; cancel it instead. Removing an
    /// item of a chain aborts the rest of the chain.
    /// </summary>
    /// <remarks>
    /// The key goes in the query string, as queue item keys may contain slashes.
    /// </remarks>
    /// <param name="key">The key of the queue item.</param>
    /// <returns>No content.</returns>
    /// <response code="404">No waiting or running item has the key.</response>
    /// <response code="409">The item is running.</response>
    [Authorize("admin")]
    [HttpDelete("Items")]
    public async Task<ActionResult> RemoveItem([FromQuery, Required] string key)
    {
        return await _queueHandler.Remove(key) switch
        {
            JobCancellationResult.Removed => NoContent(),
            JobCancellationResult.Running => Conflict("The queue item is running; cancel it instead."),
            _ => NotFound("No queue item has the key."),
        };
    }

    internal static Queue.QueueItem ToQueueItem(QueueItem a)
    {
        return new Queue.QueueItem
        {
            Key = a.Key,
            Type = a.TypeName ?? a.JobType ?? string.Empty,
            Title = a.Title ?? string.Empty,
            Details = a.Details ?? [],
            StartTime = a.StartTime,
            IsRunning = a.Running,
            IsBlocked = a.Blocked,
            IsScheduled = a.Scheduled,
            PoolName = a.PoolName,
            RetryCount = a.RetryCount,
            ScheduledAt = a.ScheduledAt?.UtcDateTime,
            ParentKey = a.ParentKey,
            IsCancellable = a.Cancellable,
            IsCancellationRequested = a.CancellationRequested,
            Progress = a.Progress,
        };
    }

    private static Queue.PoolState ToPoolState(PoolStatus p)
    {
        return new Queue.PoolState
        {
            Name = p.Name,
            MaxWorkers = p.MaxWorkers,
            ActiveWorkers = p.ActiveWorkers,
            IdleWorkers = p.IdleWorkers,
            WaitingCount = p.WaitingCount,
            BlockedCount = p.BlockedCount,
            ScheduledCount = p.ScheduledCount,
            IsBlocked = p.IsBlocked,
            IsHeld = p.IsHeld,
            HeldTypeNames = p.HeldTypeNames,
            HandledTypeNames = p.HandledTypeNames,
            LastActiveAt = p.LastActiveAt?.UtcDateTime
        };
    }

    [HttpGet("DebugStats")]
    public ActionResult<DebugStats> GetDebugStats()
    {
        var metrics = _queueHandler.GetMetrics();
        var chains = _queueHandler.GetChains();
        return new DebugStats(GetQueue(), _queueHandler.GetAcquisitionFilterResults(), metrics, chains);
    }

    [HttpGet("AcquisitionFilters")]
    public ActionResult<Dictionary<string, string[]>> GetAcquisitionFilters()
    {
        return _queueHandler.GetAcquisitionFilterResults();
    }

    public class DebugStats(Queue queue, Dictionary<string, string[]> acquisitionFilters, QueueMetricsSnapshot metrics, IReadOnlyList<ChainInfo> chains)
    {
        /// <summary>
        /// High-level queue state: counts and currently executing items.
        /// </summary>
        public Queue Queue { get; init; } = queue;

        /// <summary>
        /// Acquisition filters that are currently active, keyed by filter class name.
        /// Each entry lists the short type names of jobs that filter is currently blocking.
        /// </summary>
        public Dictionary<string, string[]> AcquisitionFilters { get; init; } = acquisitionFilters;

        /// <summary>
        /// Live performance metrics: throughput, per-pool worker status, and per-type
        /// job counts (waiting + executing) with friendly display names and rolling averages.
        /// </summary>
        public QueueMetricsSnapshot Metrics { get; init; } = metrics;

        /// <summary>
        /// Active job chains. Each entry lists all jobs in the chain in execution order, with their
        /// current status: the outcome status for executed jobs ("Succeeded", "Failed", "Aborted",
        /// "Skipped"), "Executing" for the currently running job, and "Pending" for queued jobs.
        /// </summary>
        public IReadOnlyList<ChainInfo> Chains { get; init; } = chains;
    }
}
