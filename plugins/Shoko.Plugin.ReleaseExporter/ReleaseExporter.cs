using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Release;
using Shoko.Abstractions.Services;

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

    private readonly ConfigurationProvider<Configuration> _configProvider;

    private int _isExportingAll;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReleaseExporter"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="videoReleaseService">The video release service.</param>
    /// <param name="videoService">The video service.</param>
    /// <param name="configProvider">The configuration provider.</param>
    public ReleaseExporter(ILogger<ReleaseExporter> logger, IApplicationPaths applicationPaths, IVideoReleaseService videoReleaseService, IVideoService videoService, ConfigurationProvider<Configuration> configProvider)
    {
        _logger = logger;
        _applicationPaths = applicationPaths;
        _videoReleaseService = videoReleaseService;
        _videoService = videoService;
        _configProvider = configProvider;
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
    {
        _videoReleaseService.ReleaseSaved += OnVideoReleaseSaved;
        _videoReleaseService.ReleaseDeleted += OnVideoReleaseDeleted;
        _videoService.VideoFileDeleted += OnVideoDeleted;
        _videoService.VideoFileRelocated += OnVideoRelocated;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _videoReleaseService.ReleaseSaved -= OnVideoReleaseSaved;
        _videoReleaseService.ReleaseDeleted -= OnVideoReleaseDeleted;
        _videoService.VideoFileDeleted -= OnVideoDeleted;
        _videoService.VideoFileRelocated -= OnVideoRelocated;
        return Task.CompletedTask;
    }

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
            _applicationPaths.DataPath,
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
        if (!config.IsExporterEnabled || !config.IsRelocationEnabled)
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
            _applicationPaths.DataPath,
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
            _applicationPaths.DataPath,
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

    /// <summary>
    /// Exports release info for all videos to the file system. Only one
    /// instance of this operation can run at a time; concurrent calls will
    /// be skipped.
    /// </summary>
    public ConfigurationActionResult ExportAll()
    {
        var config = _configProvider.Load();
        if (!config.IsExporterEnabled)
            return new("Exporting is disabled!", DisplayColorTheme.Warning);

        if (Interlocked.CompareExchange(ref _isExportingAll, 1, 0) == 1)
            return new("Export all is already running!", DisplayColorTheme.Important);

        Task.Factory.StartNew(ExportAllInternal).ConfigureAwait(false);
        return new("Export Started!");
    }

    private void ExportAllInternal()
    {
        try
        {
            _logger.LogInformation("Started exporting release info for all videos.");
            var config = _configProvider.Load();
            var totalExported = 0;
            var totalSkipped = 0;
            var totalErrored = 0;
            var totalReadOnly = 0;
            foreach (var video in _videoService.GetAllVideos())
            {
                if (video.ReleaseInfo is not { } releaseInfo)
                    continue;

                if (video.Files is not { Count: > 0 } locations)
                    continue;

                var releaseLocations = locations.SelectMany(l => config.GetReleaseFilePaths(_applicationPaths, l.ManagedFolder, video, l.RelativePath)).ToHashSet();
                var serializedReleaseInfo = JsonConvert.SerializeObject(new ReleaseInfoWithProvider(releaseInfo));
                foreach (var releasePath in releaseLocations)
                {
                    try
                    {
                        if (File.Exists(releasePath))
                        {
                            var textData = File.ReadAllText(releasePath);
                            if (string.Equals(textData, serializedReleaseInfo, StringComparison.Ordinal))
                            {
                                _logger.LogTrace("Release info for {VideoID} already exists at {Path}", video.ID, releasePath);
                                totalSkipped++;
                                continue;
                            }
                        }

                        var releaseDirectory = Path.GetDirectoryName(releasePath);
                        if (!string.IsNullOrEmpty(releaseDirectory) && !Directory.Exists(releaseDirectory))
                            Directory.CreateDirectory(releaseDirectory);

                        File.WriteAllText(releasePath, serializedReleaseInfo);
                        _logger.LogInformation("Saved release info for {VideoID} at {Path}", video.ID, releasePath);
                        totalExported++;
                    }
                    catch (IOException ex) when (ex.Message.StartsWith("Read-only file system", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Abort if this is a full or partial read-only file system.
                        if (totalReadOnly >= 50 && totalErrored == 0 && totalSkipped == 0)
                        {
                            _logger.LogWarning("Failed to save release files due to read-only file system.");
                            return;
                        }

                        totalReadOnly++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Encountered an unexpected error saving release file: {ReleasePath}", releasePath);
                        totalErrored++;
                    }
                }
            }

            _logger.LogInformation("Export all completed. Exported: {Exported}, Skipped: {Skipped}, Errored: {Errored}, Read-Only Warnings: {ReadOnly}", totalExported, totalSkipped, totalErrored, totalReadOnly);
        }
        finally
        {
            Interlocked.Exchange(ref _isExportingAll, 0);
        }
    }
}
