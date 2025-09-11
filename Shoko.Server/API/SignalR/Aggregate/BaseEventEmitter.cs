using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

namespace Shoko.Server.API.SignalR.Aggregate;

public abstract class BaseEventEmitter : IEventEmitter
{
    private string _group;

    private readonly ConcurrentDictionary<string, IShokoUser> _connectionDict = [];

    private readonly ConcurrentDictionary<int, HashSet<string>> _userToConnectionDict = [];

    protected readonly IHubContext<Hub> Hub;

    public virtual string Group => _group ??= GetType().FullName?.Split('.').LastOrDefault()?.Replace("EventEmitter", "")?.Replace("Emitter", "").ToLower() ?? "misc";

    protected BaseEventEmitter(IHubContext<Hub> hub)
    {
        Hub = hub;
    }

    public async Task ConnectAsync(string connectionId, IShokoUser user, DateTime? lastConnectedAt = null)
    {
        if (!_connectionDict.TryAdd(connectionId, user))
            return;

        lock (_userToConnectionDict)
        {
            if (_userToConnectionDict.TryGetValue(user.ID, out var connections) || _userToConnectionDict.TryAdd(user.ID, connections = []))
                connections.Add(connectionId);
        }

        await Hub.Groups.AddToGroupAsync(connectionId, Group);

        var messages = GetInitialMessagesForUser(connectionId, user, lastConnectedAt);
        if (messages.Length == 0)
            messages = GetInitialMessages();
        if (messages.Length > 0)
            await Hub.Clients.Client(connectionId).SendCoreAsync(GetName("connected"), messages);
    }

    public async Task DisconnectAsync(string connectionId)
    {
        if (!_connectionDict.TryRemove(connectionId, out var user))
            return;

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
    }

    public async Task SendAsync(string subject, params object[] args)
    {
        await Hub.Clients.Group(Group).SendCoreAsync(GetName(subject), args);
    }

    public async Task SendToUserAsync(IShokoUser user, string subject, params object[] args)
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

    protected virtual object[] GetInitialMessagesForUser(string connectionId, IShokoUser user, DateTime? lastConnectedAt = null)
    {
        return [];
    }

    protected virtual string GetName(string message)
    {
        return Group + ":" + message;
    }
}
