using System;
using System.IO;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Plugin.Abstractions.Services;

namespace Shoko.Plugin.ReleaseExporter;

public class ReleaseExporter
{
    private readonly IVideoReleaseService _videoReleaseService;

    private readonly IVideoService _videoService;

    public ReleaseExporter(IVideoReleaseService videoReleaseService, IVideoService videoService)
    {
        _videoReleaseService = videoReleaseService;
        _videoService = videoService;

        _videoReleaseService.ReleaseSaved += OnVideoReleaseSaved;
        _videoReleaseService.ReleaseDeleted += OnVideoReleaseDeleted;
        _videoService.VideoFileDeleted += OnVideoDeleted;
        _videoService.VideoFileRelocated += OnVideoRelocated;
        _videoReleaseService.ProvidersUpdated += OnReleaseProvidersUpdated;
    }

    ~ReleaseExporter()
    {
        _videoReleaseService.ReleaseSaved -= OnVideoReleaseSaved;
        _videoReleaseService.ReleaseDeleted -= OnVideoReleaseDeleted;
        _videoService.VideoFileDeleted -= OnVideoDeleted;
        _videoService.VideoFileRelocated -= OnVideoRelocated;
        _videoReleaseService.ProvidersUpdated -= OnReleaseProvidersUpdated;
    }

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
                File.Delete(releasePath);
                return;
            }
        }

        File.Move(releasePath, newReleasePath);
    }

    private void OnVideoDeleted(object? sender, FileEventArgs eventArgs)
    {
        var releasePath = Path.ChangeExtension(eventArgs.File.Path, ".release.json");
        if (!File.Exists(releasePath))
            return;

        File.Delete(releasePath);
    }
}
