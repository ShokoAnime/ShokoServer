using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.User.Events;
using Shoko.Abstractions.User.Services;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

public class UserEventEmitter : BaseEventEmitter, IDisposable
{
    private readonly IUserService _userService;

    private readonly ILogger<UserEventEmitter> _logger;

    public UserEventEmitter(IHubContext<AggregateHub> hub, IUserService userService, ILogger<UserEventEmitter> logger) : base(hub)
    {
        _userService = userService;
        _logger = logger;
        _userService.UserAdded += OnUserAdded;
        _userService.UserUpdated += OnUserUpdated;
        _userService.UserRemoved += OnUserRemoved;
    }

    public void Dispose()
    {
        _userService.UserAdded -= OnUserAdded;
        _userService.UserUpdated -= OnUserUpdated;
        _userService.UserRemoved -= OnUserRemoved;
    }

    private async void OnUserAdded(object? sender, UserChangedEventArgs e)
    {
        try
        {
            await SendAsync("added", new UserChangedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'added' event.");
        }
    }

    private async void OnUserUpdated(object? sender, UserChangedEventArgs e)
    {
        try
        {
            await SendAsync("updated", new UserChangedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'updated' event.");
        }
    }

    private async void OnUserRemoved(object? sender, UserChangedEventArgs e)
    {
        try
        {
            await SendAsync("removed", new UserChangedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'removed' event.");
        }
    }
}
