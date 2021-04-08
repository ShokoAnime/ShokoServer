using System.Text.RegularExpressions;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;

namespace Shoko.Server.Providers.AniDB.Http
{
    public abstract class HttpBaseRequest<T> where T : class
    {
        protected string Command { get; set; } = string.Empty;
        /// <summary>
        /// Various Parameters to add to the base command
        /// </summary>
        protected abstract string BaseCommand { get; }

        protected abstract HttpBaseResponse<T> ParseResponse(HttpBaseResponse<string> receivedData);

        public HttpBaseResponse<T> Execute() => Execute(AniDBHttpConnectionHandler.Instance);

        public virtual HttpBaseResponse<T> Execute(AniDBHttpConnectionHandler handler)
        {
            Command = BaseCommand.Trim();
            HttpBaseResponse<string> rawResponse = handler.GetHttp(Command);
            var response = ParseResponse(rawResponse);
            PostExecute(response);
            return response;
        }

        protected virtual void PostExecute(HttpBaseResponse<T> response)
        {
        }
    }
}
