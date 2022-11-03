using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Commons.Notification;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.Commands;
using Shoko.Server.Server;

namespace Shoko.Server.API.SignalR.Aggregate;

public class QueueEmitter : BaseEmitter, IDisposable
{
    private readonly Dictionary<string, object> _lastState = new();
    public event EventHandler<(string Name, object[] args)> StateUpdate;

    public QueueEmitter()
    {
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

    private void ServerStatePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == null) return;
        // Currently, only the DatabaseBlocked property, but we could use this for more.
        if (e.PropertyName == "DatabaseBlocked" || e.PropertyName.StartsWith("Server"))
        {
            StateChangedAsync("ServerStateChanged", e.PropertyName, e.GetPropertyValue(sender));
        }
    }

    private void OnGeneralQueueStateChangedEvent(QueueStateEventArgs e)
    {
        StateChangedAsync("QueueStateChanged", "GeneralQueueState",
            new QueueStateSignalRModel
                {State = e.QueueState.queueState, Description = e.QueueState.formatMessage()});
    }

    private void OnHasherQueueStateChangedEvent(QueueStateEventArgs e)
    {
        StateChangedAsync("QueueStateChanged", "HasherQueueState", new QueueStateSignalRModel
            {State = e.QueueState.queueState, Description = e.QueueState.formatMessage()});
    }

    private void OnImageQueueStateChangedEvent(QueueStateEventArgs e)
    {
        StateChangedAsync("QueueStateChanged", "ImageQueueState", new QueueStateSignalRModel
            {State = e.QueueState.queueState, Description = e.QueueState.formatMessage()});
    }

    private void OnGeneralQueueCountChangedEvent(QueueCountEventArgs ev)
    {
        StateChangedAsync("QueueCountChanged", "GeneralQueueCount", ev.QueueCount);
    }
        
    private void OnHasherQueueCountChangedEvent(QueueCountEventArgs ev)
    {
        StateChangedAsync("QueueCountChanged", "HasherQueueCount", ev.QueueCount);
    }
        
    private void OnImageQueueCountChangedEvent(QueueCountEventArgs ev)
    {
        StateChangedAsync("QueueCountChanged", "ImageQueueCount", ev.QueueCount);
    }

    private void StateChangedAsync(string method, string property, object currentState)
    {
        if (_lastState.ContainsKey(property) && _lastState.TryGetValue(property, out var previousState) &&
            previousState == currentState) return;

        _lastState[property] = currentState;
        StateUpdate?.Invoke(this, (GetName(method), new[] { property, currentState }));
    }

    public override object GetInitialMessage()
    {
        return new Dictionary<string, object>
        {
            {
                "GeneralQueueState",
                new QueueStateSignalRModel
                {
                    State = ShokoService.CmdProcessorGeneral.QueueState.queueState,
                    Description = ShokoService.CmdProcessorGeneral.QueueState.formatMessage()
                }
            },
            {
                "HasherQueueState",
                new QueueStateSignalRModel
                {
                    State = ShokoService.CmdProcessorHasher.QueueState.queueState,
                    Description = ShokoService.CmdProcessorHasher.QueueState.formatMessage()
                }
            },
            {
                "ImageQueueState",
                new QueueStateSignalRModel
                {
                    State = ShokoService.CmdProcessorImages.QueueState.queueState,
                    Description = ShokoService.CmdProcessorImages.QueueState.formatMessage()
                }
            },
            { "GeneralQueueCount", ShokoService.CmdProcessorGeneral.QueueCount },
            { "HasherQueueCount", ShokoService.CmdProcessorHasher.QueueCount },
            { "ImageQueueCount", ShokoService.CmdProcessorImages.QueueCount },
        };
    }
}
