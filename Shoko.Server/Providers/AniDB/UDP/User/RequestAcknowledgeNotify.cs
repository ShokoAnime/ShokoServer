using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Server;

namespace Shoko.Server.Providers.AniDB.UDP.User;

/// <summary>
/// Acknowledge a notification or message
/// </summary>
public class RequestAcknowledgeNotify : UDPRequest<Void>
{
    public AniDBNotifyType Type { get; set; }
    public int ID { get; set; }

    protected override string BaseCommand => $"NOTIFYACK type={(Type == AniDBNotifyType.Message ? "M" : "N")}&id={ID}";

    protected override UDPResponse<Void> ParseResponse(UDPResponse<string> response)
    {
        var code = response.Code;
        switch (code)
        {
            case UDPReturnCode.NOTIFYACK_SUCCESSFUL_MESSAGE:
            case UDPReturnCode.NOTIFYACK_SUCCESSFUL_NOTIFIATION:
            case UDPReturnCode.NO_SUCH_MESSAGE:
            case UDPReturnCode.NO_SUCH_NOTIFY:
                return new UDPResponse<Void> { Code = code };
            default:
                throw new UnexpectedUDPResponseException(code, response.Response, Command);
        }
    }

    public RequestAcknowledgeNotify(ILoggerFactory loggerFactory, IUDPConnectionHandler handler) : base(loggerFactory, handler)
    {
    }
}
