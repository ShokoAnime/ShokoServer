using System;
using Microsoft.AspNetCore.SignalR;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

public class FileEventEmitter : BaseEventEmitter, IDisposable
{
    private readonly IVideoService _videoService;

    public FileEventEmitter(IHubContext<AggregateHub> hub, IVideoService videoService) : base(hub)
    {
        _videoService = videoService;
        _videoService.VideoFileDetected += OnFileDetected;
        _videoService.VideoFileHashed += OnFileHashed;
        _videoService.VideoFileRelocated += OnFileRelocated;
        _videoService.VideoFileDeleted += OnFileDeleted;
    }

    public void Dispose()
    {
        _videoService.VideoFileDetected -= OnFileDetected;
        _videoService.VideoFileHashed -= OnFileHashed;
        _videoService.VideoFileRelocated -= OnFileRelocated;
        _videoService.VideoFileDeleted -= OnFileDeleted;
    }

    private async void OnFileDetected(object sender, FileDetectedEventArgs e)
    {
        await SendAsync("detected", new FileDetectedEventSignalRModel(e));
    }

    private async void OnFileHashed(object sender, FileHashedEventArgs e)
    {
        await SendAsync("hashed", new FileHashedEventSignalRModel(e));
    }

    private async void OnFileRelocated(object sender, FileRelocatedEventArgs e)
    {
        await SendAsync("relocated", new FileRelocatedEventSignalRModel(e));
    }

    private async void OnFileDeleted(object sender, FileEventArgs e)
    {
        await SendAsync("deleted", new FileEventSignalRModel(e));
    }
}
