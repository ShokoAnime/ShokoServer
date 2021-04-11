using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.Connection
{
    public class RequestPing : UDPBaseRequest<Void>
    {
        protected override string BaseCommand => "PING";

        protected override UDPBaseResponse<Void> ParseResponse(UDPReturnCode code, string receivedData)
        {
            if (code != UDPReturnCode.PONG) throw new UnexpectedUDPResponseException(code, receivedData);
            return new UDPBaseResponse<Void> {Code = code};
        }

        protected override void PreExecute(string sessionID)
        {
            // Don't set the session for pings
        }

        public override UDPBaseResponse<Void> Execute(AniDBUDPConnectionHandler handler)
        {
            UDPBaseResponse<string> rawResponse = handler.CallAniDBUDPDirectly(BaseCommand, false, true, true);
            var response = ParseResponse(rawResponse.Code, rawResponse.Response);
            return response;
        }
    }
}
