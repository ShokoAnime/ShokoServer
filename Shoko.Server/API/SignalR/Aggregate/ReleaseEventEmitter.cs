using System;
using Microsoft.AspNetCore.SignalR;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.Services;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

public class ReleaseEventEmitter : BaseEventEmitter, IDisposable
{
    private readonly IVideoReleaseService _videoService;

    public ReleaseEventEmitter(IHubContext<AggregateHub> hub, IVideoReleaseService videoReleaseService) : base(hub)
    {
        _videoService = videoReleaseService;
        _videoService.ReleaseSaved += OnReleaseSaved;
        _videoService.ReleaseDeleted += OnReleaseDeleted;
        _videoService.SearchStarted += OnSearchStarted;
        _videoService.SearchCompleted += OnSearchCompleted;
    }

    public void Dispose()
    {
        _videoService.ReleaseSaved -= OnReleaseSaved;
        _videoService.ReleaseDeleted -= OnReleaseDeleted;
        _videoService.SearchStarted -= OnSearchStarted;
        _videoService.SearchCompleted -= OnSearchCompleted;
    }

    private async void OnReleaseSaved(object sender, VideoReleaseSavedEventArgs e)
    {
        await SendAsync("saved", new ReleaseSavedSignalRModel(e));
    }

    private async void OnReleaseDeleted(object sender, VideoReleaseDeletedEventArgs e)
    {
        await SendAsync("removed", new ReleaseDeletedSignalRModel(e));
    }

    private async void OnSearchStarted(object sender, VideoReleaseSearchStartedEventArgs e)
    {
        await SendAsync("search.started", new ReleaseSearchStartedSignalRModel(e));
    }

    private async void OnSearchCompleted(object sender, VideoReleaseSearchCompletedEventArgs e)
    {
        await SendAsync("search.completed", new ReleaseSearchCompletedSignalRModel(e));
    }
}
