using System.Linq;
using Shoko.Plugin.Abstractions.Events;
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

        _videoReleaseService.VideoReleaseSaved += OnVideoReleaseSaved;
        _videoReleaseService.VideoReleaseDeleted += OnVideoReleaseDeleted;
        _videoService.VideoFileDeleted += OnVideoDeleted;
        _videoService.VideoFileRenamed += OnVideoRelocated;
        _videoService.VideoFileMoved += OnVideoRelocated;
    }

    ~ReleaseExporter()
    {
        _videoReleaseService.VideoReleaseSaved -= OnVideoReleaseSaved;
        _videoReleaseService.VideoReleaseDeleted -= OnVideoReleaseDeleted;
        _videoService.VideoFileDeleted -= OnVideoDeleted;
        _videoService.VideoFileRenamed -= OnVideoRelocated;
        _videoService.VideoFileMoved -= OnVideoRelocated;
    }

    private bool IsEnabled
        => _videoReleaseService.GetAvailableProviders().Any(x => x.Provider.Name == ReleaseImporter.Key && x.Enabled);

    private void OnVideoReleaseSaved(object? sender, VideoReleaseEventArgs eventArgs)
    {
        if (!IsEnabled)
            return;

        // replace or create the json file
    }

    private void OnVideoReleaseDeleted(object? sender, VideoReleaseEventArgs eventArgs)
    {
        // delete the json file
    }

    private void OnVideoRelocated(object? sender, FileMovedEventArgs eventArgs)
    {
        // move the json file
    }

    private void OnVideoRelocated(object? sender, FileRenamedEventArgs eventArgs)
    {
        // move the json file
    }

    private void OnVideoDeleted(object? sender, FileEventArgs eventArgs)
    {
        // delete the json file
    }
}
