using System;
using Shoko.Server.Providers.AniDB.MyList.Exceptions;

namespace Shoko.Server.Providers.AniDB.MyList.Commands
{
    public class AniDBUDP_RequestLogin : AniDBUDP_BaseRequest<AniDBUDP_ResponseLogin>
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public bool UseUnicode { get; set; }

        protected override string Command
        {
            get
            {
                string command = $"AUTH user={Username.Trim()}&pass={Password.Trim()}&protover=3&client=ommserver&clientver=2&comp=1";
                if (UseUnicode) command += "&enc=utf16";
                return command;
            }
        }

        protected override AniDBUDP_ResponseLogin ParseResponse(AniDBUDPReturnCode code, string receivedData)
        {
            int i = receivedData.IndexOf("LOGIN", StringComparison.Ordinal);
            if (i < 0) throw new UnexpectedAniDBResponse(code, receivedData);
            string sessionID = receivedData.Substring(0, i - 1).Trim();
            return new AniDBUDP_ResponseLogin { SessionID = sessionID };
        }
    }
}