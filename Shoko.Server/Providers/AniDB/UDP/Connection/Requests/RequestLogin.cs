using System;
using Shoko.Server.Providers.AniDB.UDP.Connection.Responses;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic.Requests;
using Shoko.Server.Providers.AniDB.UDP.Generic.Responses;

namespace Shoko.Server.Providers.AniDB.UDP.Connection.Requests
{
    public class RequestLogin : UDPBaseRequest<ResponseLogin>
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

        protected override UDPBaseResponse<ResponseLogin> ParseResponse(AniDBUDPReturnCode code, string receivedData)
        {
            int i = receivedData.IndexOf("LOGIN", StringComparison.Ordinal);
            if (i < 0) throw new UnexpectedAniDBResponseException(code, receivedData);
            string sessionID = receivedData.Substring(0, i - 1).Trim();
            return new UDPBaseResponse<ResponseLogin>
            {
                Response = new ResponseLogin {SessionID = sessionID}, Code = code
            };
        }

        protected override void PreExecute(string sessionID)
        {
            // Override to prevent attaching our non-existent sessionID
        }
        
        public override UDPBaseResponse<ResponseLogin> Execute(AniDBConnectionHandler handler)
        {
            Command = BaseCommand;
            PreExecute(handler.SessionID);
            // LOGIN commands have special needs, so we want to handle this differently
            UDPBaseResponse<string> rawResponse = handler.CallAniDBUDPDirectly(Command, true, true, false);
            var response = ParseResponse(rawResponse.Code, rawResponse.Response);
            PostExecute(handler.SessionID, response);
            return response;
        }
    }
}
