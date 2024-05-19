using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;

namespace Shoko.Server.Providers.AniDB.UDP.User;

/// <summary>
/// Get IDs of pending notifications and unread messages
/// </summary>
public class RequestGetNotifyList : UDPRequest<IList<ResponseNotifyId>>
{

    protected override string BaseCommand => "NOTIFYLIST";

    protected override UDPResponse<IList<ResponseNotifyId>> ParseResponse(UDPResponse<string> response)
    {
        var code = response.Code;
        if (code != UDPReturnCode.NOTIFYLIST)
        {
            throw new UnexpectedUDPResponseException(code, response.Response, Command);
        }

        var receivedData = response.Response;

        if (receivedData.Length == 0)
        {
            return new UDPResponse<IList<ResponseNotifyId>>
            {
                Code = code,
                Response = System.Array.Empty<ResponseNotifyId>()
            };
        }

        string[] lines;
        if (receivedData.Contains('\n'))
        {
            lines = receivedData.Split('\n');
        }
        else
        {
            lines = new[] { receivedData };
        }

        var notifications = new List<ResponseNotifyId>();
        foreach (var line in lines)
        {
            // {str type}|{int4 id}
            /*
                type = M for message, N for notification
                id = ID of the message or notification
            */
            var parts = line.Split("|");

            if (parts.Length != 2)
            {
                throw new UnexpectedUDPResponseException("Incorrect Number of Parts Returned", code, receivedData, Command);
            }

            if (!int.TryParse(parts[1], out var id))
            {
                throw new UnexpectedUDPResponseException("ID was not an int", code, receivedData, Command);
            }

            var typeStr = parts[0];
            if (!typeStr.Equals('M') || !typeStr.Equals('N'))
            {
                throw new UnexpectedUDPResponseException("Type was not M or N", code, receivedData, Command);
            }

            notifications.Add(
                new ResponseNotifyId
                {
                    Message = typeStr.Equals('M'),
                    ID = id
                }
            );
        }

        return new UDPResponse<IList<ResponseNotifyId>>
        {
            Code = code,
            Response = notifications
        };
    }

    public RequestGetNotifyList(ILoggerFactory loggerFactory, IUDPConnectionHandler handler) : base(loggerFactory, handler)
    {
    }
}
