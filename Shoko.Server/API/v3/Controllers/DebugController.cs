using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Quartz;
using Shoko.Server.API.Annotations;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Test;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

#if DEBUG

/// <summary>
/// A controller with endpoints that should only be used while debugging.
/// Not for general use.
/// </summary>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize("admin")]
public class DebugController : BaseController
{
    private readonly ILogger<DebugController> _logger;

    private readonly IUDPConnectionHandler _udpHandler;
    private readonly ISchedulerFactory _schedulerFactory;

    public DebugController(ILogger<DebugController> logger, IUDPConnectionHandler udpHandler, ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory) : base(settingsProvider)
    {
        _logger = logger;
        _udpHandler = udpHandler;
        _schedulerFactory = schedulerFactory;
    }

    /// <summary>
    /// Schedule {<paramref name="count"/>} jobs that just wait for 60 seconds
    /// </summary>
    /// <param name="count"></param>
    /// <param name="seconds"></param>
    [HttpGet("ScheduleJobs/Delay/{count}")]
    public async Task<ActionResult> ScheduleTestJobs(int count, [FromQuery] int seconds = 60)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        for (var i = 0; i < count; i++)
            await scheduler.StartJob<TestDelayJob>(t => (t.DelaySeconds, t.Offset) = (seconds, i), prioritize: true).ConfigureAwait(false);

        return Ok();
    }

    /// <summary>
    /// Schedule {<paramref name="count"/>} jobs that just error
    /// </summary>
    /// <param name="count"></param>
    [HttpGet("ScheduleJobs/Error/{count}")]
    public async Task<ActionResult> ScheduleTestErrorJobs(int count)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        for (var i = 0; i < count; i++)
            await scheduler.StartJob<TestErrorJob>(t => t.Offset = i, prioritize: true).ConfigureAwait(false);

        return Ok();
    }

    /// <summary>
    /// Fetch a specific AniDB message by the provided ID.
    /// </summary>
    /// <param name="id">Message ID</param>
    /// <returns></returns>
    [HttpGet("FetchAniDBMessage/{id}")]
    public async Task<ActionResult> FetchAniDBMessage(int id)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<GetAniDBMessageJob>(r => r.MessageID = id, prioritize: true).ConfigureAwait(false);
        return Ok();
    }

    /// <summary>
    /// Call the AniDB UDP API using the 
    /// </summary>
    /// <remarks>
    /// Most of the code here is just copy-pasted from the UDPRequest class, and
    /// afterwards modified to fit the new request/response models.
    /// </remarks>
    /// <param name="request">The AniDB UDP Request to make.</param>
    /// <returns>An AniDB UDP Response.</returns>
    [HttpPost("AniDB/UDP/Call")]
    public AnidbUdpResponse CallAniDB([FromBody] AnidbUdpRequest request)
    {
        try
        {
            var token = HttpContext.RequestAborted;
            _logger.LogDebug("Got command {Command}", request.Command);
            if (request.NeedAuth)
            {
                if (string.IsNullOrEmpty(_udpHandler.SessionID) && !_udpHandler.Login())
                    return new() { Code = UDPReturnCode.NOT_LOGGED_IN };
                request.Payload.Add("s", _udpHandler.SessionID);
            }

            var fullResponse = request.Unsafe ?
                _udpHandler.SendDirectly(request.Command, isPing: request.IsPing, isLogout: request.IsLogout) :
                _udpHandler.Send(request.Command);
            var decodedParts = fullResponse.Split('\n');
            var decodedResponse = string.Join('\n',
                fullResponse.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Skip(1));
            _logger.LogDebug("Got response {Response}", fullResponse);

            if (decodedParts.Length < 2)
                return new() { Response = fullResponse };

            var firstLineParts = decodedParts[0].Split(' ', 2);
            if (firstLineParts.Length != 2)
                return new() { Response = fullResponse };

            if (!int.TryParse(firstLineParts[0], out var code))
                return new() { Response = fullResponse };

            var returnCode = (UDPReturnCode)code;
            _udpHandler.IsBanned = returnCode == UDPReturnCode.BANNED;
            if (_udpHandler.IsBanned)
                return new() { Code = UDPReturnCode.BANNED };

            switch (returnCode)
            {
                case UDPReturnCode.INVALID_SESSION:
                case UDPReturnCode.ILLEGAL_INPUT_OR_ACCESS_DENIED:
                {
                    _udpHandler.IsInvalidSession = true;
                    return new() { Code = UDPReturnCode.NOT_LOGGED_IN };
                }
                case UDPReturnCode.INTERNAL_SERVER_ERROR:
                case UDPReturnCode.ANIDB_OUT_OF_SERVICE:
                case UDPReturnCode.SERVER_BUSY:
                case UDPReturnCode.TIMEOUT_DELAY_AND_RESUBMIT:
                {
                    var errorMessage = $"{(int)returnCode} {returnCode}";
                    _logger.LogTrace("Waiting. AniDB returned {StatusCode} {Status}", (int)returnCode, returnCode);
                    _udpHandler.StartBackoffTimer(300, errorMessage);
                    break;
                }
                case UDPReturnCode.UNKNOWN_COMMAND:
                {
                    return new() { Code = returnCode, Response = fullResponse };
                }
            }

            return new()
            {
                Code = returnCode,
                Response = request.FullResponse ? fullResponse : decodedResponse,
            };
        }
        // The UDP handler might still throw any of these errors, so catch them
        // here.
        catch (AniDBBannedException)
        {
            return new() { Code = UDPReturnCode.BANNED };
        }
        catch (NotLoggedInException)
        {
            return new() { Code = UDPReturnCode.NOT_LOGGED_IN };
        }
    }

    /// <summary>
    /// An abstraction for an AniDB UDP API Request.
    /// </summary>
    public class AnidbUdpRequest
    {
        /// <summary>
        /// The action to run.
        /// </summary>
        [Required]
        [MinLength(1)]
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Run the action without checking if we're banned and what-not.
        /// </summary>
        public bool Unsafe { get; set; } = false;

        /// <summary>
        /// Extra payload to use with the action.
        /// </summary>
        public Dictionary<string, object?> Payload { get; set; } = [];

        /// <summary>
        /// The computed command for the action and payload.
        /// </summary>
        [JsonIgnore]
        public string Command
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Action))
                    return string.Empty;

                return Payload.Count == 0 ? Action.ToUpperInvariant().Trim() : $"{Action.ToUpperInvariant()} {QueryString}".Trim();
            }
        }

        /// <summary>
        /// Indicates that this request is a ping request.
        /// </summary>
        [JsonIgnore]
        public bool IsPing
        {
            get
            {
                return string.Equals(Action, "PING", StringComparison.InvariantCultureIgnoreCase) ||
                    string.Equals(Action, "UPTIME", StringComparison.InvariantCultureIgnoreCase) ||
                    string.Equals(Action, "LOGIN", StringComparison.InvariantCultureIgnoreCase);
            }
        }

        /// <summary>
        /// Indicates that this request is a ping request.
        /// </summary>
        [JsonIgnore]
        public bool IsLogout
        {
            get
            {
                return string.Equals(Action, "LOGOUT", StringComparison.InvariantCultureIgnoreCase);
            }
        }

        /// <summary>
        /// Indicates the request needs authentication.
        /// </summary>
        [JsonIgnore]
        public bool NeedAuth
        {
            get
            {
                return !IsPing && !Payload.ContainsKey("s");
            }
        }

        private bool? _fullResponse = null;

        /// <summary>
        /// Indicates that we would want the whole response, and not just the
        /// decoded response.
        /// </summary>
        [DefaultValue(false)]
        public bool FullResponse
        {
            get
            {
                return _fullResponse ?? string.Equals(Action, "LOGIN", StringComparison.InvariantCultureIgnoreCase);
            }
            set
            {
                _fullResponse = value;
            }
        }

        /// <summary>
        /// The computed query string for the payload to use in the command.
        /// </summary>
        [JsonIgnore]
        private string QueryString
        {
            get
            {
                if (Payload.Count == 0)
                    return string.Empty;

                var queryString = HttpUtility.ParseQueryString(string.Empty);
                foreach (var (key, value) in Payload)
                {
                    if (value == null)
                        continue;

                    switch (value)
                    {
                        case string text:
                            if (string.IsNullOrWhiteSpace(text) || string.Equals(text, "null", StringComparison.InvariantCultureIgnoreCase))
                                continue;
                            if (string.Equals(text, "true", StringComparison.InvariantCultureIgnoreCase))
                                queryString[key] = "1";
                            else if (string.Equals(text, "false", StringComparison.InvariantCultureIgnoreCase))
                                queryString[key] = "0";
                            else
                                queryString[key] = text;
                            break;
                        case bool boolean:
                            queryString[key] = boolean ? "1" : "0";
                            break;
                        default:
                            queryString[key] = value.ToString();
                            break;
                    }
                }

                return queryString.ToString()!;
            }
        }
    }

    /// <summary>
    /// A response from the AniDB UDP API.
    /// </summary>
    public class AnidbUdpResponse
    {
        /// <summary>
        /// The UDP return code for the request.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public UDPReturnCode Code { get; set; } = UDPReturnCode.UNKNOWN_COMMAND;

        /// <summary>
        /// The response body.
        /// </summary>
        public string Response { get; set; } = string.Empty;
    }
}

#endif
