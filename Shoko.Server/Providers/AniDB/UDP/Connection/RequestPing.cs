using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.Connection;

public class RequestPing : UDPRequest<Void>
{
    protected override string BaseCommand => "PING";

    protected internal override UDPResponse<Void> ParseResponse(UDPResponse<string> response)
    {
        var code = response.Code;
        var receivedData = response.Response;
        if (code != UDPReturnCode.PONG)
        {
            throw new UnexpectedUDPResponseException(code, receivedData, BaseCommand);
        }

        return new UDPResponse<Void> { Code = code };
    }

    protected override void PreExecute(string? sessionID)
    {
        // Don't set the session for pings
    }

    public override async Task<UDPResponse<Void>> SendAsync(CancellationToken cancellationToken = default)
    {
        var rawResponse = await Handler.SendDirectlyAsync(BaseCommand, isPing: true, cancellationToken: cancellationToken);
        var response = ParseResponse(rawResponse, true);
        var parsedResponse = ParseResponse(response);
        return parsedResponse;
    }

    public RequestPing(ILoggerFactory loggerFactory, IAniDbUdpRequestChannel handler) : base(loggerFactory, handler)
    {
    }
}
