using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

// ReSharper disable once UnusedMember.Global
/// <summary>
/// The queue controller. Used for controlling the queues.
/// </summary>
[ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
[DatabaseBlockedExempt]
public class QueueController : BaseController
{
    private readonly QueueHandler _queueHandler;
    private readonly ISettingsProvider _settingsProvider;

    public QueueController(ISettingsProvider settingsProvider, QueueHandler queueHandler) : base(settingsProvider)
    {
        _settingsProvider = settingsProvider;
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
            CurrentlyExecuting = _queueHandler.GetExecutingJobs().Select(a => new Queue.QueueItem
            {
                Key = a.Key,
                Type = a.JobType,
                Title = a.Title,
                Details = a.Details,
                IsRunning = true,
                StartTime = a.StartTime?.ToUniversalTime()
            }).OrderBy(a => a.StartTime).ToList()
        };
    }

    /// <summary>
    /// Get all the queued and active command types across the queue.
    /// </summary>
    /// <returns>A dictionary of all the queued and active command types, and the count for each type.</returns>
    [HttpGet("Types")]
    [InitFriendly]
    public async Task<ActionResult<Dictionary<string, int>>> GetTypesForItemsInAllQueues()
    {
        return await _queueHandler.GetJobCounts();
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
    public async Task<ActionResult<ListResult<Queue.QueueItem>>> GetItemsInQueue(
        [FromQuery, Range(0, 1000)] int pageSize = 10,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool showAll = false
    )
    {
        var total = showAll
            ? _queueHandler.WaitingCount + _queueHandler.BlockedCount + _queueHandler.GetExecutingJobs().Length
            : _queueHandler.WaitingCount + _queueHandler.GetExecutingJobs().Length;

        var offset = (page - 1) * pageSize;
        var executing = _queueHandler.GetExecutingJobs();
        // simplified from (page - 1) * pageSize + pageSize
        if (showAll && page * pageSize <= executing.Length + _settingsProvider.GetSettings().Quartz.WaitingCacheSize)
        {
            var results = new List<QueueItem>();
            if (offset < executing.Length)
            {
                results.AddRange(executing.Skip(offset).Take(pageSize));
                offset = 0;
            }
            else offset -= executing.Length;
            if (pageSize - results.Count <= 0) return new ListResult<Queue.QueueItem>(total, results.Select(ToQueueItem).ToList());

            results.AddRange(_queueHandler.GetWaitingJobs().Skip(offset).Take(pageSize - results.Count));
            return new ListResult<Queue.QueueItem>(total, results.Select(ToQueueItem).ToList());
        }

        var result = (await _queueHandler.GetJobs(pageSize, offset, !showAll)).Select(ToQueueItem).ToList();
        return new ListResult<Queue.QueueItem>(total, result);
    }

    private static Queue.QueueItem ToQueueItem(QueueItem a)
    {
        return new Queue.QueueItem
        {
            Key = a.Key,
            Type = a.JobType,
            Title = a.Title,
            Details = a.Details,
            StartTime = a.StartTime,
            IsRunning = a.Running,
            IsBlocked = a.Blocked
        };
    }

    [HttpGet("DebugStats")]
    public ActionResult<DebugStats> GetDebugStats()
    {
        return new DebugStats(GetQueue(), _queueHandler.GetTypes(), _queueHandler.GetAcquisitionFilterResults());
    }

    [HttpGet("AcquisitionFilters")]
    public ActionResult<Dictionary<string, string[]>> GetAcquisitionFilters()
    {
        return _queueHandler.GetAcquisitionFilterResults();
    }

    public record DebugStats(Queue Queue, JobTypes TypeFilters, Dictionary<string, string[]> AcquisitionFilters);
}
