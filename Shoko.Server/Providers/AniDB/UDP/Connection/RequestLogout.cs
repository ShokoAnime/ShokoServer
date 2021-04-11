using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.Connection
{
    public class RequestLogout : UDPBaseRequest<Void>
    {
        // Normally we would override Execute, but we are always logged in here, and Login() just returns if we are
        protected override string BaseCommand => "LOGOUT";
        protected override UDPBaseResponse<Void> ParseResponse(UDPReturnCode code, string receivedData)
        {
            return new UDPBaseResponse<Void> {Code = code};
        }
    }
}
