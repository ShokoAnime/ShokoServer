using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Shoko.Abstractions.Web.SignalR;
using Shoko.QueueProcessor;

namespace Shoko.Server.API.SignalR.Aggregate;

public class AggregateHub : Hub
{
    private readonly QueueHandler _queueHandler;

    private readonly EventEmitterRegistry _registry;

    private readonly FrozenDictionary<string, IEventEmitter> _allFeeds;

    public AggregateHub(QueueHandler queueHandler, EventEmitterRegistry registry)
    {
        _queueHandler = queueHandler;
        _registry = registry;
        _allFeeds = registry.Feeds;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        var context = Context.GetHttpContext();
        // Opt this connection in to per-pool detail before joining, so the initial queue state
        // (sent during the join) already reflects the choice. Off by default.
        if (bool.TryParse(context?.Request.Query["queue_pools"].ToString(), out var wantPools) && wantPools)
            SetQueuePoolInfo(true);

        var query = context?.Request.Query["feeds"]
            .Where(a => !string.IsNullOrEmpty(a))
            .SelectMany(a => a!.Split(","))
            .Select(a => a.Trim())
            .ToArray();

        // Says which feeds the connection is on once it joined what it asked for, an empty list if nothing.
        var joined = query is { Length: > 0 } ? await JoinFeeds(query) : [];
        await Clients.Caller.SendAsync("feed.joined", joined);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var emitter in _allFeeds.Values)
            await emitter.DisconnectAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    [HubMethodName("feed.list_all")]
    public IReadOnlyList<string> ListFeeds()
        => [.. _allFeeds.Keys];

    [HubMethodName("feed.list_joined")]
    public IReadOnlyList<string> ListJoinedFeeds()
    {
        var connectionId = Context.ConnectionId;
        var feeds = new List<string>();
        foreach (var (feed, emitter) in _allFeeds)
        {
            if (emitter.IsListening(connectionId))
                feeds.Add(feed);
        }
        return feeds;
    }

    // Every call that changes the feeds answers with the ones the connection is on afterwards, as
    // feed.list_joined does: a feed asked for but missing is unknown or refused the user.

    [HubMethodName("feed.join_single")]
    public async Task<IReadOnlyList<string>> JoinFeed(string feed, DateTime? lastConnectedAt = null)
    {
        if (_allFeeds.TryGetValue(feed, out var emitter))
            await emitter.ConnectAsync(Context.ConnectionId, Context.User.GetUser(), lastConnectedAt);
        return ListJoinedFeeds();
    }

    [HubMethodName("feed.join_many")]
    public async Task<IReadOnlyList<string>> JoinFeeds(string[] feeds, DateTime? lastConnectedAt = null)
    {
        var connectionId = Context.ConnectionId;
        var user = Context.User.GetUser();
        foreach (var emitter in _registry.Find(feeds))
            await emitter.ConnectAsync(connectionId, user, lastConnectedAt);
        return ListJoinedFeeds();
    }

    [HubMethodName("feed.leave_single")]
    public async Task<IReadOnlyList<string>> LeaveFeeds(string feed)
    {
        if (_allFeeds.TryGetValue(feed, out var emitter))
            await emitter.DisconnectAsync(Context.ConnectionId);
        return ListJoinedFeeds();
    }

    [HubMethodName("feed.leave_many")]
    public async Task<IReadOnlyList<string>> LeaveFeeds(string[] feeds)
    {
        var connectionId = Context.ConnectionId;
        foreach (var emitter in _registry.Find(feeds))
            await emitter.DisconnectAsync(connectionId);
        return ListJoinedFeeds();
    }

    [HubMethodName("feed.replace_all")]
    public async Task<IReadOnlyList<string>> UpdateFeeds(string[] feeds, DateTime? lastConnectedAt = null)
    {
        var connectionId = Context.ConnectionId;
        var user = Context.User.GetUser();
        var feedsToAdd = _registry.Find(feeds);
        foreach (var emitter in _allFeeds.Values)
        {
            if (feedsToAdd.Contains(emitter))
                await emitter.ConnectAsync(connectionId, user, lastConnectedAt);
            else
                await emitter.DisconnectAsync(connectionId);
        }
        return ListJoinedFeeds();
    }

    [HubMethodName("feed.clear_all")]
    public async Task<IReadOnlyList<string>> ClearFeeds()
    {
        var connectionId = Context.ConnectionId;
        foreach (var emitter in _allFeeds.Values)
            await emitter.DisconnectAsync(connectionId);
        return ListJoinedFeeds();
    }

    /// <summary>
    /// Pauses or resumes the queue, for admins only, as the REST routes do.
    /// </summary>
    /// <param name="paused">Whether to pause the queue rather than resume it.</param>
    [Authorize("admin")]
    [HubMethodName("queue.pause")]
    public void ChangeQueueProcessingState(bool paused)
    {
        if (paused) _queueHandler.Pause().GetAwaiter().GetResult();
        else _queueHandler.Resume().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Opt this connection in or out of per-pool detail in the <c>queue:state.changed</c> feed.
    /// Pool info is omitted by default; call with <c>true</c> to receive it.
    /// </summary>
    [HubMethodName("queue.set_pool_info")]
    public void SetQueuePoolInfo(bool include)
    {
        if (_allFeeds.TryGetValue("queue", out var emitter) && emitter is QueueEventEmitter queueEmitter)
            queueEmitter.SetIncludePools(Context.ConnectionId, include);
    }
}
