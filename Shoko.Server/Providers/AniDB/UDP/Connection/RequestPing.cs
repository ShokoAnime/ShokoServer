using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.Connection
{
    public class RequestPing : UDPBaseRequest<Void>
    {
        protected override string BaseCommand => "PING";

        protected override UDPBaseResponse<Void> ParseResponse(ILogger logger, UDPBaseResponse<string> response)
        {
            var code = response.Code;
            var receivedData = response.Response;
            if (code != UDPReturnCode.PONG) throw new UnexpectedUDPResponseException(code, receivedData);
            return new UDPBaseResponse<Void> {Code = code};
        }

        protected override void PreExecute(string sessionID)
        {
            // Don't set the session for pings
        }

        public override UDPBaseResponse<Void> Execute(IUDPConnectionHandler handler)
        {
            UDPBaseResponse<string> rawResponse = handler.CallAniDBUDPDirectly(BaseCommand, false, true, true);
            var factory = handler.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var logger = factory.CreateLogger(GetType());
            var response = ParseResponse(logger, rawResponse);
            return response;
        }
    }
}
