using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.User.Events;
using Shoko.Abstractions.User.Services;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

public class UserDataEventEmitter : BaseEventEmitter, IDisposable
{
    private readonly IUserDataService _userDataService;

    private readonly ILogger<UserDataEventEmitter> _logger;

    public override string Group { get; } = "userData";

    public UserDataEventEmitter(IHubContext<AggregateHub> hub, IUserDataService userDataService, ILogger<UserDataEventEmitter> logger) : base(hub)
    {
        _userDataService = userDataService;
        _logger = logger;
        _userDataService.VideoUserDataSaved += OnVideoUserDataSaved;
        _userDataService.EpisodeUserDataSaved += OnEpisodeUserDataSaved;
        _userDataService.SeriesUserDataSaved += OnSeriesUserDataSaved;
        _userDataService.GroupUserDataSaved += OnGroupUserDataSaved;
    }

    public void Dispose()
    {
        _userDataService.VideoUserDataSaved -= OnVideoUserDataSaved;
        _userDataService.EpisodeUserDataSaved -= OnEpisodeUserDataSaved;
        _userDataService.SeriesUserDataSaved -= OnSeriesUserDataSaved;
        _userDataService.GroupUserDataSaved -= OnGroupUserDataSaved;
    }

    private async void OnVideoUserDataSaved(object? sender, VideoUserDataSavedEventArgs e)
    {
        try
        {
            await SendAsync("video.saved", new VideoUserDataSavedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'video.saved' event.");
        }
    }

    private async void OnEpisodeUserDataSaved(object? sender, EpisodeUserDataSavedEventArgs e)
    {
        try
        {
            await SendAsync("episode.saved", new EpisodeUserDataSavedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'episode.saved' event.");
        }
    }

    private async void OnSeriesUserDataSaved(object? sender, SeriesUserDataSavedEventArgs e)
    {
        try
        {
            await SendAsync("series.saved", new SeriesUserDataSavedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'series.saved' event.");
        }
    }

    private async void OnGroupUserDataSaved(object? sender, GroupUserDataSavedEventArgs e)
    {
        try
        {
            await SendAsync("group.saved", new GroupUserDataSavedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'group.saved' event.");
        }
    }
}
