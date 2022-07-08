using Microsoft.Extensions.Logging;

namespace Shoko.Server.Providers.AniDB.Http
{
    public abstract class HttpBaseRequest<T> where T : class
    {
        protected string Command { get; set; } = string.Empty;
        /// <summary>
        /// Various Parameters to add to the base command
        /// </summary>
        protected abstract string BaseCommand { get; }

        protected abstract HttpBaseResponse<T> ParseResponse(ILogger logger, HttpBaseResponse<string> receivedData);

        public virtual HttpBaseResponse<T> Execute(AniDBHttpConnectionHandler handler)
        {
            Command = BaseCommand.Trim();
            var rawResponse = handler.GetHttp(Command);
            var logger = handler.LoggerFactory.CreateLogger(GetType());
            var response = ParseResponse(logger, rawResponse);
            PostExecute(logger, response);
            return response;
        }

        protected virtual void PostExecute(ILogger logger, HttpBaseResponse<T> response)
        {
        }
    }
}
