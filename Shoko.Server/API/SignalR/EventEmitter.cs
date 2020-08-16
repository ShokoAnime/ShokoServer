using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Commons.Notification;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.Commands;
using Shoko.Server.Server;

namespace Shoko.Server.API.SignalR
{
    public class EventEmitter : IDisposable
    {
        private IHubContext<EventsHub> Hub { get; set; }

        private readonly Dictionary<string, object> _lastState = new Dictionary<string, object>();

        public EventEmitter(IHubContext<EventsHub> hub)
        {
            Hub = hub;
            ShokoService.CmdProcessorGeneral.OnQueueCountChangedEvent += OnGeneralQueueCountChangedEvent;
            ShokoService.CmdProcessorHasher.OnQueueCountChangedEvent += OnHasherQueueCountChangedEvent;
            ShokoService.CmdProcessorImages.OnQueueCountChangedEvent += OnImageQueueCountChangedEvent;

            ShokoService.CmdProcessorGeneral.OnQueueStateChangedEvent += OnGeneralQueueStateChangedEvent;
            ShokoService.CmdProcessorHasher.OnQueueStateChangedEvent += OnImageQueueStateChangedEvent;
            ShokoService.CmdProcessorImages.OnQueueStateChangedEvent += OnHasherQueueStateChangedEvent;
            ServerState.Instance.PropertyChanged += ServerStatePropertyChanged;
        }

        public void Dispose()
        {
            ShokoService.CmdProcessorGeneral.OnQueueCountChangedEvent -= OnGeneralQueueCountChangedEvent;
            ShokoService.CmdProcessorHasher.OnQueueCountChangedEvent -= OnHasherQueueCountChangedEvent;
            ShokoService.CmdProcessorImages.OnQueueCountChangedEvent -= OnImageQueueCountChangedEvent;

            ShokoService.CmdProcessorGeneral.OnQueueStateChangedEvent -= OnGeneralQueueStateChangedEvent;
            ShokoService.CmdProcessorHasher.OnQueueStateChangedEvent -= OnImageQueueStateChangedEvent;
            ShokoService.CmdProcessorImages.OnQueueStateChangedEvent -= OnHasherQueueStateChangedEvent;
            ServerState.Instance.PropertyChanged -= ServerStatePropertyChanged;
        }

        private async void ServerStatePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Currently, only the DatabaseBlocked property, but we could use this for more.
            if (e.PropertyName == "DatabaseBlocked" || e.PropertyName.StartsWith("Server"))
            {
                await StateChangedAsync("ServerStateChanged", e.PropertyName, e.GetPropertyValue(sender));
            }
        }

        private async void OnGeneralQueueStateChangedEvent(QueueStateEventArgs e)
        {
            await StateChangedAsync("QueueStateChanged", "GeneralQueueState",
                new QueueStateSignalRModel
                    {State = e.QueueState.queueState, Description = e.QueueState.formatMessage()});
        }

        private async void OnHasherQueueStateChangedEvent(QueueStateEventArgs e)
        {
            await StateChangedAsync("QueueStateChanged", "HasherQueueState", new QueueStateSignalRModel
                {State = e.QueueState.queueState, Description = e.QueueState.formatMessage()});
        }

        private async void OnImageQueueStateChangedEvent(QueueStateEventArgs e)
        {
            await StateChangedAsync("QueueStateChanged", "ImageQueueState", new QueueStateSignalRModel
                {State = e.QueueState.queueState, Description = e.QueueState.formatMessage()});
        }

        private async void OnGeneralQueueCountChangedEvent(QueueCountEventArgs ev)
        {
            await StateChangedAsync("QueueCountChanged", "GeneralQueueCount", ev.QueueCount);
        }
        
        private async void OnHasherQueueCountChangedEvent(QueueCountEventArgs ev)
        {
            await StateChangedAsync("QueueCountChanged", "HasherQueueCount", ev.QueueCount);
        }
        
        private async void OnImageQueueCountChangedEvent(QueueCountEventArgs ev)
        {
            await StateChangedAsync("QueueCountChanged", "ImageQueueCount", ev.QueueCount);
        }

        public async Task StateChangedAsync(string method, string property, object currentState)
        {
            if (_lastState.ContainsKey(property) && _lastState.TryGetValue(property, out var previousState) &&
                previousState == currentState)
            {
                return;
            }

            _lastState[property] = currentState;
            await Hub.Clients.All.SendAsync(method, property, currentState);
        }
    }
}
