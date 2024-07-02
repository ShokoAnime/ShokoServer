using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Server;

namespace Shoko.Server.Providers.AniDB.UDP.User;

/// <summary>
/// Get data about a specific message
/// </summary>
public class RequestGetMessageContent : UDPRequest<ResponseMessageContent>
{
    private static readonly Regex BreakRegex = new(@"\<br ?\/?\>", RegexOptions.Compiled);

    /// <summary>
    /// Message ID
    /// </summary>
    public int ID { get; set; }

    protected override string BaseCommand => $"NOTIFYGET type=M&id={ID}";

    protected override UDPResponse<ResponseMessageContent> ParseResponse(UDPResponse<string> response)
    {
        var code = response.Code;
        var receivedData = response.Response;

        switch (code)
        {
            case UDPReturnCode.NOTIFYGET_MESSAGE:
                {
                    // {int4 id}|{int4 from_user_id}|{str from_user_name}|{int4 date}|{int4 type}|{str title}|{str body}
                    /*
                        id is the message identifier
                        from_user_id and from_user_name are the id and name of the user who sent the message
                        date is the time the message was sent
                        type is the type of message (0=normal msg, 1=annonymous, 2=system msg, 3=mod msg)
                        title and body are the message title and body
                    */
                    var parts = receivedData.Split('|').Select(a => a.Trim()).ToArray();
                    if (parts.Length != 7)
                    {
                        throw new UnexpectedUDPResponseException("Incorrect Number of Parts Returned", code, receivedData, Command);
                    }

                    if (!int.TryParse(parts[0], out var msgID))
                    {
                        throw new UnexpectedUDPResponseException("Message ID was not an int", code, receivedData, Command);
                    }

                    if (!int.TryParse(parts[1], out var senderID))
                    {
                        throw new UnexpectedUDPResponseException("Sender ID was not an int", code, receivedData, Command);
                    }
                    var senderName = parts[2];

                    if (!int.TryParse(parts[3], out var sentTime))
                    {
                        throw new UnexpectedUDPResponseException("Date was not an int", code, receivedData, Command);
                    }
                    var sentDateTime = DateTime.UnixEpoch.AddSeconds(sentTime).ToLocalTime();

                    if (!int.TryParse(parts[4], out var msgTypeI))
                    {
                        throw new UnexpectedUDPResponseException("Type was not an int", code, receivedData, Command);
                    }

                    if (msgTypeI < 0 || msgTypeI > 3)
                    {
                        throw new UnexpectedUDPResponseException("Type was not in 0-3 range", code, receivedData, Command);
                    }
                    var msgType = (AniDBMessageType)msgTypeI;

                    var msgTitle = parts[5].Trim();
                    var msgBody = BreakRegex.Replace(parts[6], "\n").Trim();

                    return new UDPResponse<ResponseMessageContent>
                    {
                        Code = code,
                        Response = new ResponseMessageContent
                        {
                            ID = msgID,
                            SenderID = senderID,
                            SenderName = senderName,
                            SentTime = sentDateTime,
                            Type = msgType,
                            Title = msgTitle,
                            Body = msgBody
                        }
                    };
                }
            case UDPReturnCode.NO_SUCH_MESSAGE:
                {
                    return new UDPResponse<ResponseMessageContent> { Code = code, Response = null };
                }
            default:
                {
                    throw new UnexpectedUDPResponseException(code, receivedData, Command);
                }
        }
    }

    public RequestGetMessageContent(ILoggerFactory loggerFactory, IUDPConnectionHandler handler) : base(loggerFactory, handler)
    {
    }
}
