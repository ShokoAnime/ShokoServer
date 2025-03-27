using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Plugin.Abstractions.Services;

namespace Shoko.Plugin.ReleaseExporter;

/// <summary>
/// Responsible for exporting releases to the file system and moving or deleting the releases when the video files are moved or deleted, or when the release for the video is deleted.
/// </summary>
public class ReleaseExporter : IHostedService
{
    private readonly ILogger<ReleaseExporter> _logger;

    private readonly IVideoReleaseService _videoReleaseService;

    private readonly IVideoService _videoService;

    private readonly ConfigurationProvider<ReleaseExporterConfiguration> _configProvider;

    private ReleaseExporterConfiguration Configuration { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReleaseExporter"/> class.
    /// </summary>
    /// <param name="videoReleaseService">The video release service.</param>
    /// <param name="videoService">The video service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="configProvider">The configuration provider.</param>
    public ReleaseExporter(IVideoReleaseService videoReleaseService, IVideoService videoService, ILogger<ReleaseExporter> logger, ConfigurationProvider<ReleaseExporterConfiguration> configProvider)
    {
        _logger = logger;
        _videoReleaseService = videoReleaseService;
        _videoService = videoService;
        _configProvider = configProvider;

        Configuration = configProvider.Load();

        _configProvider.Saved += OnConfigurationSaved;
        _videoReleaseService.ReleaseSaved += OnVideoReleaseSaved;
        _videoReleaseService.ReleaseDeleted += OnVideoReleaseDeleted;
        _videoService.VideoFileDeleted += OnVideoDeleted;
        _videoService.VideoFileRelocated += OnVideoRelocated;
    }

    /// <summary>
    /// Disposing of the service.
    /// </summary>
    ~ReleaseExporter()
    {
        _configProvider.Saved -= OnConfigurationSaved;
        _videoReleaseService.ReleaseSaved -= OnVideoReleaseSaved;
        _videoReleaseService.ReleaseDeleted -= OnVideoReleaseDeleted;
        _videoService.VideoFileDeleted -= OnVideoDeleted;
        _videoService.VideoFileRelocated -= OnVideoRelocated;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    private void OnConfigurationSaved(object? sender, ConfigurationSavedEventArgs<ReleaseExporterConfiguration> e)
    {
        Configuration = e.Configuration;
    }

    private void OnVideoReleaseSaved(object? sender, VideoReleaseEventArgs eventArgs)
    {
        if (!Configuration.IsExporterEnabled)
            return;

        if (eventArgs.Video.Locations is not { Count: > 0 } locations)
            return;

        var releaseInfo = JsonConvert.SerializeObject(new ReleaseInfoWithProvider(eventArgs.ReleaseInfo));
        foreach (var location in locations)
        {
            var releasePath = Path.ChangeExtension(location.Path, Configuration.ReleaseExtension);
            try
            {
                if (File.Exists(releasePath))
                {
                    var textData = File.ReadAllText(releasePath);
                    if (string.Equals(textData, releaseInfo, StringComparison.InvariantCulture))
                    {
                        _logger.LogInformation("Release info for {VideoID} already exists at {Path}", eventArgs.Video.ID, releasePath);
                        continue;
                    }
                }

                _logger.LogInformation("Saving release info for {VideoID} at {Path}", eventArgs.Video.ID, releasePath);
                File.WriteAllText(releasePath, releaseInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encountered an error saving release file: {ReleasePath}", releasePath);
            }
        }
    }

    private void OnVideoReleaseDeleted(object? sender, VideoReleaseEventArgs eventArgs)
    {
        if (!Configuration.DeletePhysicalReleaseFiles)
            return;

        if (eventArgs.Video.Locations is not { Count: > 0 } locations)
            return;

        foreach (var location in locations)
        {
            var releasePath = Path.ChangeExtension(location.Path, Configuration.ReleaseExtension);
            try
            {
                if (!File.Exists(releasePath))
                    continue;

                _logger.LogInformation("Deleting release info for {VideoID} at {Path}", eventArgs.Video.ID, releasePath);
                File.Delete(releasePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encountered an error deleting release file: {ReleasePath}", releasePath);
            }
        }
    }

    private void OnVideoRelocated(object? sender, FileMovedEventArgs eventArgs)
    {
        var releasePath = Path.ChangeExtension(eventArgs.PreviousPath, Configuration.ReleaseExtension);
        if (!File.Exists(releasePath))
            return;

        var newReleasePath = Path.ChangeExtension(eventArgs.File.Path, Configuration.ReleaseExtension);
        if (File.Exists(newReleasePath))
        {
            var releaseInfo = File.ReadAllText(releasePath);
            var textData = File.ReadAllText(newReleasePath);
            if (string.Equals(releaseInfo, textData, StringComparison.InvariantCulture))
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
        var releasePath = Path.ChangeExtension(eventArgs.File.Path, Configuration.ReleaseExtension);
        if (!File.Exists(releasePath))
            return;

        _logger.LogInformation("Deleting release info for {VideoID} at {Path}", eventArgs.Video.ID, releasePath);
        File.Delete(releasePath);
    }
}
