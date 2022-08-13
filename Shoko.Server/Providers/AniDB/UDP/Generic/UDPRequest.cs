using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;

namespace Shoko.Server.Providers.AniDB.UDP.Generic
{
    public abstract class UDPRequest<T> : IRequest, IRequest<UDPResponse<T>, T> where T : class
    {
        protected readonly ILogger Logger;
        protected readonly IUDPConnectionHandler Handler;
        protected string Command { get; set; } = string.Empty;
        /// <summary>
        /// Various Parameters to add to the base command
        /// </summary>
        protected abstract string BaseCommand { get; }

        protected abstract UDPResponse<T> ParseResponse(UDPResponse<string> response);

        // Muting the warning, I read up, and it's the intended result here
        // ReSharper disable once StaticMemberInGenericType
        protected static readonly Regex CommandRegex = new("[A-Za-z0-9]+ +\\S", RegexOptions.Compiled | RegexOptions.Singleline);

        protected UDPRequest(ILoggerFactory loggerFactory, IUDPConnectionHandler handler)
        {
            Logger = loggerFactory.CreateLogger(GetType());
            Handler = handler;
        }

        public virtual UDPResponse<T> Execute()
        {
            Command = BaseCommand.Trim();
            if (string.IsNullOrEmpty(Handler.SessionID) && !Handler.Login()) throw new NotLoggedInException();
            PreExecute(Handler.SessionID);
            var rawResponse = Handler.CallAniDBUDP(Command);
            var response = ParseResponse(rawResponse);
            PostExecute(Handler.SessionID, response);
            return response;
        }

        protected virtual void PreExecute(string sessionID)
        {
            if (CommandRegex.IsMatch(Command))
                Command += $"&s={sessionID}";
            else
                Command += $" s={sessionID}";
        }

        protected virtual void PostExecute(string sessionID, UDPResponse<T> response)
        {
        }

        object IRequest.Execute() => Execute();
    }
}
