using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Commons.Notification;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Scheduling;
using Shoko.Server.Server;

namespace Shoko.Server.API.SignalR.Aggregate;

public class QueueEmitter : BaseEmitter, IDisposable
{
    private readonly QueueStateEventHandler _queueStateEventHandler;
    private readonly QueueHandler _queueHandler;
    private readonly Dictionary<string, object> _lastState = new();

    public QueueEmitter(IHubContext<AggregateHub> hub, QueueStateEventHandler queueStateEventHandler, QueueHandler queueHandler) : base(hub)
    {
        _queueStateEventHandler = queueStateEventHandler;
        _queueHandler = queueHandler;
        _queueStateEventHandler.QueueChanged += OnQueueStateChangedEvent;
        ServerState.Instance.PropertyChanged += ServerStatePropertyChanged;
    }

    public void Dispose()
    {
        _queueStateEventHandler.QueueChanged -= OnQueueStateChangedEvent;
        ServerState.Instance.PropertyChanged -= ServerStatePropertyChanged;
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

    private async void OnQueueStateChangedEvent(object sender, QueueChangedEventArgs e)
    {
        await StateChangedAsync("QueueStateChanged", "QueueState",
            new QueueStateSignalRModel
            {
                WaitingCount = e.WaitingJobsCount,
                BlockedCount = e.BlockedJobsCount,
                TotalCount = e.WaitingJobsCount + e.BlockedJobsCount,
                ThreadCount = e.ThreadCount,
                CurrentlyExecuting = e.ExecutingItems.Select(a => new Queue.QueueItem
                {
                    Key = a.Key,
                    Type = a.JobType,
                    Description = a.Description,
                    IsRunning = true
                }).ToList()
            });
    }

    private async Task StateChangedAsync(string method, string property, object currentState)
    {
        if (_lastState.ContainsKey(property) && _lastState.TryGetValue(property, out var previousState) &&
            previousState == currentState) return;

        _lastState[property] = currentState;
        await SendAsync(method, property, currentState);
    }

    public override object GetInitialMessage()
    {
        return new Dictionary<string, object>
        {
            {
                "QueueState", new QueueStateSignalRModel
                {
                    WaitingCount = _queueHandler.WaitingCount,
                    BlockedCount = _queueHandler.BlockedCount,
                    TotalCount = _queueHandler.Count,
                    ThreadCount = _queueHandler.ThreadCount
                }
            },
        };
    }
}
