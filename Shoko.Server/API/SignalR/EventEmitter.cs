using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shoko.Server.Commands;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Shoko.Server.API.SignalR
{
    public class EventEmitter
    {
        public EventEmitter(IHubContext<EventsHub> hub)
        {
            Hub = hub;
        }

        private IHubContext<EventsHub> Hub
        {
            get;
            set;
        }

        public async Task QueueStateChanged(string queue, QueueStateEventArgs e)
        {
            await Hub.Clients.All.SendAsync("QueueStateChanged", queue, e.QueueState.formatMessage());
        }

        public async Task QueueCountChanged(string queue, QueueCountEventArgs e)
        {
            await Hub.Clients.All.SendAsync("QueueCountChanged", queue, e.QueueCount);
        }
    }
}
