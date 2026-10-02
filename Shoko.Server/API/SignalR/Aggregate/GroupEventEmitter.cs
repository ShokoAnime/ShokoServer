using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.Repositories.Cached.AniDB;

namespace Shoko.Server.API.SignalR.Aggregate;

/// <summary>
/// Bridges the group events to the aggregate hub. A group event reaches the
/// users who may see any series in the group, and a moved series the users
/// who may see the series.
/// </summary>
/// <remarks>
/// A group is only removed once it holds no series, so a removal goes to users
/// without restricted tags only. Groups being recreated carries no entity, and
/// goes to everyone.
/// </remarks>
public class GroupEventEmitter : BaseEventEmitter, IDisposable
{
    public override string Name => "group";

    private readonly AniDB_AnimeRepository _anidbAnime;

    private readonly ILogger<GroupEventEmitter> _logger;

    public GroupEventEmitter(IHubContext<AggregateHub> hub, AniDB_AnimeRepository anidbAnime, ILogger<GroupEventEmitter> logger) : base(hub)
    {
        _anidbAnime = anidbAnime;
        _logger = logger;
        ShokoEventHandler.Instance.GroupUpdated += OnGroupUpdated;
        ShokoEventHandler.Instance.SeriesMoved += OnSeriesMoved;
        ShokoEventHandler.Instance.GroupsRecreated += OnGroupsRecreated;
    }

    public void Dispose()
    {
        ShokoEventHandler.Instance.GroupUpdated -= OnGroupUpdated;
        ShokoEventHandler.Instance.SeriesMoved -= OnSeriesMoved;
        ShokoEventHandler.Instance.GroupsRecreated -= OnGroupsRecreated;
    }

    private async void OnGroupUpdated(object? sender, GroupInfoUpdatedEventArgs e)
    {
        try
        {
            var eventName = e.Reason is UpdateReason.None ? "group.updated" : "group." + e.Reason.ToString().ToLower();
            await SendToAudienceAsync(EventAudience.ForGroup(e.GroupInfo, _anidbAnime.GetByAnimeID), eventName, new GroupInfoUpdatedEventSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'group' event.");
        }
    }

    private async void OnSeriesMoved(object? sender, SeriesMovedEventArgs e)
    {
        try
        {
            await SendToAudienceAsync(EventAudience.ForSeries(e.SeriesInfo, _anidbAnime.GetByAnimeID), "series.moved", new SeriesMovedEventSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'series.moved' event.");
        }
    }

    private async void OnGroupsRecreated(object? sender, EventArgs e)
    {
        try
        {
            await SendAsync("recreated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'recreated' event.");
        }
    }
}
