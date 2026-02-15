using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Abstractions.User;

#nullable enable
namespace Shoko.Server.API.SignalR.Aggregate;

public abstract class BaseEventEmitter : IEventEmitter
{
    private string? _group;

    private readonly ConcurrentDictionary<string, IUser> _connectionDict = [];

    private readonly ConcurrentDictionary<int, HashSet<string>> _userToConnectionDict = [];

    protected readonly IHubContext<Hub> Hub;

    public virtual string Group => _group ??= GetType().FullName?.Split('.').LastOrDefault()?.Replace("EventEmitter", "")?.Replace("Emitter", "").ToLower() ??
        throw new InvalidOperationException("Unable to parse group name from type!");

    protected BaseEventEmitter(IHubContext<Hub> hub)
    {
        Hub = hub;
    }

    public bool IsListening(string connectionId) => _connectionDict.ContainsKey(connectionId);

    public async Task<bool> ConnectAsync(string connectionId, IUser user, DateTime? lastConnectedAt = null)
    {
        if (!_connectionDict.TryAdd(connectionId, user))
            return false;

        lock (_userToConnectionDict)
        {
            if (_userToConnectionDict.TryGetValue(user.ID, out var connections) || _userToConnectionDict.TryAdd(user.ID, connections = []))
                connections.Add(connectionId);
        }

        await Hub.Groups.AddToGroupAsync(connectionId, Group);

        var messages = GetInitialMessagesForUser(connectionId, user, lastConnectedAt) ?? GetInitialMessages();
        if (messages.Length > 0)
            await Hub.Clients.Client(connectionId).SendCoreAsync(GetName("connected"), messages);

        return true;
    }

    public async Task<bool> DisconnectAsync(string connectionId)
    {
        if (!_connectionDict.TryRemove(connectionId, out var user))
            return false;

        lock (_userToConnectionDict)
        {
            if (_userToConnectionDict.TryGetValue(user.ID, out var connections))
            {
                connections.Remove(connectionId);
                if (connections.Count == 0)
                    _userToConnectionDict.TryRemove(user.ID, out _);
            }
        }

        await Hub.Groups.RemoveFromGroupAsync(connectionId, Group);

        return true;
    }

    public async Task SendAsync(string subject, params object[] args)
    {
        await Hub.Clients.Group(Group).SendCoreAsync(GetName(subject), args);
    }

    public async Task SendToUserAsync(IUser user, string subject, params object[] args)
    {
        var clients = _userToConnectionDict.TryGetValue(user.ID, out var connections) ? connections.ToArray() : [];
        foreach (var connectionId in clients)
        {
            var client = Hub.Clients.Client(connectionId);
            await client.SendCoreAsync(GetName(subject), args);
        }
    }

    protected virtual object[] GetInitialMessages()
    {
        return [];
    }

    protected virtual object[]? GetInitialMessagesForUser(string connectionId, IUser user, DateTime? lastConnectedAt = null)
    {
        return null;
    }

    protected virtual string GetName(string message)
    {
        return Group + ":" + message;
    }
}
