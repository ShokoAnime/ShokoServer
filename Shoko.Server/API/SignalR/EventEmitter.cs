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
            ShokoService.CmdProcessorGeneral.OnQueueCountChangedEvent += (e) => OnQueueCountChangedEvent("general", e);
            ShokoService.CmdProcessorHasher.OnQueueCountChangedEvent += (e) => OnQueueCountChangedEvent("hasher", e);
            ShokoService.CmdProcessorImages.OnQueueCountChangedEvent += (e) => OnQueueCountChangedEvent("images", e);

            ShokoService.CmdProcessorGeneral.OnQueueStateChangedEvent += (e) => OnQueueStateChangedEvent("general", e);
            ShokoService.CmdProcessorHasher.OnQueueStateChangedEvent += (e) => OnQueueStateChangedEvent("hasher", e);
            ShokoService.CmdProcessorImages.OnQueueStateChangedEvent += (e) => OnQueueStateChangedEvent("images", e);
        }

        private IHubContext<EventsHub> Hub
        {
            get;
            set;
        }

        private readonly Dictionary<string, string> _lastState =
            new Dictionary<string, string>();
        private readonly Dictionary<string, int> _lastCount =
            new Dictionary<string, int>();

        private async void OnQueueStateChangedEvent(string queue, QueueStateEventArgs e)
        {
            await QueueStateChangedAsync(queue, e);
        }

        private async void OnQueueCountChangedEvent(string queue, QueueCountEventArgs ev)
        {
            await QueueCountChanged(queue, ev);
        }

        public async Task QueueStateChangedAsync(string queue, QueueStateEventArgs e)
        {
            var currentState = e.QueueState.formatMessage();

            if (_lastState.ContainsKey(queue) == true && _lastState.TryGetValue(queue, out var previousState) &&
                previousState == currentState)
            {
                return;
            }

            _lastState[queue] = currentState;
            await Hub.Clients.All.SendAsync("QueueStateChanged", queue, currentState);
        }

        public async Task QueueCountChanged(string queue, QueueCountEventArgs e)
        {
            var currentCount = e.QueueCount;

            if (_lastCount.ContainsKey(queue) == true && _lastCount.TryGetValue(queue, out var previousCount) &&
                previousCount == currentCount)
            {
                return;
            }

            _lastCount[queue] = currentCount;
            await Hub.Clients.All.SendAsync("QueueCountChanged", queue, currentCount);
        }
    }
}
