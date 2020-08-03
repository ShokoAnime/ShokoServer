using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Server.Commands;

namespace Shoko.Server.API.SignalR
{
    public class EventEmitter : IDisposable
    {
        private IHubContext<EventsHub> Hub { get; set; }

        private readonly Dictionary<string, string> _lastState = new Dictionary<string, string>();
        private readonly Dictionary<string, int> _lastCount = new Dictionary<string, int>();

        public EventEmitter(IHubContext<EventsHub> hub)
        {
            Hub = hub;
            ShokoService.CmdProcessorGeneral.OnQueueCountChangedEvent += OnGeneralQueueCountChangedEvent;
            ShokoService.CmdProcessorHasher.OnQueueCountChangedEvent += OnHasherQueueCountChangedEvent;
            ShokoService.CmdProcessorImages.OnQueueCountChangedEvent += OnImageQueueCountChangedEvent;

            ShokoService.CmdProcessorGeneral.OnQueueStateChangedEvent += OnGeneralQueueStateChangedEvent;
            ShokoService.CmdProcessorHasher.OnQueueStateChangedEvent += OnImageQueueStateChangedEvent;
            ShokoService.CmdProcessorImages.OnQueueStateChangedEvent += OnHasherQueueStateChangedEvent;
        }

        public void Dispose()
        {
            ShokoService.CmdProcessorGeneral.OnQueueCountChangedEvent -= OnGeneralQueueCountChangedEvent;
            ShokoService.CmdProcessorHasher.OnQueueCountChangedEvent -= OnHasherQueueCountChangedEvent;
            ShokoService.CmdProcessorImages.OnQueueCountChangedEvent -= OnImageQueueCountChangedEvent;

            ShokoService.CmdProcessorGeneral.OnQueueStateChangedEvent -= OnGeneralQueueStateChangedEvent;
            ShokoService.CmdProcessorHasher.OnQueueStateChangedEvent -= OnImageQueueStateChangedEvent;
            ShokoService.CmdProcessorImages.OnQueueStateChangedEvent -= OnHasherQueueStateChangedEvent;
        }

        private async void OnGeneralQueueStateChangedEvent(QueueStateEventArgs e)
        {
            await QueueStateChangedAsync("general", e);
        }

        private async void OnHasherQueueStateChangedEvent(QueueStateEventArgs e)
        {
            await QueueStateChangedAsync("hasher", e);
        }

        private async void OnImageQueueStateChangedEvent(QueueStateEventArgs e)
        {
            await QueueStateChangedAsync("images", e);
        }

        private async void OnGeneralQueueCountChangedEvent(QueueCountEventArgs ev)
        {
            await QueueCountChanged("general", ev);
        }
        
        private async void OnHasherQueueCountChangedEvent(QueueCountEventArgs ev)
        {
            await QueueCountChanged("hasher", ev);
        }
        
        private async void OnImageQueueCountChangedEvent(QueueCountEventArgs ev)
        {
            await QueueCountChanged("images", ev);
        }

        public async Task QueueStateChangedAsync(string queue, QueueStateEventArgs e)
        {
            var currentState = e.QueueState.formatMessage();

            if (_lastState.ContainsKey(queue) && _lastState.TryGetValue(queue, out var previousState) &&
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

            if (_lastCount.ContainsKey(queue) && _lastCount.TryGetValue(queue, out var previousCount) &&
                previousCount == currentCount)
            {
                return;
            }

            _lastCount[queue] = currentCount;
            await Hub.Clients.All.SendAsync("QueueCountChanged", queue, currentCount);
        }
    }
}
