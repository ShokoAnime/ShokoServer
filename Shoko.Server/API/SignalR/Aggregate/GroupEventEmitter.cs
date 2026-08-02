using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

public class GroupEventEmitter : BaseEventEmitter, IDisposable
{
    private readonly ILogger<GroupEventEmitter> _logger;

    public GroupEventEmitter(IHubContext<AggregateHub> hub, ILogger<GroupEventEmitter> logger) : base(hub)
    {
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

    private async void OnGroupUpdated(object sender, GroupInfoUpdatedEventArgs e)
    {
        try
        {
            var eventName = e.Reason is UpdateReason.None ? "group.updated" : "group." + e.Reason.ToString().ToLower();
            await SendAsync(eventName, new GroupInfoUpdatedEventSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'group' event.");
        }
    }

    private async void OnSeriesMoved(object sender, SeriesMovedEventArgs e)
    {
        try
        {
            await SendAsync("series.moved", new SeriesMovedEventSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'series.moved' event.");
        }
    }

    private async void OnGroupsRecreated(object sender, EventArgs e)
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
