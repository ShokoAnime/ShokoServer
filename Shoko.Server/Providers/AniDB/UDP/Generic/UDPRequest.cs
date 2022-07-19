using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;

namespace Shoko.Server.Providers.AniDB.UDP.Generic
{
    public abstract class UDPRequest<T> where T : class
    {
        protected string Command { get; set; } = string.Empty;
        /// <summary>
        /// Various Parameters to add to the base command
        /// </summary>
        protected abstract string BaseCommand { get; }

        protected abstract UDPResponse<T> ParseResponse(ILogger logger, UDPResponse<string> response);

        // Muting the warning, I read up, and it's the intended result here
        // ReSharper disable once StaticMemberInGenericType
        protected static readonly Regex CommandRegex = new("[A-Za-z0-9]+ +\\S", RegexOptions.Compiled | RegexOptions.Singleline);

        public virtual UDPResponse<T> Execute(IUDPConnectionHandler handler)
        {
            Command = BaseCommand.Trim();
            if (string.IsNullOrEmpty(handler.SessionID) && !handler.Login()) throw new NotLoggedInException();
            PreExecute(handler.SessionID);
            var rawResponse = handler.CallAniDBUDP(Command);
            var factory = handler.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var logger = factory.CreateLogger(GetType());
            var response = ParseResponse(logger, rawResponse);
            PostExecute(logger, handler.SessionID, response);
            return response;
        }

        protected virtual void PreExecute(string sessionID)
        {
            if (CommandRegex.IsMatch(Command))
                Command += $"&s={sessionID}";
            else
                Command += $" s={sessionID}";
        }

        protected virtual void PostExecute(ILogger logger, string sessionID, UDPResponse<T> response)
        {
        }
    }
}
