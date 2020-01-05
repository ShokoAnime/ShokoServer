using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Responses;

namespace Shoko.Server.Providers.AniDB.UDP.Requests
{
    public abstract class AniDBUDP_BaseRequest<T> where T : class
    {
        protected string Command { get; set; } = string.Empty;
        /// <summary>
        /// Various Parameters to add to the base command
        /// </summary>
        protected abstract string BaseCommand { get; }

        protected abstract AniDBUDP_Response<T> ParseResponse(AniDBUDPReturnCode code, string receivedData);

        public virtual AniDBUDP_Response<T> Execute(AniDBConnectionHandler handler)
        {
            Command = BaseCommand;
            PreExecute(handler.SessionID);
            AniDBUDP_Response<string> rawResponse = handler.CallAniDBUDP(Command);
            var response = ParseResponse(rawResponse.Code, rawResponse.Response);
            PostExecute(handler.SessionID, response);
            return response;
        }

        protected virtual void PreExecute(string sessionID)
        {
            Command += $"&s={sessionID}";
        }

        protected virtual void PostExecute(string sessionID, AniDBUDP_Response<T> response)
        {
        }
    }
}
