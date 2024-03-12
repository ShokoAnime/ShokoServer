using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
    private QueueStateSignalRModel _lastQueueState;
    private readonly Dictionary<string, object> _lastServerState = new();

    public QueueEmitter(IHubContext<AggregateHub> hub, QueueStateEventHandler queueStateEventHandler, QueueHandler queueHandler) : base(hub)
    {
        _queueStateEventHandler = queueStateEventHandler;
        _queueHandler = queueHandler;
        _queueStateEventHandler.ExecutingJobsChanged += OnExecutingJobsStateChangedEvent;
        _queueStateEventHandler.QueueStarted += OnQueueStarted;
        _queueStateEventHandler.QueuePaused += OnQueuePaused;
        ServerState.Instance.PropertyChanged += ServerStatePropertyChanged;
    }

    private QueueStateSignalRModel GetQueueState()
    {
        return new QueueStateSignalRModel
        {
            Running = _queueStateEventHandler.Running,
            WaitingCount = _queueHandler.WaitingCount,
            BlockedCount = _queueHandler.BlockedCount,
            TotalCount = _queueHandler.TotalCount,
            ThreadCount = _queueHandler.ThreadCount,
            CurrentlyExecuting = _queueHandler.GetExecutingJobs().Select(a => new Queue.QueueItem
            {
                Key = a.Key,
                Type = a.JobType,
                Title = a.Title,
                Details = a.Details,
                IsRunning = true,
                StartTime = a.StartTime
            }).OrderBy(a => a.StartTime).ToList()
        };
    }

    public override object GetInitialMessage()
    {
        return GetQueueState();
    }

    private async void OnQueueStarted(object sender, EventArgs e)
    {
        var state = GetQueueState();

        await SendAsync("QueueStateChanged", state);
    }

    private async void OnQueuePaused(object sender, EventArgs e)
    {
        var state = GetQueueState();

        await SendAsync("QueueStateChanged", state);
    }

    public void Dispose()
    {
        _queueStateEventHandler.ExecutingJobsChanged -= OnExecutingJobsStateChangedEvent;
        _queueStateEventHandler.QueueStarted -= OnQueueStarted;
        _queueStateEventHandler.QueuePaused -= OnQueuePaused;
        ServerState.Instance.PropertyChanged -= ServerStatePropertyChanged;
    }

    private async void ServerStatePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == null) return;
        // Currently, only the DatabaseBlocked property, but we could use this for more.
        if (e.PropertyName != "DatabaseBlocked" && !e.PropertyName.StartsWith("Server")) return;

        var value = e.GetPropertyValue(sender);
        if (_lastServerState.ContainsKey(e.PropertyName) && _lastServerState.TryGetValue(e.PropertyName, out var previousState) &&
            Equals(previousState, value)) return;

        _lastServerState[e.PropertyName] = value;
        await SendAsync("ServerStateChanged", e.PropertyName, value);
    }

    private async void OnExecutingJobsStateChangedEvent(object sender, QueueChangedEventArgs e)
    {
        var currentState = new QueueStateSignalRModel
        {
            Running = _queueStateEventHandler.Running,
            WaitingCount = e.WaitingJobsCount,
            BlockedCount = e.BlockedJobsCount,
            TotalCount = e.WaitingJobsCount + e.BlockedJobsCount + e.ExecutingItems.Count,
            ThreadCount = e.ThreadCount,
            CurrentlyExecuting = e.ExecutingItems.Select(a => new Queue.QueueItem
            {
                Key = a.Key,
                Type = a.JobType,
                Title = a.Title,
                Details = a.Details,
                IsRunning = true,
                StartTime = a.StartTime
            }).OrderBy(a => a.StartTime).ToList()
        };
        if (Equals(_lastQueueState, currentState)) return;
        _lastQueueState = currentState;
        await SendAsync("QueueStateChanged", currentState);
    }
}
