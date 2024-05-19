using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.User;

/// <summary>
/// Get data about a specific notification
/// </summary>
public class RequestGetNotificationContent : UDPRequest<ResponseNotificationContent>
{
    /// <summary>
    /// Message ID
    /// </summary>
    public int ID { get; set; }

    protected override string BaseCommand => $"NOTIFYGET type=N&id={ID}";

    protected override UDPResponse<ResponseNotificationContent> ParseResponse(UDPResponse<string> response)
    {
        var code = response.Code;
        var receivedData = response.Response;

        switch (code)
        {
            case UDPReturnCode.NOTIFYGET_NOTIFY:
                {
                    // {int4 relid}|{int4 type}|{int2 count}|{int4 date}|{str relidname}|{str fids}
                    /*
                        relid is the id of the related type (not specified, but there is only the anime type)
                        type is the type of notification (0=all, 1=new, 2=group, 3=complete)
                        count is the number of events pending for this subscription
                        date is the time the notification was sent
                        relidname is the name of the related type
                        fids is a comma separated list of affected file ids
                    */
                    var parts = receivedData.Split('|').Select(a => a.Trim()).ToArray();
                    if (parts.Length != 6)
                    {
                        throw new UnexpectedUDPResponseException("Incorrect Number of Parts Returned", code, receivedData, Command);
                    }

                    if (!int.TryParse(parts[0], out var relatedTypeID))
                    {
                        throw new UnexpectedUDPResponseException("Related Type ID was not an int", code, receivedData, Command);
                    }

                    if (!int.TryParse(parts[1], out var notifType))
                    {
                        throw new UnexpectedUDPResponseException("Type was not an int", code, receivedData, Command);
                    }

                    if (notifType < 0 || notifType > 3)
                    {
                        throw new UnexpectedUDPResponseException("Type was not in 0-3 range", code, receivedData, Command);
                    }

                    if (!int.TryParse(parts[2], out var countPending))
                    {
                        throw new UnexpectedUDPResponseException("Count was not an int", code, receivedData, Command);
                    }

                    if (!int.TryParse(parts[3], out var sentTime))
                    {
                        throw new UnexpectedUDPResponseException("Date was not an int", code, receivedData, Command);
                    }
                    var sentDateTime = DateTime.UnixEpoch.AddSeconds(sentTime).ToLocalTime();

                    var relatedTypeName = parts[4];

                    var fileIdsArray = parts[5].Split(',');
                    var fileIds = new int[fileIdsArray.Length];
                    for (var i = 0; i < fileIdsArray.Length; i++)
                    {
                        if (!int.TryParse(fileIdsArray[i], out var fileID))
                        {
                            throw new UnexpectedUDPResponseException("File ID was not an int", code, receivedData, Command);
                        }

                        fileIds[i] = fileID;
                    }

                    return new UDPResponse<ResponseNotificationContent>
                    {
                        Code = code,
                        Response = new ResponseNotificationContent
                        {
                            RelatedTypeID = relatedTypeID,
                            Type = notifType,
                            PendingEvents = countPending,
                            SentTime = sentDateTime,
                            RelatedTypeName = relatedTypeName,
                            FileIDs = fileIds
                        }
                    };
                }
            case UDPReturnCode.NO_SUCH_NOTIFY:
                {
                    return new UDPResponse<ResponseNotificationContent> { Code = code, Response = null };
                }
            default:
                {
                    throw new UnexpectedUDPResponseException(code, receivedData, Command);
                }
        }
    }

    public RequestGetNotificationContent(ILoggerFactory loggerFactory, IUDPConnectionHandler handler) : base(loggerFactory, handler)
    {
    }
}
