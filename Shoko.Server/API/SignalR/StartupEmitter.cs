using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Events;
using Shoko.Abstractions.Core.Services;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.API.v3.Models.Shoko;

namespace Shoko.Server.API.SignalR;

/// <summary>
///   Sends every connection to <see cref="StartupHub"/> what the start says:
///   <c>Backlog</c> on connect, then each new <c>Message</c>, and the
///   <c>State</c> on connect and whenever it changes. A successful start
///   closes every connection; a failed one leaves them open until the restart.
/// </summary>
/// <remarks>
///   <see cref="ISystemService.StartupMessageChanged"/> is raised through
///   <c>Task.Run</c>, so its events may arrive out of order. An event is taken
///   only as a sign that something is new: the emitter reads
///   <see cref="ISystemService.StartupMessages"/>, which is ordered, and sends
///   each connection what follows the last entry it was sent. Each
///   connection's sends run one after another, so nothing is sent twice or
///   out of order.
/// </remarks>
public class StartupEmitter : IDisposable
{
    // The start's last message, which says nothing once the server has started.
    private const string CompleteMessage = "Complete!";

    private readonly ISystemService _systemService;

    private readonly IHubContext<StartupHub> _hub;

    private readonly ILogger<StartupEmitter> _logger;

    private readonly TimeSpan _closeGrace;

    private readonly ConcurrentDictionary<string, Subscriber> _subscribers = new();

    public StartupEmitter(ISystemService systemService, IHubContext<StartupHub> hub, ILogger<StartupEmitter> logger)
        : this(systemService, hub, logger, TimeSpan.FromSeconds(2)) { }

    // With its own wait between sending started and closing, for tests.
    internal StartupEmitter(ISystemService systemService, IHubContext<StartupHub> hub, ILogger<StartupEmitter> logger, TimeSpan closeGrace)
    {
        _systemService = systemService;
        _hub = hub;
        _logger = logger;
        _closeGrace = closeGrace;
        _systemService.StartupMessageChanged += OnMessage;
        _systemService.StartupFailed += OnFailed;
        _systemService.Started += OnStarted;
        _systemService.SetupRequired += OnSetupChanged;
        _systemService.SetupCompleted += OnSetupChanged;
    }

    /// <summary>
    ///   How many connections the emitter is following.
    /// </summary>
    internal int Count => _subscribers.Count;

    /// <summary>
    ///   Follows a new connection: its backlog and state are queued before it
    ///   is listed, so no event can send it anything first.
    /// </summary>
    /// <param name="connectionId">The connection's ID.</param>
    /// <param name="abort">Closes the connection.</param>
    /// <param name="signedIn">Whether the caller was signed in, as one that gave the setup token is.</param>
    /// <returns>When the backlog and the state have been sent.</returns>
    public Task JoinAsync(string connectionId, Action abort, bool signedIn = false)
    {
        var subscriber = new Subscriber(connectionId, abort, signedIn);
        var sent = subscriber.Enqueue(() => SendBacklogAsync(subscriber));
        _subscribers[connectionId] = subscriber;
        // A start that ended between the policy and here is sent and closed at once.
        if (_systemService.IsStarted)
            _ = CloseAsync(subscriber);
        return sent;
    }

    /// <summary>
    ///   Stops following a connection that has gone.
    /// </summary>
    /// <param name="connectionId">The connection's ID.</param>
    public void Leave(string connectionId)
        => _subscribers.TryRemove(connectionId, out _);

    // Waits for everything queued for every connection so far, for tests.
    internal Task IdleAsync()
        => Task.WhenAll(_subscribers.Values.Select(subscriber => subscriber.Enqueue(() => Task.CompletedTask)));

    public void Dispose()
    {
        _systemService.StartupMessageChanged -= OnMessage;
        _systemService.StartupFailed -= OnFailed;
        _systemService.Started -= OnStarted;
        _systemService.SetupRequired -= OnSetupChanged;
        _systemService.SetupCompleted -= OnSetupChanged;
    }

    private void OnMessage(object? sender, StartupMessageChangedEventArgs e)
    {
        foreach (var subscriber in _subscribers.Values)
            _ = subscriber.Enqueue(() => SendNewAsync(subscriber, e.Entry));
    }

    private void OnFailed(object? sender, StartupFailedEventArgs e)
    {
        var state = new StartupStateSignalRModel(ServerStatus.StartupState.Failed, e.Exception.Message);
        foreach (var subscriber in _subscribers.Values)
        {
            // Failure mode is guarded by the setup token while one is set, so a caller who joined without it is closed
            // rather than told why the start failed; it may join again with the token.
            if (_systemService.HasSetupToken && !subscriber.SignedIn)
            {
                _ = subscriber.Enqueue(() =>
                {
                    subscriber.Closing = true;
                    subscriber.Abort();
                    _subscribers.TryRemove(subscriber.ConnectionId, out _);
                    return Task.CompletedTask;
                });
                continue;
            }

            _ = subscriber.Enqueue(() => SendStateAsync(subscriber, state));
        }
    }

    private void OnStarted(object? sender, EventArgs e)
    {
        foreach (var subscriber in _subscribers.Values)
            _ = CloseAsync(subscriber);
    }

    private void OnSetupChanged(object? sender, EventArgs e)
    {
        foreach (var subscriber in _subscribers.Values)
            _ = subscriber.Enqueue(() => SendStateAsync(subscriber, null));
    }

    private Task CloseAsync(Subscriber subscriber)
        => subscriber.Enqueue(async () =>
        {
            if (subscriber.Closing)
                return;
            await SendStateAsync(subscriber, new(ServerStatus.StartupState.Started, null)).ConfigureAwait(false);
            subscriber.Closing = true;
            await Task.Delay(_closeGrace).ConfigureAwait(false);
            // Aborted before it leaves the list, so whoever no longer finds it there can count on it being closed.
            subscriber.Abort();
            _subscribers.TryRemove(subscriber.ConnectionId, out _);
        });

    private async Task SendBacklogAsync(Subscriber subscriber)
    {
        var entries = _systemService.StartupMessages.ToList();
        subscriber.Last = entries.LastOrDefault();
        await SendAsync(subscriber, "Backlog", entries.Select(entry => new StartupMessageSignalRModel(entry)).ToList()).ConfigureAwait(false);
        await SendStateAsync(subscriber, null).ConfigureAwait(false);
    }

    private Task SendNewAsync(Subscriber subscriber, StartupMessageEntry? hint)
    {
        if (subscriber.Closing)
            return Task.CompletedTask;
        var entries = _systemService.StartupMessages;
        var fresh = After(entries, subscriber.Last);
        // The list is cleared after a start; an entry it no longer holds is still news.
        if (fresh.Count == 0 && hint is not null && !entries.Contains(hint) && (subscriber.Last is null || hint.Time > subscriber.Last.Time))
            fresh = [hint];
        if (fresh.Count == 0)
            return Task.CompletedTask;
        subscriber.Last = fresh[^1];
        return SendAllAsync(subscriber, fresh);
    }

    private async Task SendAllAsync(Subscriber subscriber, IReadOnlyList<StartupMessageEntry> entries)
    {
        foreach (var entry in entries)
            await SendAsync(subscriber, "Message", new StartupMessageSignalRModel(entry)).ConfigureAwait(false);
    }

    private async Task SendStateAsync(Subscriber subscriber, StartupStateSignalRModel? state)
    {
        // The last messages go before the state they led to.
        await SendNewAsync(subscriber, null).ConfigureAwait(false);
        state ??= ReadState();

        // The backlog's state and an event's may be the same one.
        if (subscriber.LastState is { } last && last.State == state.State && last.Message == state.Message)
            return;
        subscriber.LastState = state;
        await SendAsync(subscriber, "State", state).ConfigureAwait(false);
    }

    // Where the start is, read as GET /api/v3/Init/Status reads it: started, else failed, else waiting in setup mode, else starting.
    private StartupStateSignalRModel ReadState()
        => _systemService switch
        {
            { IsStarted: true } => new(ServerStatus.StartupState.Started, null),
            { StartupFailedException: { } failure } => new(ServerStatus.StartupState.Failed, failure.Message),
            { InSetupMode: true } => new(ServerStatus.StartupState.Waiting, null),
            { StartupMessage: var current } => new(ServerStatus.StartupState.Starting, current is CompleteMessage ? null : current),
        };

    private async Task SendAsync(Subscriber subscriber, string method, object payload)
    {
        try
        {
            await _hub.Clients.Client(subscriber.ConnectionId).SendAsync(method, payload).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not send the startup feed to {ConnectionId}.", subscriber.ConnectionId);
        }
    }

    // The entries after the last one sent, found by reference, or by time when the list no longer holds it.
    private static List<StartupMessageEntry> After(IReadOnlyList<StartupMessageEntry> entries, StartupMessageEntry? last)
    {
        if (last is null)
            return [.. entries];
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(entries[i], last))
                return [.. entries.Skip(i + 1)];
        }

        return [.. entries.Where(entry => entry.Time > last.Time)];
    }

    private sealed class Subscriber(string connectionId, Action abort, bool signedIn)
    {
        private readonly object _lock = new();

        private Task _tail = Task.CompletedTask;

        public string ConnectionId { get; } = connectionId;

        public Action Abort { get; } = abort;

        public bool SignedIn { get; } = signedIn;

        // Read and written only by the queued work, which runs one at a time.
        public StartupMessageEntry? Last { get; set; }

        public StartupStateSignalRModel? LastState { get; set; }

        public bool Closing { get; set; }

        public Task Enqueue(Func<Task> work)
        {
            lock (_lock)
                return _tail = _tail.ContinueWith(_ => work(), TaskScheduler.Default).Unwrap();
        }
    }
}
