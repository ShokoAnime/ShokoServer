

using Shoko.Server.Providers.AniDB.UDP.Responses;

namespace Shoko.Server.Providers.AniDB.UDP.Requests
{
    public class RequestLogout : UDPBaseRequest<Void>
    {
        // Normally we would override Execute, but we are always logged in here, and Login() just returns if we are
        protected override string BaseCommand => "LOGOUT";
        protected override UDPBaseResponse<Void> ParseResponse(AniDBUDPReturnCode code, string receivedData)
        {
            return new UDPBaseResponse<Void> {Code = code};
        }

        protected override void PreExecute(string sessionID)
        {
            // Don't attach session
        }
    }
}
