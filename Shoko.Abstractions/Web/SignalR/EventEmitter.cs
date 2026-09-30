using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Web.SignalR;

/// <summary>
/// The base of a feed on the aggregate hub. It tracks the feed's connections
/// per user, adds them to and removes them from the feed's group, and sends
/// the initial messages on join. A derived feed names itself and sends with
/// <see cref="SendAsync"/> and <see cref="SendToUserAsync"/>.
/// </summary>
public abstract class EventEmitter : IEventEmitter
{
    #region Fields

    private readonly ConcurrentDictionary<string, IUser> _connections = [];

    private readonly ConcurrentDictionary<int, HashSet<string>> _userConnections = [];

    private IHubContext? _hub;

    #endregion

    #region Properties

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <summary>
    /// The aggregate hub's context, once the server attached the feed, for a
    /// feed that sends in ways <see cref="SendAsync"/> and
    /// <see cref="SendToUserAsync"/> don't cover.
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

        lock (_userConnections)
        {
            if (_userConnections.TryGetValue(user.LocalID, out var connections) || _userConnections.TryAdd(user.LocalID, connections = []))
                connections.Add(connectionId);
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

        lock (_userConnections)
        {
            if (_userConnections.TryGetValue(user.LocalID, out var connections))
            {
                connections.Remove(connectionId);
                if (connections.Count == 0)
                    _userConnections.TryRemove(user.LocalID, out _);
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
        lock (_userConnections)
            connectionIds = _userConnections.TryGetValue(user.LocalID, out var connections) ? connections.ToArray() : [];
        foreach (var connectionId in connectionIds)
            await hub.Clients.Client(connectionId).SendCoreAsync(GetMessageName(subject), args);
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
}
