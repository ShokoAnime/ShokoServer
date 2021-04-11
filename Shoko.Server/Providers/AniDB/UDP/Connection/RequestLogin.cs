using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.Connection
{
    public class RequestLogin : UDPBaseRequest<ResponseLogin>
    {
        public string Username { get; set; }
        public string Password { get; set; }

        protected override string BaseCommand
        {
            get
            {
                string command = $"AUTH user={Username}&pass={Password}&protover=3&client=ommserver&clientver=2&comp=1&imgserver=1&enc=utf-16";
                return command;
            }
        }

        protected override UDPBaseResponse<ResponseLogin> ParseResponse(UDPReturnCode code, string receivedData)
        {
            int i = receivedData.IndexOf("LOGIN", StringComparison.Ordinal);
            if (i < 0) throw new UnexpectedUDPResponseException(code, receivedData);
            // after response code, before login message
            string sessionID = receivedData.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(sessionID)) throw new UnexpectedUDPResponseException(code, receivedData);
            string imageServer = receivedData.Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            return new UDPBaseResponse<ResponseLogin>
            {
                Response = new ResponseLogin {SessionID = sessionID, ImageServer = imageServer}, Code = code
            };
        }

        protected override void PreExecute(string sessionID)
        {
            // Override to prevent attaching our non-existent sessionID
        }
        
        public override UDPBaseResponse<ResponseLogin> Execute(AniDBUDPConnectionHandler handler)
        {
            Command = BaseCommand;
            PreExecute(handler.SessionID);
            // LOGIN commands have special needs, so we want to handle this differently
            UDPBaseResponse<string> rawResponse = handler.CallAniDBUDPDirectly(Command, false, true, false, true);
            var response = ParseResponse(rawResponse.Code, rawResponse.Response);
            PostExecute(handler.SessionID, response);
            return response;
        }
    }
}
