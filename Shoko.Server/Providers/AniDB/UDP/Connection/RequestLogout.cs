using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.Connection;

public class RequestLogout : UDPRequest<Void>
{
    // Normally we would override Execute, but we are always logged in here, and Login() just returns if we are
    protected override string BaseCommand => "LOGOUT";

    public override async Task<UDPResponse<Void>> SendAsync(CancellationToken cancellationToken = default)
    {
        Command = BaseCommand.Trim();
        if (string.IsNullOrEmpty(Handler.SessionID) || Handler.BanState.IsBanned || Handler.IsInvalidSession)
        {
            return new UDPResponse<Void>
            {
                Code = UDPReturnCode.LOGGED_OUT
            };
        }

        PreExecute(Handler.SessionID);
        var rawResponse = await Handler.SendDirectlyAsync(Command, isLogout: true, cancellationToken: cancellationToken);
        var response = ParseResponse(rawResponse);
        var parsedResponse = ParseResponse(response);
        return parsedResponse;
    }

    protected internal override UDPResponse<Void> ParseResponse(UDPResponse<string> response)
    {
        var code = response.Code;
        return new UDPResponse<Void> { Code = code };
    }

    public RequestLogout(ILoggerFactory loggerFactory, IAniDbUdpRequestChannel handler) : base(loggerFactory, handler)
    {
    }
}
