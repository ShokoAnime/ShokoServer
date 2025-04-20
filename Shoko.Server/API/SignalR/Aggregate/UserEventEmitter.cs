using System;
using Microsoft.AspNetCore.SignalR;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.SignalR.Models;

#nullable enable
namespace Shoko.Server.API.SignalR.Aggregate;

public class UserEventEmitter : BaseEventEmitter, IDisposable
{
    private readonly IUserService _userService;

    public UserEventEmitter(IHubContext<AggregateHub> hub, IUserService userService) : base(hub)
    {
        _userService = userService;
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
        await SendAsync("added", new UserChangedSignalRModel(e));
    }

    private async void OnUserUpdated(object? sender, UserChangedEventArgs e)
    {
        await SendAsync("updated", new UserChangedSignalRModel(e));
    }

    private async void OnUserRemoved(object? sender, UserChangedEventArgs e)
    {
        await SendAsync("removed", new UserChangedSignalRModel(e));
    }
}
