using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Responses;

namespace Shoko.Server.Providers.AniDB.UDP.Requests
{
    public class AniDBUDP_RequestPing : AniDBUDP_BaseRequest<string>
    {
        protected override string BaseCommand => "PING";
        protected override AniDBUDP_Response<string> ParseResponse(AniDBUDPReturnCode code, string receivedData)
        {
            if (code != AniDBUDPReturnCode.PONG) throw new UnexpectedAniDBResponseException(code, receivedData);
            return new AniDBUDP_Response<string> {Code = code, Response = "PONG"};
        }

        protected override void PreExecute(string sessionID)
        {
            // Don't set the session for pings
        }

        public override AniDBUDP_Response<string> Execute(AniDBConnectionHandler handler)
        {
            AniDBUDP_Response<string> rawResponse = handler.CallAniDBUDPDirectly(BaseCommand, false, true, true);
            var response = ParseResponse(rawResponse.Code, rawResponse.Response);
            return response;
        }
    }
}
