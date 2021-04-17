using System.Text.RegularExpressions;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;

namespace Shoko.Server.Providers.AniDB.UDP.Generic
{
    public abstract class UDPBaseRequest<T> where T : class
    {
        protected string Command { get; set; } = string.Empty;
        /// <summary>
        /// Various Parameters to add to the base command
        /// </summary>
        protected abstract string BaseCommand { get; }

        protected abstract UDPBaseResponse<T> ParseResponse(UDPReturnCode code, string receivedData);

        protected static readonly Regex CommandRegex = new("[A-Za-z0-9]+ +\\S", RegexOptions.Compiled | RegexOptions.Singleline);

        public virtual UDPBaseResponse<T> Execute(AniDBUDPConnectionHandler handler)
        {
            Command = BaseCommand.Trim();
            if (string.IsNullOrEmpty(handler.SessionID) && !handler.Login()) throw new NotLoggedInException();
            PreExecute(handler.SessionID);
            UDPBaseResponse<string> rawResponse = handler.CallAniDBUDP(Command);
            var response = ParseResponse(rawResponse.Code, rawResponse.Response);
            PostExecute(handler.SessionID, response);
            return response;
        }

        protected virtual void PreExecute(string sessionID)
        {
            if (CommandRegex.IsMatch(Command))
                Command += $"&s={sessionID}";
            else
                Command += $" s={sessionID}";
        }

        protected virtual void PostExecute(string sessionID, UDPBaseResponse<T> response)
        {
        }
    }
}
