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

    [HubMethodName("feed.join")]
    public async Task JoinFeeds(string[] feeds, DateTime? lastConnectedAt = null)
    {
        var connectionId = Context.ConnectionId;
        var user = Context.User.GetUser();
        var feedsToAdd = _allFeeds!.Keys.Intersect(feeds).ToList();
        foreach (var feed in feedsToAdd)
        {
            var emitter = _allFeeds![feed];
            await emitter.ConnectAsync(connectionId, user, lastConnectedAt);
        }
    }

    [HubMethodName("feed.leave")]
    public async Task LeaveFeeds(string[] feeds)
    {
        var connectionId = Context.ConnectionId;
        var feedsToRemove = _allFeeds!.Keys.Intersect(feeds).ToList();
        foreach (var feed in feedsToRemove)
        {
            var emitter = _allFeeds![feed];
            await emitter.DisconnectAsync(connectionId);
        }
    }

    [HubMethodName("feed.replace")]
    public async Task UpdateFeeds(string[] feeds, DateTime? lastConnectedAt = null)
    {
        var connectionId = Context.ConnectionId;
        var user = Context.User.GetUser();
        var feedsToAdd = _allFeeds!.Keys.Intersect(feeds).ToList();
        foreach (var (feed, emitter) in _allFeeds)
        {
            if (feedsToAdd.Contains(feed))
                await emitter.ConnectAsync(connectionId, user, lastConnectedAt);
            else
                await emitter.DisconnectAsync(connectionId);
        }
    }

    [HubMethodName("queue.pause")]
    public void ChangeQueueProcessingState(bool paused)
    {
        if (paused) _queueHandler.Pause().GetAwaiter().GetResult();
        else _queueHandler.Resume().GetAwaiter().GetResult();
    }
}
