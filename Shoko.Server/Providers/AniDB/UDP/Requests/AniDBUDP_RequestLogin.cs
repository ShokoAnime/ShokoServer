using System;
using Shoko.Server.Providers.AniDB.MyList.Exceptions;

namespace Shoko.Server.Providers.AniDB.MyList.Commands
{
    public class AniDBUDP_RequestLogin : AniDBUDP_BaseRequest<AniDBUDP_ResponseLogin>
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public bool UseUnicode { get; set; }

        protected override string BaseCommand
        {
            get
            {
                string command = $"AUTH user={Username.Trim()}&pass={Password.Trim()}&protover=3&client=ommserver&clientver=2&comp=1";
                if (UseUnicode) command += "&enc=utf16";
                return command;
            }
        }

        protected override AniDBUDP_Response<AniDBUDP_ResponseLogin> ParseResponse(AniDBUDPReturnCode code, string receivedData)
        {
            int i = receivedData.IndexOf("LOGIN", StringComparison.Ordinal);
            if (i < 0) throw new UnexpectedAniDBResponseException(code, receivedData);
            string sessionID = receivedData.Substring(0, i - 1).Trim();
            return new AniDBUDP_Response<AniDBUDP_ResponseLogin>
            {
                Response = new AniDBUDP_ResponseLogin {SessionID = sessionID}, Code = code
            };
        }

        protected override void PreExecute(string sessionID)
        {
            // Override to prevent attaching our non-existent sessionID
        }
        
        public override void Execute(AniDBConnectionHandler handler, string sessionID)
        {
            Command = BaseCommand;
            PreExecute(sessionID);
            // LOGIN commands have special needs, so we want to handle this differently
            AniDBUDP_Response<string> response = handler.CallAniDBUDPDirectly(Command, true, true, false);
            Response = ParseResponse(response.Code, response.Response);
            PostExecute(sessionID, _response);
            HasEexecuted = true;
        }
    }
}