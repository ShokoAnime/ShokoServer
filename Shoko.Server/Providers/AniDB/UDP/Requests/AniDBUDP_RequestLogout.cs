

using Shoko.Server.Providers.AniDB.UDP.Responses;

namespace Shoko.Server.Providers.AniDB.UDP.Requests
{
    public class AniDBUDP_RequestLogout : AniDBUDP_BaseRequest<string>
    {
        // Normally we would override Execute, but we are always logged in here, and Login() just returns if we are
        protected override string BaseCommand => "LOGOUT";
        protected override AniDBUDP_Response<string> ParseResponse(AniDBUDPReturnCode code, string receivedData)
        {
            return new AniDBUDP_Response<string> {Code = code, Response = receivedData};
        }

        protected override void PreExecute(string sessionID)
        {
            // Don't attach session
        }
    }
}
