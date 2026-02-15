using System;
using Microsoft.AspNetCore.SignalR;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.Services;
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
        _userDataService.EpisodeUserDataSaved += OnEpisodeUserDataSaved;
        _userDataService.SeriesUserDataSaved += OnSeriesUserDataSaved;
    }

    public void Dispose()
    {
        _userDataService.VideoUserDataSaved -= OnVideoUserDataSaved;
        _userDataService.EpisodeUserDataSaved -= OnEpisodeUserDataSaved;
        _userDataService.SeriesUserDataSaved -= OnSeriesUserDataSaved;
    }

    private async void OnVideoUserDataSaved(object? sender, VideoUserDataSavedEventArgs e)
    {
        await SendAsync("video.saved", new VideoUserDataSavedSignalRModel(e));
    }

    private async void OnEpisodeUserDataSaved(object? sender, EpisodeUserDataSavedEventArgs e)
    {
        await SendAsync("episode.saved", new EpisodeUserDataSavedSignalRModel(e));
    }

    private async void OnSeriesUserDataSaved(object? sender, SeriesUserDataSavedEventArgs e)
    {
        await SendAsync("series.saved", new SeriesUserDataSavedSignalRModel(e));
    }
}
