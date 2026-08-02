using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Video.Events;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

public class ReleaseEventEmitter : BaseEventEmitter, IDisposable
{
    private readonly IVideoReleaseService _videoService;

    private readonly ILogger<ReleaseEventEmitter> _logger;

    public ReleaseEventEmitter(IHubContext<AggregateHub> hub, IVideoReleaseService videoReleaseService, ILogger<ReleaseEventEmitter> logger) : base(hub)
    {
        _videoService = videoReleaseService;
        _logger = logger;
        _videoService.ReleaseSaved += OnReleaseSaved;
        _videoService.ReleaseDeleted += OnReleaseDeleted;
        _videoService.SearchCompleted += OnSearchCompleted;
    }

    public void Dispose()
    {
        _videoService.ReleaseSaved -= OnReleaseSaved;
        _videoService.ReleaseDeleted -= OnReleaseDeleted;
        _videoService.SearchCompleted -= OnSearchCompleted;
    }

    private async void OnReleaseSaved(object sender, VideoReleaseSavedEventArgs e)
    {
        try
        {
            await SendAsync("saved", new VideoReleaseSavedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'saved' event.");
        }
    }

    private async void OnReleaseDeleted(object sender, VideoReleaseDeletedEventArgs e)
    {
        try
        {
            await SendAsync("removed", new ReleaseDeletedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'removed' event.");
        }
    }

    private async void OnSearchCompleted(object sender, VideoReleaseSearchCompletedEventArgs e)
    {
        try
        {
            await SendAsync("search.completed", new VideoReleaseSearchCompletedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'search.completed' event.");
        }
    }
}
