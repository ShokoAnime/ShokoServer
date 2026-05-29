using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Web.Attributes;
using Shoko.QueueProcessor;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Settings;

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

    public QueueController(ISettingsProvider settingsProvider, QueueHandler queueHandler) : base(settingsProvider)
    {
        _queueHandler = queueHandler;
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
    /// Clear the queue and reschedule recurring jobs.
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
    /// <param name="showAll">Show all queue items, even those skipped from processing under the current conditions.</param>
    /// <returns>A full or partial representation of the queued items, depending on the page and page size used, and the remaining items in the queue.</returns>
    [Authorize("admin")]
    [HttpGet("Items")]
    public ActionResult<ListResult<Queue.QueueItem>> GetItemsInQueue(
        [FromQuery, Range(0, 1000)] int pageSize = 10,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool showAll = false
    )
    {
        var offset = (page - 1) * pageSize;
        var items = _queueHandler.GetJobs(pageSize, offset, !showAll).Select(ToQueueItem).ToList();
        var total = showAll ? _queueHandler.TotalCount : _queueHandler.WaitingCount + _queueHandler.GetExecutingJobs().Length;
        return new ListResult<Queue.QueueItem>(total, items);
    }

    private static Queue.QueueItem ToQueueItem(QueueItem a)
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
            PoolName = a.PoolName,
            RetryCount = a.RetryCount
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
            IsBlocked = p.IsBlocked,
            HandledTypeNames = p.HandledTypeNames,
            LastActiveAt = p.LastActiveAt?.UtcDateTime
        };
    }

    [HttpGet("DebugStats")]
    public ActionResult<DebugStats> GetDebugStats()
    {
        var metrics = _queueHandler.GetMetrics();
        return new DebugStats(GetQueue(), _queueHandler.GetAcquisitionFilterResults(), metrics);
    }

    [HttpGet("AcquisitionFilters")]
    public ActionResult<Dictionary<string, string[]>> GetAcquisitionFilters()
    {
        return _queueHandler.GetAcquisitionFilterResults();
    }

    public class DebugStats(Queue queue, Dictionary<string, string[]> acquisitionFilters, QueueMetricsSnapshot metrics)
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
    }
}
