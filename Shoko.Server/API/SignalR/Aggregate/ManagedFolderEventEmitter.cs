using System;
using Microsoft.AspNetCore.SignalR;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.SignalR.Models;

#nullable enable
namespace Shoko.Server.API.SignalR.Aggregate;

public class ManagedFolderEventEmitter : BaseEventEmitter, IDisposable
{
    private readonly IVideoService _userService;

    public ManagedFolderEventEmitter(IHubContext<AggregateHub> hub, IVideoService userService) : base(hub)
    {
        _userService = userService;
        _userService.ManagedFolderAdded += OnManagedFolderAdded;
        _userService.ManagedFolderUpdated += OnManagedFolderUpdated;
        _userService.ManagedFolderRemoved += OnManagedFolderRemoved;
    }

    public void Dispose()
    {
        _userService.ManagedFolderAdded -= OnManagedFolderAdded;
        _userService.ManagedFolderUpdated -= OnManagedFolderUpdated;
        _userService.ManagedFolderRemoved -= OnManagedFolderRemoved;
    }

    private async void OnManagedFolderAdded(object? sender, ManagedFolderChangedEventArgs e)
    {
        await SendAsync("added", new ManagedFolderChangedSignalRModel(e));
    }

    private async void OnManagedFolderUpdated(object? sender, ManagedFolderChangedEventArgs e)
    {
        await SendAsync("updated", new ManagedFolderChangedSignalRModel(e));
    }

    private async void OnManagedFolderRemoved(object? sender, ManagedFolderChangedEventArgs e)
    {
        await SendAsync("removed", new ManagedFolderChangedSignalRModel(e));
    }
}
