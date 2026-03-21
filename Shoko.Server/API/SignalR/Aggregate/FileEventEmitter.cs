using System;
using Microsoft.AspNetCore.SignalR;
using Shoko.Abstractions.Video.Events;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

public class FileEventEmitter : BaseEventEmitter, IDisposable
{
    private readonly IVideoService _videoService;

    public FileEventEmitter(IHubContext<AggregateHub> hub, IVideoService videoService) : base(hub)
    {
        _videoService = videoService;
        _videoService.VideoFileDetected += OnFileDetected;
        _videoService.VideoFileHashed += OnVideoFileHashed;
        _videoService.VideoFileRelocated += OnFileRelocated;
        _videoService.VideoFileDeleted += OnFileDeleted;
    }

    public void Dispose()
    {
        _videoService.VideoFileDetected -= OnFileDetected;
        _videoService.VideoFileHashed -= OnVideoFileHashed;
        _videoService.VideoFileRelocated -= OnFileRelocated;
        _videoService.VideoFileDeleted -= OnFileDeleted;
    }

    private async void OnFileDetected(object sender, VideoFileDetectedEventArgs e)
    {
        await SendAsync("detected", new VideoFileDetectedEventSignalRModel(e));
    }

    private async void OnVideoFileHashed(object sender, VideoFileHashedEventArgs e)
    {
        await SendAsync("hashed", new VideoFileHashedEventSignalRModel(e));
    }

    private async void OnFileRelocated(object sender, VideoFileRelocatedEventArgs e)
    {
        await SendAsync("relocated", new VideoFileRelocatedEventSignalRModel(e));
    }

    private async void OnFileDeleted(object sender, VideoFileEventArgs e)
    {
        await SendAsync("deleted", new VideoFileEventSignalRModel(e));
    }
}
