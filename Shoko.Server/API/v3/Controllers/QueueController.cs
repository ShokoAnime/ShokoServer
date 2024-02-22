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

    public QueueController(ISettingsProvider settingsProvider, QueueHandler queueHandler) : base(settingsProvider)
    {
        _queueHandler = queueHandler;
    }

    /// <summary>
    /// Get a list of all the available queues.
    /// </summary>
    /// <returns>A list of all the available queues.</returns>
    [HttpGet]
    public Queue GetQueue()
    {
        return new Queue
        {
            WaitingCount = _queueHandler.WaitingCount,
            BlockedCount = _queueHandler.BlockedCount,
            TotalCount = _queueHandler.Count,
            ThreadCount = _queueHandler.ThreadCount,
            CurrentlyExecuting = _queueHandler.GetExecutingJobs().Select(a => new Queue.QueueItem
            {
                Key = a.Key,
                Type = a.JobType,
                Description = a.Description,
                IsRunning = true
            }).ToList()
        };
    }

    /// <summary>
    /// Get all the queued and active command types across all the queues.
    /// </summary>
    /// <returns>A dictionary of all the queued and active command types across all queues, and the count for each type.</returns>
    [HttpGet("Types")]
    public async Task<ActionResult<Dictionary<string, int>>> GetTypesForItemsInAllQueues()
    {
        return await _queueHandler.GetJobCounts();
    }

    /// <summary>
    /// Start all the queues.
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
    /// Stop all the queues.
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
    /// Clear all the queues.
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
    /// Get the current items in the queue, in the order they will be processed
    /// in.
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
        // TODO this...
        return null;
    }
}
