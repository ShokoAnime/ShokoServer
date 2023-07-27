using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Server.API.Annotations;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
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

    public DebugController(ILogger<DebugController> logger, IUDPConnectionHandler udpHandler, ISettingsProvider settingsProvider) : base(settingsProvider)
    {
        _logger = logger;
        _udpHandler = udpHandler;
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
            _logger.LogDebug("Got command {Command}", request.Command);
            if (request.NeedAuth)
            {
                if (string.IsNullOrEmpty(_udpHandler.SessionID) && !_udpHandler.Login())
                    return new() { Code = UDPReturnCode.NOT_LOGGED_IN };
                request.Payload ??= new();
                request.Payload.Add("s", _udpHandler.SessionID);
            }

            var fullResponse = request.Unsafe ?
                _udpHandler.CallAniDBUDPDirectly(request.Command, isPing: request.IsPing) :
                _udpHandler.CallAniDBUDP(request.Command, isPing: request.IsPing);
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
                    _udpHandler.IsInvalidSession = true;
                    return new() { Code = UDPReturnCode.NOT_LOGGED_IN };
                case UDPReturnCode.INTERNAL_SERVER_ERROR:
                case UDPReturnCode.ANIDB_OUT_OF_SERVICE:
                case UDPReturnCode.SERVER_BUSY:
                case UDPReturnCode.TIMEOUT_DELAY_AND_RESUBMIT:
                    {
                        var errorMessage = $"{(int)returnCode} {returnCode}";
                        _logger.LogTrace("Waiting. AniDB returned {StatusCode} {Status}", (int)returnCode, returnCode);
                        _udpHandler.ExtendBanTimer(300, errorMessage);
                        break;
                    }
                case UDPReturnCode.UNKNOWN_COMMAND:
                    return new() { Code = returnCode, Response = fullResponse };
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
        public string Action = string.Empty;

        /// <summary>
        /// Run the action without checking if we're banned and what-not.
        /// </summary>
        public bool Unsafe = false;

        /// <summary>
        /// Extra payload to use with the action.
        /// </summary>
        public Dictionary<string, object?> Payload = new();

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

                if (Payload == null || Payload.Count == 0)
                    return Action.ToUpperInvariant().Trim();

                return $"{Action.ToUpperInvariant()} {QueryString}".Trim();
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
        /// Indicates the request needs authentication.
        /// </summary>
        [JsonIgnore]
        public bool NeedAuth
        {
            get
            {
                return !IsPing && (Payload == null || !Payload.ContainsKey("s"));
            }
        }

        /// <summary>
        /// Indicates that we would want the whole response, and not just the
        /// decoded response.
        /// </summary>
        [JsonIgnore]
        public bool FullResponse
        {
            get
            {
                return string.Equals(Action, "LOGIN", StringComparison.InvariantCultureIgnoreCase);
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
                if (Payload == null || Payload.Count == 0)
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
        public UDPReturnCode Code = UDPReturnCode.UNKNOWN_COMMAND;

        /// <summary>
        /// The response body.
        /// </summary>
        public string Response = string.Empty;
    }
}

#endif
