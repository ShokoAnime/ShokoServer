using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Responses;

namespace Shoko.Server.Providers.AniDB.UDP.Requests
{
    public class RequestPing : UDPBaseRequest<Void>
    {
        protected override string BaseCommand => "PING";
        protected override UDPBaseResponse<Void> ParseResponse(AniDBUDPReturnCode code, string receivedData)
        {
            if (code != AniDBUDPReturnCode.PONG) throw new UnexpectedAniDBResponseException(code, receivedData);
            return new UDPBaseResponse<Void> {Code = code};
        }

        protected override void PreExecute(string sessionID)
        {
            // Don't set the session for pings
        }

        public override UDPBaseResponse<Void> Execute(AniDBConnectionHandler handler)
        {
            UDPBaseResponse<string> rawResponse = handler.CallAniDBUDPDirectly(BaseCommand, false, true, true);
            var response = ParseResponse(rawResponse.Code, rawResponse.Response);
            return response;
        }
    }
}
