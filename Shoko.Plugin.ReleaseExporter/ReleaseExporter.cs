using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Plugin.Abstractions.Services;

namespace Shoko.Plugin.ReleaseExporter;

public class ReleaseExporter : IHostedService
{
    private readonly ILogger<ReleaseExporter> _logger;

    private readonly IVideoReleaseService _videoReleaseService;

    private readonly IVideoService _videoService;

    public ReleaseExporter(IVideoReleaseService videoReleaseService, IVideoService videoService, ILogger<ReleaseExporter> logger)
    {
        _logger = logger;
        _videoReleaseService = videoReleaseService;
        _videoService = videoService;

        _videoReleaseService.ReleaseSaved += OnVideoReleaseSaved;
        _videoReleaseService.ReleaseDeleted += OnVideoReleaseDeleted;
        _videoService.VideoFileDeleted += OnVideoDeleted;
        _videoService.VideoFileRelocated += OnVideoRelocated;
        _videoReleaseService.ProvidersUpdated += OnReleaseProvidersUpdated;

        _logger.LogInformation("ReleaseExporter initialized");
    }

    ~ReleaseExporter()
    {
        _videoReleaseService.ReleaseSaved -= OnVideoReleaseSaved;
        _videoReleaseService.ReleaseDeleted -= OnVideoReleaseDeleted;
        _videoService.VideoFileDeleted -= OnVideoDeleted;
        _videoService.VideoFileRelocated -= OnVideoRelocated;
        _videoReleaseService.ProvidersUpdated -= OnReleaseProvidersUpdated;
    }

    public Task StartAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    private bool IsEnabled { get; set; } = false;

    private void OnReleaseProvidersUpdated(object? sender, EventArgs e)
    {
        IsEnabled = _videoReleaseService.GetProviderInfo<ReleaseImporter>().Enabled;
    }

    private void OnVideoReleaseSaved(object? sender, VideoReleaseEventArgs eventArgs)
    {
        if (!IsEnabled)
            return;

        if (eventArgs.Video.Locations is not { Count: > 0 } locations)
            return;

        var releaseInfo = JsonConvert.SerializeObject(new ReleaseInfoWithProvider(eventArgs.ReleaseInfo));
        foreach (var location in locations)
        {
            var releasePath = Path.ChangeExtension(location.Path, ".release.json");
            if (File.Exists(releasePath))
            {
                var textData = File.ReadAllText(releasePath);
                if (textData == releaseInfo)
                    continue;
            }

            _logger.LogInformation("Saving release info for {VideoID} at {Path}", eventArgs.Video.ID, releasePath);
            File.WriteAllText(releasePath, releaseInfo);
        }
    }

    private void OnVideoReleaseDeleted(object? sender, VideoReleaseEventArgs eventArgs)
    {
        if (eventArgs.Video.Locations is not { Count: > 0 } locations)
            return;

        foreach (var location in locations)
        {
            var releasePath = Path.ChangeExtension(location.Path, ".release.json");
            if (!File.Exists(releasePath))
                continue;

            _logger.LogInformation("Deleting release info for {VideoID} at {Path}", eventArgs.Video.ID, releasePath);
            File.Delete(releasePath);
        }
    }

    private void OnVideoRelocated(object? sender, FileMovedEventArgs eventArgs)
    {
        var releasePath = Path.ChangeExtension(eventArgs.PreviousPath, ".release.json");
        if (!File.Exists(releasePath))
            return;

        var newReleasePath = Path.ChangeExtension(eventArgs.File.Path, ".release.json");
        if (File.Exists(newReleasePath))
        {
            var releaseInfo = File.ReadAllText(releasePath);
            var textData = File.ReadAllText(releasePath);
            if (textData == releaseInfo)
            {
                _logger.LogInformation("Deleting duplicate release info for {VideoID} at {Path}", eventArgs.Video.ID, releasePath);
                File.Delete(releasePath);
                return;
            }
        }

        _logger.LogInformation("Relocating release info for {VideoID} from {Path} to {NewPath}", eventArgs.Video.ID, releasePath, newReleasePath);
        File.Move(releasePath, newReleasePath);
    }

    private void OnVideoDeleted(object? sender, FileEventArgs eventArgs)
    {
        var releasePath = Path.ChangeExtension(eventArgs.File.Path, ".release.json");
        if (!File.Exists(releasePath))
            return;

        _logger.LogInformation("Deleting release info for {VideoID} at {Path}", eventArgs.Video.ID, releasePath);
        File.Delete(releasePath);
    }
}
