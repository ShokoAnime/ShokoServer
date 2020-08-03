using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using NLog;
using Shoko.Server.API.SignalR.NLog;
using Shoko.Server.Commands;

namespace Shoko.Server.API.SignalR
{
    public class LoggingEmitter : IDisposable
    {
        private IHubContext<LoggingHub> Hub { get; set; }
        public SignalRTarget Target { get; set; }

        public LoggingEmitter(IHubContext<LoggingHub> hub)
        {
            Hub = hub;
            Target = LogManager.Configuration?.AllTargets?.Select(a => a as SignalRTarget).FirstOrDefault(a => a != null);
            if (Target != null)
                Target.LogEventHandler += OnLog;
        }

        public void Dispose()
        {
            if (Target != null)
                Target.LogEventHandler -= OnLog;
        }
        
        public async void OnLog(LogEvent e)
        {
            await Hub.Clients.All.SendAsync(Target.MethodName, e);
        }
    }
}
