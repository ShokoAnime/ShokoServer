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
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

// ReSharper disable once UnusedMember.Global
/// <summary>
/// The queue controller. Used for controlling the queues.
/// </summary>
[ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
[DatabaseBlockedExempt]
[InitFriendly]
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
                StartTime = a.StartTime
            }).OrderBy(a => a.StartTime).ToList()
        };
    }

    /// <summary>
    /// Get all the queued and active command types across the queue.
    /// </summary>
    /// <returns>A dictionary of all the queued and active command types, and the count for each type.</returns>
    [HttpGet("Types")]
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
        // simplified from (page - 1) * pageSize + pageSize
        if (page * pageSize <= _settingsProvider.GetSettings().Quartz.WaitingCacheSize && showAll)
        {
            var results = _queueHandler.GetExecutingJobs().Skip(offset).Take(pageSize).ToList();
            if (pageSize - results.Count > 0)
                results.AddRange(_queueHandler.GetWaitingJobs().Skip(offset - results.Count).Take(pageSize - results.Count));
            return new ListResult<Queue.QueueItem>(total, results.Select(a =>
                new Queue.QueueItem
                {
                    Key = a.Key,
                    Type = a.JobType,
                    Title = a.Title,
                    Details = a.Details,
                    IsRunning = a.Running,
                    IsBlocked = a.Blocked
                }).ToList());
        }

        var result = (await _queueHandler.GetJobs(pageSize, offset, !showAll))
            .Select(a => new Queue.QueueItem
        {
            Key = a.Key,
            Type = a.JobType,
            Title = a.Title,
            Details = a.Details,
            IsRunning = a.Running,
            IsBlocked = a.Blocked,
            StartTime = a.StartTime
        }).ToList();
        return new ListResult<Queue.QueueItem>(total, result);
    }

    [HttpGet("DebugStats")]
    public ActionResult GetDebugState()
    {
        return new OkObjectResult((Queue: GetQueue(), TypeFilters: _queueHandler.GetTypes(), AcquisitionFilters: _queueHandler.GetAcquisitionFilterResults()));
    }
}
