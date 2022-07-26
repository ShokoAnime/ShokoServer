using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;

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

        protected virtual HttpBaseResponse<T> ParseResponse(IServiceProvider serviceProvider, HttpBaseResponse<string> receivedData)
        {
            var factory = serviceProvider.GetService<ILoggerFactory>();
            var logger = factory.CreateLogger(GetType());
            return ParseResponse(logger, receivedData);
        }

        public virtual HttpBaseResponse<T> Execute(IHttpConnectionHandler handler)
        {
            Command = BaseCommand.Trim();
            var rawResponse = handler.GetHttp(Command);
            var response = ParseResponse(handler.ServiceProvider, rawResponse);
            PostExecute(handler.ServiceProvider, response);
            return response;
        }

        protected virtual void PostExecute(ILogger logger, HttpBaseResponse<T> response)
        {
        }

        protected virtual void PostExecute(IServiceProvider provider, HttpBaseResponse<T> response)
        {
            var factory = provider.GetService<ILoggerFactory>();
            var logger = factory.CreateLogger(GetType());
            PostExecute(logger, response);
        }
    }
}
