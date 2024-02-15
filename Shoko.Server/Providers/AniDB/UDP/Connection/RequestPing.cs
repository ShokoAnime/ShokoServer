using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.Connection;

public class RequestPing : UDPRequest<Void>
{
    protected override string BaseCommand => "PING";

    protected override UDPResponse<Void> ParseResponse(UDPResponse<string> response)
    {
        var code = response.Code;
        var receivedData = response.Response;
        if (code != UDPReturnCode.PONG)
        {
            throw new UnexpectedUDPResponseException(code, receivedData);
        }

        return new UDPResponse<Void> { Code = code };
    }

    protected override void PreExecute(string sessionID)
    {
        // Don't set the session for pings
    }

    public override async Task<UDPResponse<Void>> Send()
    {
        var rawResponse = await Handler.CallAniDBUDPDirectly(BaseCommand, true, true);
        var response = ParseResponse(rawResponse);
        var parsedResponse = ParseResponse(response);
        return parsedResponse;
    }

    public RequestPing(ILoggerFactory loggerFactory, IUDPConnectionHandler handler) : base(loggerFactory, handler)
    {
    }
}
