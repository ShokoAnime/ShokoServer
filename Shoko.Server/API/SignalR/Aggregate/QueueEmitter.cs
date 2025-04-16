using System;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Scheduling;

#nullable enable
namespace Shoko.Server.API.SignalR.Aggregate;

public class QueueEmitter : BaseEmitter, IDisposable
{
    private readonly QueueStateEventHandler _queueStateEventHandler;
    private readonly QueueHandler _queueHandler;
    private QueueStateSignalRModel? _lastQueueState;

    public QueueEmitter(IHubContext<AggregateHub> hub, QueueStateEventHandler queueStateEventHandler, QueueHandler queueHandler) : base(hub)
    {
        _queueStateEventHandler = queueStateEventHandler;
        _queueHandler = queueHandler;
        _queueStateEventHandler.QueueItemsAdded += OnQueueItemsAddedEvent;
        _queueStateEventHandler.ExecutingJobsChanged += OnExecutingJobsStateChangedEvent;
        _queueStateEventHandler.QueueStarted += OnQueueStarted;
        _queueStateEventHandler.QueuePaused += OnQueuePaused;
    }

    public void Dispose()
    {
        _queueStateEventHandler.ExecutingJobsChanged -= OnExecutingJobsStateChangedEvent;
        _queueStateEventHandler.QueueStarted -= OnQueueStarted;
        _queueStateEventHandler.QueuePaused -= OnQueuePaused;
        GC.SuppressFinalize(this);
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
                StartTime = a.StartTime?.ToUniversalTime()
            }).OrderBy(a => a.StartTime).ToList()
        };
    }

    public override object GetInitialMessage()
    {
        return GetQueueState();
    }

    private async void OnQueueStarted(object? sender, EventArgs e)
    {
        var state = GetQueueState();

        await SendAsync("QueueStateChanged", state);
    }

    private async void OnQueuePaused(object? sender, EventArgs e)
    {
        var state = GetQueueState();

        await SendAsync("QueueStateChanged", state);
    }

    private async void OnQueueItemsAddedEvent(object? sender, QueueItemsAddedEventArgs e)
    {
        var currentState = new QueueStateSignalRModel
        {
            Running = _queueStateEventHandler.Running,
            WaitingCount = e.WaitingJobsCount,
            BlockedCount = e.BlockedJobsCount,
            TotalCount = e.WaitingJobsCount + e.BlockedJobsCount + e.ExecutingJobsCount,
            ThreadCount = e.ThreadCount,
            CurrentlyExecuting = _lastQueueState is { } ? _lastQueueState.CurrentlyExecuting : [],
        };
        if (Equals(_lastQueueState, currentState)) return;
        _lastQueueState = currentState;
        await SendAsync("QueueStateChanged", currentState);
    }

    private async void OnExecutingJobsStateChangedEvent(object? sender, QueueChangedEventArgs e)
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
                StartTime = a.StartTime?.ToUniversalTime()
            }).OrderBy(a => a.StartTime).ToList()
        };
        if (Equals(_lastQueueState, currentState)) return;
        _lastQueueState = currentState;
        await SendAsync("QueueStateChanged", currentState);
    }
}
