using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Video.Events;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

public class ManagedFolderEventEmitter : BaseEventEmitter, IDisposable
{
    private readonly IVideoService _userService;

    private readonly ILogger<ManagedFolderEventEmitter> _logger;

    public ManagedFolderEventEmitter(IHubContext<AggregateHub> hub, IVideoService userService, ILogger<ManagedFolderEventEmitter> logger) : base(hub)
    {
        _userService = userService;
        _logger = logger;
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
        try
        {
            await SendAsync("added", new ManagedFolderChangedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'added' event.");
        }
    }

    private async void OnManagedFolderUpdated(object? sender, ManagedFolderChangedEventArgs e)
    {
        try
        {
            await SendAsync("updated", new ManagedFolderChangedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'updated' event.");
        }
    }

    private async void OnManagedFolderRemoved(object? sender, ManagedFolderChangedEventArgs e)
    {
        try
        {
            await SendAsync("removed", new ManagedFolderChangedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'removed' event.");
        }
    }
}
