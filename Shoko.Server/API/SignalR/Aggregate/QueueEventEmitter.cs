using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Abstractions.User;
using Shoko.QueueProcessor;
using Shoko.QueueProcessor.Events;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.API.v3.Models.Shoko;

namespace Shoko.Server.API.SignalR.Aggregate;

public class QueueEventEmitter : BaseEventEmitter, IDisposable
{
    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromMilliseconds(250);

    private readonly QueueStateEventHandler _queueStateEventHandler;
    private readonly QueueHandler _queueHandler;
    private QueueStateSignalRModel? _lastQueueState;

    // Connections that opted in to per-pool detail. Empty by default — pool info is omitted unless
    // a client explicitly requests it (queue.set_pool_info, or the queue_pools connect query flag).
    private readonly ConcurrentDictionary<string, byte> _poolSubscribers = new();

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

    private QueueStateSignalRModel GetQueueState(bool includePools)
    {
        return new QueueStateSignalRModel
        {
            Running = _queueStateEventHandler.Running,
            WaitingCount = _queueHandler.WaitingCount,
            BlockedCount = _queueHandler.BlockedCount,
            ScheduledCount = _queueHandler.ScheduledCount,
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
            // Per-pool detail is opt-in (it's the largest part of the payload and most clients
            // only need the aggregate counts). Omitted unless the connection requested it.
            Pools = includePools
                ? _queueHandler.GetPoolStatus().Values.Select(p => new Queue.PoolState
                {
                    Name = p.Name,
                    MaxWorkers = p.MaxWorkers,
                    ActiveWorkers = p.ActiveWorkers,
                    IdleWorkers = p.IdleWorkers,
                    WaitingCount = p.WaitingCount,
                    BlockedCount = p.BlockedCount,
                    ScheduledCount = p.ScheduledCount,
                    IsBlocked = p.IsBlocked,
                    HandledTypeNames = p.HandledTypeNames,
                    LastActiveAt = p.LastActiveAt?.UtcDateTime
                }).OrderBy(p => p.Name).ToList()
                : []
        };
    }

    /// <summary>
    /// Opt a connection in or out of per-pool detail in queue state messages. Pushes a fresh state
    /// immediately so the change takes effect without waiting for the next queue event.
    /// </summary>
    public void SetIncludePools(string connectionId, bool include)
    {
        if (include)
            _poolSubscribers[connectionId] = 0;
        else
            _poolSubscribers.TryRemove(connectionId, out _);
        RequestPush();
    }

    protected override void OnConnectionRemoved(string connectionId)
        => _poolSubscribers.TryRemove(connectionId, out _);

    protected override object[] GetInitialMessages()
        => [GetQueueState(includePools: false)];

    protected override object[] GetInitialMessagesForUser(string connectionId, IUser user, DateTime? lastConnectedAt = null)
        => [GetQueueState(_poolSubscribers.ContainsKey(connectionId))];

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
        var withoutPools = GetQueueState(includePools: false);
        _lastQueueState = withoutPools;

        var poolIds = _poolSubscribers.Keys.ToArray();
        if (poolIds.Length == 0)
        {
            await SendAsync("state.changed", withoutPools);
            return;
        }

        // Pool subscribers get the detailed payload; everyone else gets the lean one.
        var withPools = GetQueueState(includePools: true);
        await Hub.Clients.GroupExcept(Group, poolIds).SendCoreAsync(GetName("state.changed"), [withoutPools]);
        await Hub.Clients.Clients(poolIds).SendCoreAsync(GetName("state.changed"), [withPools]);
    }
}
