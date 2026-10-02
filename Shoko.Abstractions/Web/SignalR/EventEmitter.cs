using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Web.SignalR;

/// <summary>
/// The base of a feed on the aggregate hub. It tracks the feed's connections
/// per user, adds them to and removes them from the feed's group, and sends
/// the initial messages on join. A derived feed names itself and sends with
/// <see cref="SendAsync"/>, <see cref="SendToUserAsync"/>,
/// <see cref="SendWhereAsync"/> and <see cref="SendPerUserAsync"/>.
/// </summary>
public abstract class EventEmitter : IEventEmitter
{
    #region Fields

    private readonly ConcurrentDictionary<string, IUser> _connections = [];

    private readonly Dictionary<int, UserConnections> _userConnections = [];

    private readonly Lock _userConnectionsLock = new();

    /// <summary>
    /// Every user on the feed with their connections, rebuilt whenever a
    /// connection joins or leaves, so a send reads it without locking.
    /// </summary>
    private volatile Listener[] _listeners = [];

    private IHubContext? _hub;

    #endregion

    #region Properties

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <summary>
    /// The aggregate hub's context, once the server attached the feed, for a
    /// feed that sends in ways the send methods don't cover.
    /// </summary>
    protected IHubContext? HubContext => _hub;

    #endregion

    #region Connections

    /// <inheritdoc />
    public void Attach(IHubContext hub)
    {
        ArgumentNullException.ThrowIfNull(hub);
        _hub = hub;
    }

    /// <inheritdoc />
    public bool IsListening(string connectionId) => _connections.ContainsKey(connectionId);

    /// <inheritdoc />
    public async Task<bool> ConnectAsync(string connectionId, IUser user, DateTime? lastConnectedAt = null)
    {
        var hub = _hub ?? throw new InvalidOperationException($"The feed '{Name}' is not attached to a hub.");
        if (!CanConnect(user) || !_connections.TryAdd(connectionId, user))
            return false;

        lock (_userConnectionsLock)
        {
            if (!_userConnections.TryGetValue(user.LocalID, out var connections))
                _userConnections.Add(user.LocalID, connections = new());
            connections.User = user;
            connections.ConnectionIDs.Add(connectionId);
            RebuildListeners();
        }

        await hub.Groups.AddToGroupAsync(connectionId, Name);

        var messages = GetInitialMessagesForUser(connectionId, user, lastConnectedAt) ?? GetInitialMessages();
        if (messages.Length > 0)
            await hub.Clients.Client(connectionId).SendCoreAsync(GetMessageName("connected"), messages);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DisconnectAsync(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var user))
            return false;

        lock (_userConnectionsLock)
        {
            if (_userConnections.TryGetValue(user.LocalID, out var connections))
            {
                connections.ConnectionIDs.Remove(connectionId);
                if (connections.ConnectionIDs.Count == 0)
                    _userConnections.Remove(user.LocalID);
                RebuildListeners();
            }
        }

        if (_hub is { } hub)
            await hub.Groups.RemoveFromGroupAsync(connectionId, Name);

        OnConnectionRemoved(connectionId);

        return true;
    }

    /// <summary>
    /// Decides whether a user may join the feed. A user who may not is never
    /// added, and is sent nothing, not even the initial messages.
    /// </summary>
    /// <param name="user">The user asking to join.</param>
    /// <returns><see langword="true"/> if the user may join; the default lets everyone join.</returns>
    protected virtual bool CanConnect(IUser user) => true;

    /// <summary>
    /// Called when a connection leaves the feed, after it was removed from the
    /// feed's group, so a feed can drop what it keeps per connection.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID that left.</param>
    protected virtual void OnConnectionRemoved(string connectionId) { }

    /// <summary>
    /// Rebuilds <see cref="_listeners"/> from the connections per user. Called
    /// with the lock held.
    /// </summary>
    private void RebuildListeners()
        => _listeners = [.. _userConnections.Values.Select(connections => new Listener(connections.User, [.. connections.ConnectionIDs]))];

    #endregion

    #region Sending

    /// <inheritdoc />
    public async Task SendAsync(string subject, params object[] args)
    {
        if (_hub is { } hub)
            await hub.Clients.Group(Name).SendCoreAsync(GetMessageName(subject), args);
    }

    /// <inheritdoc />
    public async Task SendToUserAsync(IUser user, string subject, params object[] args)
    {
        if (_hub is not { } hub)
            return;

        string[] connectionIds;
        lock (_userConnectionsLock)
            connectionIds = _userConnections.TryGetValue(user.LocalID, out var connections) ? [.. connections.ConnectionIDs] : [];
        foreach (var connectionId in connectionIds)
            await hub.Clients.Client(connectionId).SendCoreAsync(GetMessageName(subject), args);
    }

    /// <summary>
    /// Sends a message to the connections of the users on the feed that match
    /// a predicate, such as the users who may see what the message is about.
    /// The predicate is asked once per user, before anything is sent.
    /// </summary>
    /// <param name="predicate">Whether a user on the feed receives the message.</param>
    /// <param name="subject">The message's subject; clients receive it as <c>&lt;name&gt;:&lt;subject&gt;</c>.</param>
    /// <param name="args">The message's arguments, serialized for the client.</param>
    /// <returns>A task completing once the message was handed to the matching connections, of which there may be none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    protected Task SendWhereAsync(Func<IUser, bool> predicate, string subject, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return SendPerUserAsync(subject, user => predicate(user) ? args : null);
    }

    /// <summary>
    /// Sends a message whose arguments depend on the user, to the connections
    /// of every user on the feed. The arguments are asked for once per user,
    /// before anything is sent; users given the same array share one send.
    /// </summary>
    /// <param name="subject">The message's subject; clients receive it as <c>&lt;name&gt;:&lt;subject&gt;</c>.</param>
    /// <param name="getArgs">The message's arguments for a user, or <see langword="null"/> to send that user nothing.</param>
    /// <returns>A task completing once the messages were handed to the connections, of which there may be none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="getArgs"/> is <see langword="null"/>.</exception>
    protected async Task SendPerUserAsync(string subject, Func<IUser, object[]?> getArgs)
    {
        ArgumentNullException.ThrowIfNull(getArgs);
        if (_hub is not { } hub)
            return;

        var listeners = _listeners;
        if (listeners.Length is 0)
            return;

        var batches = new List<(object[] Args, List<string> ConnectionIDs)>();
        foreach (var listener in listeners)
        {
            if (getArgs(listener.User) is not { } args)
                continue;

            var index = batches.FindIndex(batch => ReferenceEquals(batch.Args, args));
            if (index is -1)
                batches.Add((args, [.. listener.ConnectionIDs]));
            else
                batches[index].ConnectionIDs.AddRange(listener.ConnectionIDs);
        }

        if (batches.Count is 0)
            return;

        var name = GetMessageName(subject);
        foreach (var (args, connectionIDs) in batches)
            await hub.Clients.Clients(connectionIDs).SendCoreAsync(name, args);
    }

    /// <summary>
    /// The messages sent as <c>&lt;name&gt;:connected</c> to a connection
    /// joining the feed, when <see cref="GetInitialMessagesForUser"/> has none
    /// of its own. Nothing is sent when there are none.
    /// </summary>
    /// <returns>The message's arguments; the default is none.</returns>
    protected virtual object[] GetInitialMessages() => [];

    /// <summary>
    /// The messages sent as <c>&lt;name&gt;:connected</c> to one connection
    /// joining the feed, for a feed whose initial state differs per user or
    /// connection.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID joining the feed.</param>
    /// <param name="user">The user the connection signed in as.</param>
    /// <param name="lastConnectedAt">When the client was last connected, if it said so when joining, so the feed can send only what changed since.</param>
    /// <returns>The message's arguments, or <see langword="null"/> to send <see cref="GetInitialMessages"/> instead, which is the default.</returns>
    protected virtual object[]? GetInitialMessagesForUser(string connectionId, IUser user, DateTime? lastConnectedAt = null) => null;

    /// <summary>
    /// Names a message the feed sends.
    /// </summary>
    /// <param name="subject">The message's subject.</param>
    /// <returns>The name clients receive the message by, <c>&lt;name&gt;:&lt;subject&gt;</c>.</returns>
    protected string GetMessageName(string subject) => Name + ":" + subject;

    #endregion

    #region Nested Types

    /// <summary>
    /// A user's connections to the feed.
    /// </summary>
    private sealed class UserConnections
    {
        /// <summary>
        /// The user, as the latest of its connections signed in.
        /// </summary>
        public IUser User { get; set; } = null!;

        /// <summary>
        /// The user's SignalR connection IDs on the feed.
        /// </summary>
        public HashSet<string> ConnectionIDs { get; } = [];
    }

    /// <summary>
    /// A user on the feed and its connections, as one send reads them.
    /// </summary>
    /// <param name="User">The user.</param>
    /// <param name="ConnectionIDs">The user's SignalR connection IDs on the feed.</param>
    private sealed record Listener(IUser User, string[] ConnectionIDs);

    #endregion
}
