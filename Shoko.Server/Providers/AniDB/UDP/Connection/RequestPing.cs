using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.Connection
{
    public class RequestPing : UDPRequest<Void>
    {
        protected override string BaseCommand => "PING";

        protected override UDPResponse<Void> ParseResponse(ILogger logger, UDPResponse<string> response)
        {
            var code = response.Code;
            var receivedData = response.Response;
            if (code != UDPReturnCode.PONG) throw new UnexpectedUDPResponseException(code, receivedData);
            return new UDPResponse<Void> {Code = code};
        }

        protected override void PreExecute(string sessionID)
        {
            // Don't set the session for pings
        }

        public override UDPResponse<Void> Execute(IUDPConnectionHandler handler)
        {
            var rawResponse = handler.CallAniDBUDPDirectly(BaseCommand, true, true, true);
            var factory = handler.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var logger = factory.CreateLogger(GetType());
            var response = ParseResponse(logger, rawResponse);
            return response;
        }
    }
}
