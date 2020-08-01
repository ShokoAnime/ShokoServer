using Microsoft.AspNetCore.SignalR;
using NLog.SignalR;

namespace Shoko.Server.API.SignalR
{
    public class LoggingHub : Hub<ILoggingHub>
    {
        public void Log(LogEvent logEvent)
        {
            Clients.Others.Log(logEvent);
        }
    }
}