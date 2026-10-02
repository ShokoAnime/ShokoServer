using System;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Video.Events;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.Repositories.Cached.AniDB;

namespace Shoko.Server.API.SignalR.Aggregate;

/// <summary>
/// Bridges the video service's file events to the aggregate hub. A file event
/// reaches the users who may see the video: everyone when it is linked to no
/// known anime, otherwise those who may see any anime it is linked to.
/// </summary>
/// <remarks>
/// A detected file is not hashed yet, so it can't be checked, and goes to users
/// without restricted tags only. A deleted file is checked against the series
/// the event captured and the video's links; when it is linked but none of its
/// anime is known any more, it goes to users without restricted tags only.
/// </remarks>
public class FileEventEmitter : BaseEventEmitter, IDisposable
{
    public override string Name => "file";

    private readonly IVideoService _videoService;

    private readonly AniDB_AnimeRepository _anidbAnime;

    private readonly ILogger<FileEventEmitter> _logger;

    public FileEventEmitter(IHubContext<AggregateHub> hub, IVideoService videoService, AniDB_AnimeRepository anidbAnime, ILogger<FileEventEmitter> logger) : base(hub)
    {
        _videoService = videoService;
        _anidbAnime = anidbAnime;
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
            await SendToAudienceAsync(EventAudience.Unrestricted, "detected", new VideoFileDetectedEventSignalRModel(e));
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
            await SendToAudienceAsync(EventAudience.ForVideo(e.Video, _anidbAnime.GetByAnimeID), "hashed", new VideoFileHashedEventSignalRModel(e));
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
            var audience = EventAudience.ForVideo(e.Video, _anidbAnime.GetByAnimeID, () => e.Series.Select(series => series.AnidbAnimeID));
            await SendToAudienceAsync(audience, "relocated", new VideoFileRelocatedEventSignalRModel(e));
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
            var audience = EventAudience.ForVideo(e.Video, _anidbAnime.GetByAnimeID, () => e.Series.Select(series => series.AnidbAnimeID), isRemoval: true);
            await SendToAudienceAsync(audience, "deleted", new VideoFileEventSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'deleted' video file event.");
        }
    }
}
