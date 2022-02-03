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
    public class QueueEmitter : IDisposable
    {
        private IHubContext<QueueHub> Hub { get; set; }

        private readonly Dictionary<string, object> _lastState = new Dictionary<string, object>();

        public QueueEmitter(IHubContext<QueueHub> hub)
        {
            Hub = hub;
            ShokoService.CmdProcessorGeneral.OnQueueCountChangedEvent += OnGeneralQueueCountChangedEvent;
            ShokoService.CmdProcessorHasher.OnQueueCountChangedEvent += OnHasherQueueCountChangedEvent;
            ShokoService.CmdProcessorImages.OnQueueCountChangedEvent += OnImageQueueCountChangedEvent;

            ShokoService.CmdProcessorGeneral.OnQueueStateChangedEvent += OnGeneralQueueStateChangedEvent;
            ShokoService.CmdProcessorHasher.OnQueueStateChangedEvent += OnHasherQueueStateChangedEvent;
            ShokoService.CmdProcessorImages.OnQueueStateChangedEvent += OnImageQueueStateChangedEvent;
            ServerState.Instance.PropertyChanged += ServerStatePropertyChanged;
        }

        public void Dispose()
        {
            ShokoService.CmdProcessorGeneral.OnQueueCountChangedEvent -= OnGeneralQueueCountChangedEvent;
            ShokoService.CmdProcessorHasher.OnQueueCountChangedEvent -= OnHasherQueueCountChangedEvent;
            ShokoService.CmdProcessorImages.OnQueueCountChangedEvent -= OnImageQueueCountChangedEvent;

            ShokoService.CmdProcessorGeneral.OnQueueStateChangedEvent -= OnGeneralQueueStateChangedEvent;
            ShokoService.CmdProcessorHasher.OnQueueStateChangedEvent -= OnHasherQueueStateChangedEvent;
            ShokoService.CmdProcessorImages.OnQueueStateChangedEvent -= OnImageQueueStateChangedEvent;
            ServerState.Instance.PropertyChanged -= ServerStatePropertyChanged;
        }

        public async Task OnConnectedAsync(IClientProxy caller)
        {
            if (ServerState.Instance.DatabaseAvailable)
                await caller.SendAsync(
                    "CommandProcessingStatus", new Dictionary<string, object>
                    {
                        {"GeneralQueueState", new QueueStateSignalRModel {State = ShokoService.CmdProcessorGeneral.QueueState.queueState, Description = ShokoService.CmdProcessorGeneral.QueueState.formatMessage()}},
                        {"HasherQueueState", new QueueStateSignalRModel {State = ShokoService.CmdProcessorHasher.QueueState.queueState, Description = ShokoService.CmdProcessorHasher.QueueState.formatMessage()}},
                        {"ImageQueueState", new QueueStateSignalRModel {State = ShokoService.CmdProcessorImages.QueueState.queueState, Description = ShokoService.CmdProcessorImages.QueueState.formatMessage()}},
                        {"GeneralQueueCount", ShokoService.CmdProcessorGeneral.QueueCount},
                        {"HasherQueueCount", ShokoService.CmdProcessorHasher.QueueCount},
                        {"ImageQueueCount", ShokoService.CmdProcessorImages.QueueCount},
                    }
                );
        }

        private async void ServerStatePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == null) return;
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
