using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.QueueProcessor;
using Shoko.QueueProcessor.Events;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.API.v3.Models.Shoko;

#nullable enable
namespace Shoko.Server.API.SignalR.Aggregate;

public class QueueEventEmitter : BaseEventEmitter, IDisposable
{
    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromMilliseconds(250);

    private readonly QueueStateEventHandler _queueStateEventHandler;
    private readonly QueueHandler _queueHandler;
    private QueueStateSignalRModel? _lastQueueState;

    // Trailing-edge throttle: first push in a quiet period fires immediately; subsequent pushes
    // within ThrottleWindow are coalesced into a single trailing push at window end. Under burst
    // load (workers completing fast jobs from many threads) this collapses N events into 2 sends.
    private readonly object _throttleGate = new();
    private readonly Timer _trailingTimer;
    private DateTimeOffset _lastSendAt = DateTimeOffset.MinValue;
    private bool _trailingScheduled;
    private bool _disposed;

    public QueueEventEmitter(IHubContext<AggregateHub> hub, QueueStateEventHandler queueStateEventHandler, QueueHandler queueHandler) : base(hub)
    {
        _queueStateEventHandler = queueStateEventHandler;
        _queueHandler = queueHandler;
        _trailingTimer = new Timer(OnTrailingTick, null, Timeout.Infinite, Timeout.Infinite);
        _queueStateEventHandler.QueueItemsAdded += OnQueueItemsAddedEvent;
        _queueStateEventHandler.ExecutingJobsChanged += OnExecutingJobsStateChangedEvent;
        _queueStateEventHandler.QueueStarted += OnQueueStarted;
        _queueStateEventHandler.QueuePaused += OnQueuePaused;
    }

    public void Dispose()
    {
        _queueStateEventHandler.QueueItemsAdded -= OnQueueItemsAddedEvent;
        _queueStateEventHandler.ExecutingJobsChanged -= OnExecutingJobsStateChangedEvent;
        _queueStateEventHandler.QueueStarted -= OnQueueStarted;
        _queueStateEventHandler.QueuePaused -= OnQueuePaused;
        lock (_throttleGate) _disposed = true;
        _trailingTimer.Dispose();
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
                Type = a.TypeName ?? a.JobType ?? string.Empty,
                Title = a.Title ?? string.Empty,
                Details = a.Details ?? [],
                IsRunning = true,
                StartTime = a.StartTime?.ToUniversalTime()
            }).OrderBy(a => a.StartTime).ToList(),
            Pools = _queueHandler.GetPoolStatus().Values.Select(p => new Queue.PoolState
            {
                Name = p.Name,
                MaxWorkers = p.MaxWorkers,
                ActiveWorkers = p.ActiveWorkers,
                IdleWorkers = p.IdleWorkers,
                WaitingCount = p.WaitingCount,
                IsBlocked = p.IsBlocked,
                HandledTypeNames = p.HandledTypeNames,
                LastActiveAt = p.LastActiveAt?.UtcDateTime
            }).OrderBy(p => p.Name).ToList()
        };
    }

    protected override object[] GetInitialMessages()
    {
        var state = GetQueueState();
        return [state];
    }

    private void OnQueueStarted(object? sender, EventArgs e) => RequestPush();

    private void OnQueuePaused(object? sender, EventArgs e) => RequestPush();

    private void OnQueueItemsAddedEvent(object? sender, QueueItemsAddedEventArgs e) => RequestPush();

    private void OnExecutingJobsStateChangedEvent(object? sender, QueueChangedEventArgs e) => RequestPush();

    /// <summary>
    /// Trailing-edge throttle. First call in a quiet period sends immediately; subsequent calls
    /// within <see cref="ThrottleWindow"/> just ensure a single trailing send is scheduled.
    /// </summary>
    private void RequestPush()
    {
        var now = DateTimeOffset.UtcNow;
        var sendNow = false;
        TimeSpan? scheduleDelay = null;

        lock (_throttleGate)
        {
            if (_disposed) return;
            var elapsed = now - _lastSendAt;
            if (elapsed >= ThrottleWindow)
            {
                _lastSendAt = now;
                sendNow = true;
            }
            else if (!_trailingScheduled)
            {
                _trailingScheduled = true;
                scheduleDelay = ThrottleWindow - elapsed;
            }
        }

        if (sendNow)
            _ = PushAsync();
        else if (scheduleDelay.HasValue)
            _trailingTimer.Change(scheduleDelay.Value, Timeout.InfiniteTimeSpan);
    }

    private void OnTrailingTick(object? _)
    {
        lock (_throttleGate)
        {
            if (_disposed) return;
            _trailingScheduled = false;
            _lastSendAt = DateTimeOffset.UtcNow;
        }
        _ = PushAsync();
    }

    private async Task PushAsync()
    {
        var state = GetQueueState();
        _lastQueueState = state;
        await SendAsync("state.changed", state);
    }
}
