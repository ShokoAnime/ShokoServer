using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;

namespace Shoko.Server.Providers.AniDB.UDP.User;

/// <summary>
/// Get number of pending notifications and unread messages
/// </summary>
public class RequestGetNotifyCount : UDPRequest<ResponseNotificationCount>
{
    /// <summary>
    /// Fetch online buddy count
    /// </summary>
    public bool Buddies { get; set; }

    protected override string BaseCommand => $"NOTIFY buddy={(Buddies ? '1' : '0')}";

    protected override UDPResponse<ResponseNotificationCount> ParseResponse(UDPResponse<string> response)
    {
        var code = response.Code;
        if (code != UDPReturnCode.NOTIFICATION_STATE)
        {
            throw new UnexpectedUDPResponseException(code, response.Response, Command);
        }

        var receivedData = response.Response;

        // {int4 pending_file_notifications}|{int4 number_of_unread_messages}
        // Or when Buddies is true
        // {int4 pending_file_notifications}|{int4 number_of_unread_messages}|{int4 number_of_online_buddies}
        var parts = receivedData.Split('|');
        if ((Buddies && parts.Length != 3) || (!Buddies && parts.Length != 2))
        {
            throw new UnexpectedUDPResponseException("Incorrect Number of Parts Returned", code, receivedData, Command);
        }

        if (!int.TryParse(parts[0], out var files))
        {
            throw new UnexpectedUDPResponseException("Pending File Notifications was not an int", code, receivedData, Command);
        }

        if (!int.TryParse(parts[1], out var messages))
        {
            throw new UnexpectedUDPResponseException("Unread Messages was not an int", code, receivedData, Command);
        }

        int? buddiesOnline = null;
        if (parts.Length == 3)
        {
            if (!int.TryParse(parts[2], out var value))
            {
                throw new UnexpectedUDPResponseException("Online Buddies was not an int", code, receivedData, Command);
            }
            buddiesOnline = value;
        }

        return new UDPResponse<ResponseNotificationCount>
        {
            Code = code,
            Response = new ResponseNotificationCount
            {
                Files = files,
                Messages = messages,
                BuddiesOnline = buddiesOnline
            }
        };
    }

    public RequestGetNotifyCount(ILoggerFactory loggerFactory, IUDPConnectionHandler handler) : base(loggerFactory, handler)
    {
    }
}
