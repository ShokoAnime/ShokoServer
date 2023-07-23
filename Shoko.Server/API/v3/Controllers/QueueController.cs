using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
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
    private const string NoQueueWithName = "There is no queue with the given name.";

    private static IReadOnlyList<CommandProcessor> _processors = new List<CommandProcessor>
    {
        ShokoService.CmdProcessorGeneral,
        ShokoService.CmdProcessorHasher,
        ShokoService.CmdProcessorImages,
    };

    private readonly ILogger<InitController> _logger;

    private readonly IUDPConnectionHandler _udpHandler;

    private readonly IHttpConnectionHandler _httpHandler;

    public QueueController(ILogger<InitController> logger, ISettingsProvider settingsProvider, IUDPConnectionHandler udpHandler, IHttpConnectionHandler httpHandler) : base(settingsProvider)
    {
        _logger = logger;
        _udpHandler = udpHandler;
        _httpHandler = httpHandler;
    }

    /// <summary>
    /// Get a list of all the available queues.
    /// </summary>
    /// <returns>A list of all the available queues.</returns>
    [HttpGet]
    public List<Queue> GetAllQueues()
    {
        return GetAllCommandProcessors()
            .Select(processor => new Queue(processor))
            .ToList();
    }

    /// <summary>
    /// Get all the queued and active command types across all the queues.
    /// </summary>
    /// <returns>A dictionary of all the queued and active command types across all queues, and the count for each type.</returns>
    [HttpGet("Types")]
    public ActionResult<Dictionary<CommandRequestType, int>> GetTypesForItemsInAllQueues()
    {
        return RepoFactory.CommandRequest.GetAll()
            .GroupBy(a => (CommandRequestType)a.CommandType)
            .ToDictionary(a => a.Key, a => a.Count());
    }

    /// <summary>
    /// Start all the queues.
    /// </summary>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpPost("StartAll")]
    public ActionResult StartAllQueues()
    {
        foreach (var processor in GetAllCommandProcessors())
        {
            processor.Paused = false;
        }
        return Ok();
    }

    /// <summary>
    /// Stop all the queues.
    /// </summary>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpPost("StopAll")]
    public ActionResult StopAllQueues()
    {
        foreach (var processor in GetAllCommandProcessors())
        {
            processor.Stop();
        }
        return Ok();
    }

    /// <summary>
    /// Clear all the queues.
    /// </summary>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpPost("ClearAll")]
    public ActionResult ClearAllQueues()
    {
        foreach (var processor in GetAllCommandProcessors())
        {
            processor.Clear();
        }
        return Ok();
    }

    /// <summary>
    /// Get a queue by name.
    /// </summary>
    /// <param name="queueName">The name of the queue.</param>
    /// <returns>The queue.</returns>
    [HttpGet("{queueName}")]
    public ActionResult<Queue> GetQueueByName([FromRoute] string queueName)
    {
        var processor = GetCommandProcessorByType(queueName);
        if (processor == null)
            return NotFound(NoQueueWithName);

        return new Queue(processor);
    }

    /// <summary>
    /// Start a queue by name.
    /// </summary>
    /// <param name="queueName">The name of the queue.</param>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpPost("{queueName}/Start")]
    public ActionResult StartQueueByName([FromRoute] string queueName)
    {
        var processor = GetCommandProcessorByType(queueName);
        if (processor == null)
            return NotFound(NoQueueWithName);

        processor.Paused = false;
        return Ok();
    }

    /// <summary>
    /// Stop a queue by name.
    /// </summary>
    /// <param name="queueName">The name of the queue.</param>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpPost("{queueName}/Stop")]
    public ActionResult StopQueueByName([FromRoute] string queueName)
    {
        var processor = GetCommandProcessorByType(queueName);
        if (processor == null)
            return NotFound(NoQueueWithName);

        processor.Stop();
        return Ok();
    }
    /// <summary>
    /// Clear a queue by name.
    /// </summary>
    /// <param name="queueName">The name of the queue.</param>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpPost("{queueName}/Clear")]
    public ActionResult ClearQueueByName([FromRoute] string queueName)
    {
        var processor = GetCommandProcessorByType(queueName);
        if (processor == null)
            return NotFound(NoQueueWithName);

        processor.Clear();
        return Ok();
    }

    /// <summary>
    /// Get the current items in the queue, in the order they will be processed
    /// in.
    /// </summary>
    /// <param name="queueName">The name of the queue.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="showAll">Show all queue items, even those skipped from processing under the current conditions.</param>
    /// <returns>A full or partial representation of the queued items, depending on the page and page size used, and the remaining items in the queue.</returns>
    [Authorize("admin")]
    [HttpGet("{queueName}/Items")]
    public ActionResult<ListResult<Queue.QueueItem>> GetItemsInQueueByName(
        [FromRoute] string queueName,
        [FromQuery, Range(0, 1000)] int pageSize = 10,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool showAll = false
    )
    {
        var processor = GetCommandProcessorByType(queueName);
        if (processor == null)
            return NotFound(NoQueueWithName);

        var httpBanned = _httpHandler.IsBanned;
        var udpBanned = _udpHandler.IsBanned;
        var udpUnavailable = _udpHandler.IsNetworkAvailable;
        return queueName.ToLowerInvariant() switch
        {
            "general" => RepoFactory.CommandRequest.GetNextGeneralCommandRequests(_udpHandler, _httpHandler, showAll)
                .ToListResult(queueItem => new Queue.QueueItem(processor, queueItem, httpBanned, udpBanned, udpUnavailable), pageSize, page),

            "hasher" => RepoFactory.CommandRequest.GetNextHasherCommandRequests()
                .ToListResult(queueItem => new Queue.QueueItem(processor, queueItem, httpBanned, udpBanned, udpUnavailable), pageSize, page),

            "image" => RepoFactory.CommandRequest.GetNextImagesCommandRequests()
                .ToListResult(queueItem => new Queue.QueueItem(processor, queueItem, httpBanned, udpBanned, udpUnavailable), pageSize, page),

            _ => NotFound(NoQueueWithName),
        };
    }

    /// <summary>
    /// Get all the queued and active command types across for the given queue.
    /// </summary>
    /// <param name="queueName">The name of the queue.</param>
    /// <returns>A dictionary of all the command types currently in the queue, and the count for each type.</returns>
    [Authorize("admin")]
    [HttpGet("{queueName}/Items/Types")]
    public ActionResult<Dictionary<CommandRequestType, int>> GetTypesForItemsInQueueByName([FromRoute] string queueName)
    {
        return queueName.ToLowerInvariant() switch
        {
            "general" => RepoFactory.CommandRequest.GetNextGeneralCommandRequests(_udpHandler, _httpHandler, true)
                .GroupBy(a => (CommandRequestType)a.CommandType)
                .ToDictionary(a => a.Key, a => a.Count()),

            "hasher" => RepoFactory.CommandRequest.GetNextHasherCommandRequests()
                .GroupBy(a => (CommandRequestType)a.CommandType)
                .ToDictionary(a => a.Key, a => a.Count()),

            "image" => RepoFactory.CommandRequest.GetNextImagesCommandRequests()
                .GroupBy(a => (CommandRequestType)a.CommandType)
                .ToDictionary(a => a.Key, a => a.Count()),

            _ => NotFound(NoQueueWithName),
        };
    }

    /// <summary>
    /// Get all the available command processors.
    /// </summary>
    /// <remarks>
    /// We can hopefully easily modify only this part of the code in the future,
    /// when we're upgrading the queue system.
    /// </remarks>
    /// <returns>A read-only list of the available command processors.</returns>
    [NonAction]
    private IReadOnlyList<CommandProcessor> GetAllCommandProcessors()
     => _processors;

    /// <summary>
    /// Get a command processor from the list of available processors.
    /// </summary>
    /// <remarks>
    /// We can hopefully easily modify only this part of the code in the future,
    /// when we're upgrading the queue system.
    /// </remarks>
    /// <param name="queueType">The command processor queue type.</param>
    /// <returns>The command processor if found, otherwise null.</returns>
    [NonAction]
    private CommandProcessor GetCommandProcessorByType(string queueType)
        => _processors.FirstOrDefault(processor => string.Equals(processor.QueueType, queueType, StringComparison.InvariantCultureIgnoreCase));
}
