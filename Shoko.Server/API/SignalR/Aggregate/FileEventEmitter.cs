using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Video.Events;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

public class FileEventEmitter : BaseEventEmitter, IDisposable
{
    private readonly IVideoService _videoService;

    private readonly ILogger<FileEventEmitter> _logger;

    public FileEventEmitter(IHubContext<AggregateHub> hub, IVideoService videoService, ILogger<FileEventEmitter> logger) : base(hub)
    {
        _videoService = videoService;
        _logger = logger;
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

    private async void OnFileDetected(object? sender, VideoFileDetectedEventArgs e)
    {
        try
        {
            await SendAsync("detected", new VideoFileDetectedEventSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'detected' video file event.");
        }
    }

    private async void OnVideoFileHashed(object? sender, VideoFileHashedEventArgs e)
    {
        try
        {
            await SendAsync("hashed", new VideoFileHashedEventSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'hashed' video file event.");
        }
    }

    private async void OnFileRelocated(object? sender, VideoFileRelocatedEventArgs e)
    {
        try
        {
            await SendAsync("relocated", new VideoFileRelocatedEventSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'relocated' video file event.");
        }
    }

    private async void OnFileDeleted(object? sender, VideoFileEventArgs e)
    {
        try
        {
            await SendAsync("deleted", new VideoFileEventSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'deleted' video file event.");
        }
    }
}
