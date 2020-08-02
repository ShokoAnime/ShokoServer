using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using NLog;
using Shoko.Server.API.SignalR.NLog;

namespace Shoko.Server.API.SignalR
{
    public class LoggingHub : Hub
    {
        private List<SignalRTarget> Targets { get; set; }
        
        public LoggingHub()
        {
            // TODO dependency injection friendly
            Targets = LogManager.Configuration?.AllTargets?.Select(a => a as SignalRTarget).Where(a => a != null)
                .ToList() ?? new List<SignalRTarget>();
            foreach (var target in Targets)
            {
                target.LogEventHandler += OnLog;
            }
        }

        protected override void Dispose(bool disposing)
        {
            foreach (var target in Targets)
            {
                target.LogEventHandler -= OnLog;
            }
        }

        public async Task OnLog(LogEventArgs e)
        {
            await Clients.All.SendAsync(e.MethodName, e.LogEvent);
        }
    }
}