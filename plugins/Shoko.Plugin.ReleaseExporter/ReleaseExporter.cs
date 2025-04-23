using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions;
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

    private readonly IApplicationPaths _applicationPaths;

    private readonly IVideoReleaseService _videoReleaseService;

    private readonly IVideoService _videoService;

    private readonly ConfigurationProvider<ReleaseExporterConfiguration> _configProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReleaseExporter"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="videoReleaseService">The video release service.</param>
    /// <param name="videoService">The video service.</param>
    /// <param name="configProvider">The configuration provider.</param>
    public ReleaseExporter(ILogger<ReleaseExporter> logger, IApplicationPaths applicationPaths, IVideoReleaseService videoReleaseService, IVideoService videoService, ConfigurationProvider<ReleaseExporterConfiguration> configProvider)
    {
        _logger = logger;
        _applicationPaths = applicationPaths;
        _videoReleaseService = videoReleaseService;
        _videoService = videoService;
        _configProvider = configProvider;

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

    private void OnVideoReleaseSaved(object? sender, VideoReleaseSavedEventArgs eventArgs)
    {
        var config = _configProvider.Load();
        if (!config.IsExporterEnabled)
            return;

        if (eventArgs.Video.Files is not { Count: > 0 } locations)
            return;
        var releaseLocations = locations.SelectMany(l => config.GetReleaseFilePaths(_applicationPaths, l.ManagedFolder, eventArgs.Video, l.RelativePath)).ToHashSet();
        var releaseInfo = JsonConvert.SerializeObject(new ReleaseInfoWithProvider(eventArgs.ReleaseInfo));
        foreach (var releasePath in releaseLocations)
        {
            try
            {
                if (File.Exists(releasePath))
                {
                    var textData = File.ReadAllText(releasePath);
                    if (string.Equals(textData, releaseInfo, StringComparison.Ordinal))
                    {
                        _logger.LogInformation("Release info for {VideoID} already exists at {Path}", eventArgs.Video.ID, releasePath);
                        continue;
                    }
                }

                var releaseDirectory = Path.GetDirectoryName(releasePath);
                if (!string.IsNullOrEmpty(releaseDirectory) && !Directory.Exists(releaseDirectory))
                    Directory.CreateDirectory(releaseDirectory);

                _logger.LogInformation("Saving release info for {VideoID} at {Path}", eventArgs.Video.ID, releasePath);
                File.WriteAllText(releasePath, releaseInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encountered an error saving release file: {ReleasePath}", releasePath);
            }
        }
    }

    private void OnVideoReleaseDeleted(object? sender, VideoReleaseDeletedEventArgs eventArgs)
    {
        var config = _configProvider.Load();
        if (!config.IsExporterEnabled)
            return;

        if (!config.DeletePhysicalReleaseFiles)
            return;

        if (eventArgs.Video?.Files is not { Count: > 0 } locations)
            return;

        var stops = new HashSet<string>([
            _applicationPaths.ProgramDataPath,
            .. _videoService.GetAllManagedFolders().Select(m => m.Path)
        ]);
        var releaseLocations = locations.SelectMany(l => config.GetReleaseFilePaths(_applicationPaths, l.ManagedFolder, eventArgs.Video, l.RelativePath)).ToHashSet();
        foreach (var releasePath in releaseLocations)
        {
            try
            {
                if (!File.Exists(releasePath))
                    continue;

                _logger.LogInformation("Deleting release info for {VideoID} at {Path}", eventArgs.Video.ID, releasePath);
                File.Delete(releasePath);

                var releaseDirectory = Path.GetDirectoryName(releasePath);
                while (!string.IsNullOrEmpty(releaseDirectory) && !stops.Contains(releaseDirectory) && Directory.Exists(releaseDirectory) && !Directory.EnumerateFileSystemEntries(releaseDirectory).Any())
                {
                    Directory.Delete(releaseDirectory);
                    releaseDirectory = Path.GetDirectoryName(releaseDirectory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encountered an error deleting release file: {ReleasePath}", releasePath);
            }
        }
    }

    private void OnVideoRelocated(object? sender, FileRelocatedEventArgs eventArgs)
    {
        var config = _configProvider.Load();
        if (!config.IsRelocationEnabled)
            return;

        var otherLocations = eventArgs.Video.Files
            .ExceptBy([eventArgs.File.ID], l => l.ID)
            .SelectMany(l => config.GetReleaseFilePaths(_applicationPaths, l.ManagedFolder, eventArgs.Video, l.RelativePath))
            .ToHashSet();
        var oldReleasePaths = otherLocations.Concat(config.GetReleaseFilePaths(_applicationPaths, eventArgs.PreviousManagedFolder, eventArgs.Video, eventArgs.PreviousRelativePath)).ToHashSet();
        var newReleasePaths = otherLocations.Concat(config.GetReleaseFilePaths(_applicationPaths, eventArgs.File.ManagedFolder, eventArgs.Video, eventArgs.File.RelativePath)).ToHashSet();
        var removedPaths = oldReleasePaths.Except(newReleasePaths).ToList();
        var addedPaths = newReleasePaths.Except(oldReleasePaths).ToList();
        var overlapPaths = oldReleasePaths.Intersect(newReleasePaths).ToList();

        var releaseInfo = (string?)null;
        if (eventArgs.Video.ReleaseInfo is { } r)
        {
            _logger.LogTrace("Found release info in database. (Video={VideoID})", eventArgs.Video.ID);
            releaseInfo = JsonConvert.SerializeObject(new ReleaseInfoWithProvider(r));
        }
        else
        {
            var lastUpdated = (DateTime?)null;
            foreach (var releasePath in overlapPaths.Concat(removedPaths))
            {
                try
                {
                    if (!File.Exists(releasePath))
                        continue;

                    if (lastUpdated is null || (File.GetLastWriteTime(releasePath) > lastUpdated))
                    {
                        var newReleaseInfo = File.ReadAllText(releasePath);
                        if (!string.IsNullOrEmpty(newReleaseInfo))
                        {
                            if (lastUpdated is null)
                                _logger.LogTrace("Found release info at {Path}. (Video={VideoID})", releasePath, eventArgs.Video.ID);
                            else
                                _logger.LogTrace("Found newer release info at {Path}. (Video={VideoID})", releasePath, eventArgs.Video.ID);

                            lastUpdated = File.GetLastWriteTime(releasePath);
                            releaseInfo = newReleaseInfo;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Encountered an error reading release file: {ReleasePath} (Video={VideoID})", releasePath, eventArgs.Video.ID);
                }
            }
        }

        var stops = new HashSet<string>([
            _applicationPaths.ProgramDataPath,
            .. _videoService.GetAllManagedFolders().Select(m => m.Path)
        ]);
        foreach (var releasePath in removedPaths)
        {
            try
            {
                if (!File.Exists(releasePath))
                    continue;

                _logger.LogInformation("Deleting release info for {VideoID} at {Path}", eventArgs.Video.ID, releasePath);
                File.Delete(releasePath);

                var releaseDirectory = Path.GetDirectoryName(releasePath);
                while (!string.IsNullOrEmpty(releaseDirectory) && !stops.Contains(releaseDirectory) && Directory.Exists(releaseDirectory) && !Directory.EnumerateFileSystemEntries(releaseDirectory).Any())
                {
                    Directory.Delete(releaseDirectory);
                    releaseDirectory = Path.GetDirectoryName(releaseDirectory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encountered an error deleting release file: {ReleasePath}", releasePath);
            }
        }

        foreach (var releasePath in overlapPaths.Concat(addedPaths))
        {
            if (string.IsNullOrEmpty(releaseInfo))
                continue;

            try
            {
                if (File.Exists(releasePath))
                {
                    var textData = File.ReadAllText(releasePath);
                    if (string.Equals(textData, releaseInfo, StringComparison.Ordinal))
                    {
                        _logger.LogInformation("Release info for {VideoID} already exists at {Path}", eventArgs.Video.ID, releasePath);
                        continue;
                    }
                }

                var releaseDirectory = Path.GetDirectoryName(releasePath);
                if (!string.IsNullOrEmpty(releaseDirectory) && !Directory.Exists(releaseDirectory))
                    Directory.CreateDirectory(releaseDirectory);

                _logger.LogInformation("Saving release info for {VideoID} at {Path}", eventArgs.Video.ID, releasePath);
                File.WriteAllText(releasePath, releaseInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encountered an error saving release file: {ReleasePath}", releasePath);
            }
        }
    }

    private void OnVideoDeleted(object? sender, FileEventArgs eventArgs)
    {
        var config = _configProvider.Load();
        if (!config.IsRelocationEnabled)
            return;

        var pathsToKeep = eventArgs.Video.Files
            .ExceptBy([eventArgs.File.ID], l => l.ID)
            .SelectMany(l => config.GetReleaseFilePaths(_applicationPaths, l.ManagedFolder, eventArgs.Video, l.RelativePath))
            .ToHashSet();
        var pathsToRemove = config.GetReleaseFilePaths(_applicationPaths, eventArgs.File.ManagedFolder, eventArgs.Video, eventArgs.File.RelativePath)
            .Except(pathsToKeep)
            .ToList();

        _logger.LogInformation(
            "Found {Count} release files to remove for video file at {Path} (Video={VideoID},ManagedFolder={ManagedFolder},RelativePath={RelativePath})",
            pathsToRemove.Count,
            eventArgs.File.Path,
            eventArgs.Video.ID,
            eventArgs.File.ManagedFolder,
            eventArgs.File.RelativePath
        );
        var stops = new HashSet<string>([
            _applicationPaths.ProgramDataPath,
            .. _videoService.GetAllManagedFolders().Select(m => m.Path)
        ]);
        foreach (var releasePath in pathsToRemove)
        {
            try
            {
                if (!File.Exists(releasePath))
                    continue;

                _logger.LogInformation("Deleting release info for {VideoID} at {Path}", eventArgs.Video.ID, releasePath);
                File.Delete(releasePath);

                var releaseDirectory = Path.GetDirectoryName(releasePath);
                while (!string.IsNullOrEmpty(releaseDirectory) && !stops.Contains(releaseDirectory) && Directory.Exists(releaseDirectory) && !Directory.EnumerateFileSystemEntries(releaseDirectory).Any())
                {
                    Directory.Delete(releaseDirectory);
                    releaseDirectory = Path.GetDirectoryName(releaseDirectory);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Encountered an error deleting release file: {ReleasePath}", releasePath);
            }
        }
    }
}
