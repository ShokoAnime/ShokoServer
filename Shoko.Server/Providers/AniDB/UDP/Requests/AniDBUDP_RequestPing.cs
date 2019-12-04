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

        public override void Execute(AniDBConnectionHandler handler)
        {
            AniDBUDP_Response<string> response = handler.CallAniDBUDPDirectly(BaseCommand, false, true, true);
            Response = ParseResponse(response.Code, response.Response);
            HasEexecuted = true;
        }
    }
}
