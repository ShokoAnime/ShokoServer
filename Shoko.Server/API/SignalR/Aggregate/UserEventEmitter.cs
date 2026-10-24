using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.User.Events;
using Shoko.Abstractions.User.Services;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

public class UserEventEmitter : BaseEventEmitter, IDisposable
{
    public override string Name => "user";

    private readonly IUserService _userService;

    private readonly ILogger<UserEventEmitter> _logger;

    public UserEventEmitter(IHubContext<AggregateHub> hub, IUserService userService, ILogger<UserEventEmitter> logger) : base(hub)
    {
        _userService = userService;
        _logger = logger;
        _userService.UserAdded += OnUserAdded;
        _userService.UserUpdated += OnUserUpdated;
        _userService.UserRemoved += OnUserRemoved;
        _userService.ApiTokenGenerated += OnApiTokenGenerated;
        _userService.ApiTokenInvalidated += OnApiTokenInvalidated;
    }

    public void Dispose()
    {
        _userService.UserAdded -= OnUserAdded;
        _userService.UserUpdated -= OnUserUpdated;
        _userService.UserRemoved -= OnUserRemoved;
        _userService.ApiTokenGenerated -= OnApiTokenGenerated;
        _userService.ApiTokenInvalidated -= OnApiTokenInvalidated;
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

    private void OnApiTokenGenerated(object? sender, ApiTokenChangedEventArgs e)
        => SendApiToken("apiToken.generated", e);

    private void OnApiTokenInvalidated(object? sender, ApiTokenChangedEventArgs e)
        => SendApiToken("apiToken.invalidated", e);

    /// <summary>
    /// Sends a token event to the token's own user and to the admins only.
    /// </summary>
    /// <param name="subject">The message's subject.</param>
    /// <param name="e">The event.</param>
    private async void SendApiToken(string subject, ApiTokenChangedEventArgs e)
    {
        try
        {
            var ownerID = e.ApiToken.User.LocalID;
            await SendWhereAsync(user => user.IsAdmin || user.LocalID == ownerID, subject, new ApiTokenChangedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the '{Subject}' event.", subject);
        }
    }
}
