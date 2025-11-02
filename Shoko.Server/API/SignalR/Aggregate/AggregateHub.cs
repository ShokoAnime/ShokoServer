using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Server.Scheduling;

#nullable enable
namespace Shoko.Server.API.SignalR.Aggregate;

public class AggregateHub : Hub
{
    private readonly QueueHandler _queueHandler;

    private static FrozenDictionary<string, IEventEmitter>? _allFeeds;

    public AggregateHub(QueueHandler queueHandler, IEnumerable<IEventEmitter> emitters)
    {
        _queueHandler = queueHandler;
        _allFeeds ??= emitters.ToFrozenDictionary(a => a.Group);
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        var context = Context.GetHttpContext();
        var query = context?.Request.Query["feeds"]
            .Where(a => !string.IsNullOrEmpty(a))
            .SelectMany(a => a!.Split(","))
            .Select(a => a.ToLower().Trim())
            .ToArray();

        if (query == null || query.Length == 0)
            return;

        await JoinFeeds(query);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var emitter in _allFeeds!.Values)
            await emitter.DisconnectAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    [HubMethodName("feed.list_all")]
    public IReadOnlyList<string> ListFeeds()
        => [.. _allFeeds!.Keys];

    [HubMethodName("feed.list_joined")]
    public async Task<IReadOnlyList<string>> ListJoinedFeeds()
    {
        var connectionId = Context.ConnectionId;
        var feeds = new List<string>();
        foreach (var (feed, emitter) in _allFeeds!)
        {
            if (emitter.IsListening(connectionId))
                feeds.Add(feed);
        }
        return feeds;
    }

    [HubMethodName("feed.join_single")]
    public async Task<bool> JoinFeed(string feed, DateTime? lastConnectedAt = null)
    {
        if (!_allFeeds!.TryGetValue(feed, out var emitter))
            return false;

        return await emitter.ConnectAsync(Context.ConnectionId, Context.User.GetUser(), lastConnectedAt);
    }

    [HubMethodName("feed.join_many")]
    public async Task<bool> JoinFeeds(string[] feeds, DateTime? lastConnectedAt = null)
    {
        var connectionId = Context.ConnectionId;
        var user = Context.User.GetUser();
        var feedsToAdd = _allFeeds!.Keys.Intersect(feeds).ToList();
        var updated = false;
        foreach (var feed in feedsToAdd)
        {
            var emitter = _allFeeds![feed];
            updated = await emitter.ConnectAsync(connectionId, user, lastConnectedAt) || updated;
        }
        return updated;
    }

    [HubMethodName("feed.leave_single")]
    public async Task<bool> LeaveFeeds(string feed)
    {
        if (!_allFeeds!.TryGetValue(feed, out var emitter))
            return false;

        return await emitter.DisconnectAsync(Context.ConnectionId);
    }

    [HubMethodName("feed.leave_many")]
    public async Task<bool> LeaveFeeds(string[] feeds)
    {
        var connectionId = Context.ConnectionId;
        var feedsToRemove = _allFeeds!.Keys.Intersect(feeds).ToList();
        var updated = false;
        foreach (var feed in feedsToRemove)
        {
            var emitter = _allFeeds![feed];
            updated = await emitter.DisconnectAsync(connectionId) || updated;
        }
        return updated;
    }

    [HubMethodName("feed.replace_all")]
    public async Task<bool> UpdateFeeds(string[] feeds, DateTime? lastConnectedAt = null)
    {
        var connectionId = Context.ConnectionId;
        var user = Context.User.GetUser();
        var feedsToAdd = _allFeeds!.Keys.Intersect(feeds).ToList();
        var updated = false;
        foreach (var (feed, emitter) in _allFeeds)
        {
            if (feedsToAdd.Contains(feed))
                updated = await emitter.ConnectAsync(connectionId, user, lastConnectedAt) || updated;
            else
                updated = await emitter.DisconnectAsync(connectionId) || updated;
        }
        return updated;
    }

    [HubMethodName("feed.clear_all")]
    public async Task<bool> ClearFeeds()
    {
        var connectionId = Context.ConnectionId;
        var updated = false;
        foreach (var (feed, emitter) in _allFeeds!)
        {
            updated = await emitter.DisconnectAsync(connectionId) || updated;
        }
        return updated;
    }

    [HubMethodName("queue.pause")]
    public void ChangeQueueProcessingState(bool paused)
    {
        if (paused) _queueHandler.Pause().GetAwaiter().GetResult();
        else _queueHandler.Resume().GetAwaiter().GetResult();
    }
}
