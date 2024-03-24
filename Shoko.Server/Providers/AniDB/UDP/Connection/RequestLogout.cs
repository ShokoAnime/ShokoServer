using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.Connection;

public class RequestLogout : UDPRequest<Void>
{
    // Normally we would override Execute, but we are always logged in here, and Login() just returns if we are
    protected override string BaseCommand => "LOGOUT";

    public override UDPResponse<Void> Send()
    {
        Command = BaseCommand.Trim();
        if (string.IsNullOrEmpty(Handler.SessionID) || Handler.IsBanned || Handler.IsInvalidSession)
        {
            return new UDPResponse<Void>
            {
                Code = UDPReturnCode.LOGGED_OUT
            };
        }

        PreExecute(Handler.SessionID);
        var rawResponse = Handler.SendDirectly(Command, resetPingTimer: false, resetLogoutTimer: false).Result;
        var response = ParseResponse(rawResponse);
        var parsedResponse = ParseResponse(response);
        return parsedResponse;
    }

    protected override UDPResponse<Void> ParseResponse(UDPResponse<string> response)
    {
        var code = response.Code;
        return new UDPResponse<Void> { Code = code };
    }

    public RequestLogout(ILoggerFactory loggerFactory, IUDPConnectionHandler handler) : base(loggerFactory, handler)
    {
    }
}
