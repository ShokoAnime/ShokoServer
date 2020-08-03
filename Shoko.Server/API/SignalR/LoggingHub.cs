using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Shoko.Server.API.SignalR
{
    public class LoggingHub : Hub
    {
        private readonly LoggingEmitter _loggingEmitter;
        public LoggingHub(LoggingEmitter emitter)
        {
            _loggingEmitter = emitter;
        }

        public override async Task OnConnectedAsync()
        {
            if ((_loggingEmitter.Target?.MaxLogsCount ?? 0) <= 0) return;
            await Clients.Caller.SendAsync("GetBacklog", _loggingEmitter.Target.Logs.ToArray());
        }
    }
}