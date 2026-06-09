using System;
using Microsoft.AspNetCore.SignalR;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

public class GroupEventEmitter : BaseEventEmitter, IDisposable
{
    public GroupEventEmitter(IHubContext<AggregateHub> hub) : base(hub)
    {
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
        var eventName = e.Reason is UpdateReason.None ? "group.updated" : "group." + e.Reason.ToString().ToLower();
        await SendAsync(eventName, new GroupInfoUpdatedEventSignalRModel(e));
    }

    private async void OnSeriesMoved(object sender, SeriesMovedEventArgs e)
    {
        await SendAsync("series.moved", new SeriesMovedEventSignalRModel(e));
    }

    private async void OnGroupsRecreated(object sender, EventArgs e)
    {
        await SendAsync("recreated");
    }
}
