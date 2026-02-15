using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;

namespace Shoko.Server.Providers.AniDB.UDP.Generic;

public abstract class UDPRequest<T> : IRequest, IRequest<UDPResponse<T>, T> where T : class
{
    protected readonly ILogger Logger;
    protected readonly IUDPConnectionHandler Handler;
    protected string Command { get; set; } = string.Empty;

    /// <summary>
    /// Various Parameters to add to the base command
    /// </summary>
    protected abstract string BaseCommand { get; }

    protected abstract UDPResponse<T> ParseResponse(UDPResponse<string> response);

    // Muting the warning, I read up, and it's the intended result here
    // ReSharper disable once StaticMemberInGenericType
    private static readonly Regex s_commandRegex =
        new("[A-Za-z0-9]+ +\\S", RegexOptions.Compiled | RegexOptions.Singleline);

    protected UDPRequest(ILoggerFactory loggerFactory, IUDPConnectionHandler handler)
    {
        Logger = loggerFactory.CreateLogger(GetType());
        Handler = handler;
    }

    public virtual UDPResponse<T> Send()
    {
        Command = BaseCommand.Trim();
        if (string.IsNullOrEmpty(Handler.SessionID) && !Handler.Login())
        {
            throw new NotLoggedInException();
        }

        PreExecute(Handler.SessionID);
        var rawResponse = Handler.Send(Command);
        var response = ParseResponse(rawResponse);
        var parsedResponse = ParseResponse(response);
        PostExecute(Handler.SessionID, parsedResponse);
        return parsedResponse;
    }

    protected virtual void PreExecute(string sessionID)
    {
        if (s_commandRegex.IsMatch(Command))
        {
            Command += $"&s={sessionID}";
        }
        else
        {
            Command += $" s={sessionID}";
        }
    }

    protected virtual void PostExecute(string sessionID, UDPResponse<T> response)
    {
    }

    protected virtual UDPResponse<string> ParseResponse(string response, bool returnFullResponse = false)
    {
        // there should be 2 newline characters in each response
        // the first is after the command .e.g "220 FILE"
        // the second is at the end of the data
        var decodedParts = response.Split('\n');
        var truncated = (typeof(T) != typeof(Void) && response.Count(a => a == '\n') < 2) || !response.EndsWith('\n');
        // things like group status have more than 2 lines, so rebuild the data from the original string. split, remove empty, and skip the code
        var decodedResponse = string.Join('\n',
            response.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Skip(1));

        // If the parts don't have at least 2 items, then we don't have a valid response
        // parts[0] => 200 FILE
        // parts[1] => Response
        // parts[2] => empty, since we ended with a newline
        if (decodedParts.Length < 2 && !returnFullResponse)
        {
            throw new UnexpectedUDPResponseException(response, Command);
        }

        var firstLineParts = decodedParts[0].Split(' ', 2);
        // If we don't have 2 parts of the first line, then it's not in the expected
        // 200 FILE
        // Format
        if (firstLineParts.Length != 2)
        {
            throw new UnexpectedUDPResponseException(response, Command);
        }

        // Can't parse the code
        if (!int.TryParse(firstLineParts[0], out var code))
        {
            throw new UnexpectedUDPResponseException(response, Command);
        }

        var status = (UDPReturnCode)code;

        // if we get banned pause the command processor for a while, so we don't make the ban worse
        Handler.IsBanned = status == UDPReturnCode.BANNED;

        // if banned, then throw the ban exception. There will be no data in the response
        if (Handler.IsBanned)
        {
            throw new AniDBBannedException
            {
                BanType = UpdateType.UDPBan,
                BanExpires = Handler.BanTime?.AddHours(Handler.BanTimerResetLength)
            };
        }

        switch (status)
        {
            // 505 ILLEGAL INPUT OR ACCESS DENIED
            // reset login status to start again
            case UDPReturnCode.ILLEGAL_INPUT_OR_ACCESS_DENIED:
                Handler.IsInvalidSession = true;
                throw new NotLoggedInException();
            // 600 INTERNAL SERVER ERROR
            // 601 ANIDB OUT OF SERVICE - TRY AGAIN LATER
            // 602 SERVER BUSY - TRY AGAIN LATER
            // 604 TIMEOUT - DELAY AND RESUBMIT
            case UDPReturnCode.INTERNAL_SERVER_ERROR:
            case UDPReturnCode.ANIDB_OUT_OF_SERVICE:
            case UDPReturnCode.SERVER_BUSY:
            case UDPReturnCode.TIMEOUT_DELAY_AND_RESUBMIT:
            {
                var errorMessage = $"{(int)status} {status}";
                Logger.LogTrace("Waiting. AniDB returned {StatusCode} {Status}", (int)status, status);
                Handler.StartBackoffTimer(300, errorMessage);
                break;
            }
            // 506 INVALID SESSION
            // 598 UNKNOWN COMMAND
            case UDPReturnCode.INVALID_SESSION:
            case UDPReturnCode.UNKNOWN_COMMAND:
                if (status == UDPReturnCode.UNKNOWN_COMMAND)
                {
                    Logger.LogWarning("AniDB returned \"UNKNOWN COMMAND\" which likely means your session has expired." +
                                      "Please check your router's settings for how long it keeps track of active connections and adjust UDPPingFrequency in the settings accordingly");
                }
                Handler.ClearSession();
                throw new NotLoggedInException();
        }

        if (truncated)
        {
            Logger.LogTrace(
                "AniDB Response Truncated: Expected a response line, but none was returned:\n{DecodedString}",
                response);
        }

        if (returnFullResponse)
        {
            return new UDPResponse<string> { Code = status, Response = response };
        }

        return new UDPResponse<string> { Code = status, Response = decodedResponse };
    }

    object IRequest.Send()
    {
        return Send();
    }
}
