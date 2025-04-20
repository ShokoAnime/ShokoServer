using System;
using Microsoft.AspNetCore.SignalR;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.SignalR.Models;

#nullable enable
namespace Shoko.Server.API.SignalR.Aggregate;

public class UserDataEventEmitter : BaseEventEmitter, IDisposable
{
    private readonly IUserDataService _userDataService;

    public override string Group { get; } = "userData";

    public UserDataEventEmitter(IHubContext<AggregateHub> hub, IUserDataService userDataService) : base(hub)
    {
        _userDataService = userDataService;
        _userDataService.VideoUserDataSaved += OnVideoUserDataSaved;
    }

    public void Dispose()
    {
        _userDataService.VideoUserDataSaved -= OnVideoUserDataSaved;
    }

    private async void OnVideoUserDataSaved(object? sender, VideoUserDataSavedEventArgs e)
    {
        await SendToUserAsync(e.User, "saved", new VideoUserDataSavedSignalRModel(e));
    }
}
