using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.Services;

namespace Shoko.Plugin.RelocationPlus;

/// <summary>
/// Service responsible for relocating video extra files near the video files.
/// </summary>
public class RelocationPlusService : IHostedService
{
    private readonly ILogger<RelocationPlusService> _logger;

    private readonly IVideoService _videoService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelocationPlusService"/> class.
    /// </summary>
    /// <param name="videoService">The video service.</param>
    /// <param name="logger">The logger.</param>
    public RelocationPlusService(IVideoService videoService, ILogger<RelocationPlusService> logger)
    {
        _logger = logger;
        _videoService = videoService;

        _videoService.VideoFileDeleted += OnVideoDeleted;
        _videoService.VideoFileRelocated += OnVideoRelocated;
    }

    /// <summary>
    /// Dispose of the service.
    /// </summary>
    ~RelocationPlusService()
    {
        _videoService.VideoFileDeleted -= OnVideoDeleted;
        _videoService.VideoFileRelocated -= OnVideoRelocated;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    private void OnVideoRelocated(object? sender, FileRelocatedEventArgs eventArgs)
    {
        var file = eventArgs.PreviousPath;
        var newPath = eventArgs.File.Path;
        if (string.IsNullOrEmpty(file) || string.IsNullOrEmpty(newPath))
            return;

        var extraFiles = FindFilesToMoveOrDelete(file).ToList();
        if (extraFiles.Count == 0)
            return;

        var newDirectory = Path.GetDirectoryName(newPath);
        _logger.LogInformation("Relocating {Count} extra files for file {Path}", extraFiles.Count, eventArgs.File.Path);
        foreach (var (extraAbsolutePath, extraRelativePath, extraFileName, isDirectory) in extraFiles)
        {
            var extraNewAbsolutePath = string.IsNullOrEmpty(extraRelativePath)
                ? string.IsNullOrEmpty(newDirectory) ? extraAbsolutePath : Path.Join(newDirectory, extraFileName)
                : string.IsNullOrEmpty(newDirectory) ? Path.Join("/", extraRelativePath, extraFileName) : Path.Join(newDirectory, extraRelativePath, extraFileName);
            if (string.Equals(extraAbsolutePath, extraNewAbsolutePath, StringComparison.InvariantCulture))
            {
                _logger.LogInformation("Extra file {Path} already exists at {NewPath}, skipping.", extraAbsolutePath, extraNewAbsolutePath);
                continue;
            }

            if (string.Equals(extraAbsolutePath, newPath, StringComparison.InvariantCultureIgnoreCase) && File.Exists(extraNewAbsolutePath))
            {
                _logger.LogInformation("Extra file {Path} already exists at {NewPath}, skipping.", extraAbsolutePath, extraNewAbsolutePath);
                continue;
            }

            try
            {
                if (isDirectory)
                    MoveDirectory(extraAbsolutePath, extraNewAbsolutePath);
                else
                    MoveFile(extraAbsolutePath, extraNewAbsolutePath);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to move extra file {Path} to {NewPath}", extraAbsolutePath, extraNewAbsolutePath);
            }
        }
    }

    private void OnVideoDeleted(object? sender, FileEventArgs eventArgs)
    {
        var file = Path.Join(eventArgs.ManagedFolder.Path, eventArgs.RelativePath);
        if (string.IsNullOrEmpty(file))
            return;

        if (eventArgs.File.IsAvailable)
        {
            _logger.LogInformation("File {Path} is still available, skipping.", eventArgs.File.Path);
            return;
        }

        var extraFiles = FindFilesToMoveOrDelete(file).ToList();
        if (extraFiles.Count == 0)
            return;

        _logger.LogInformation("Deleting {Count} extra files for file {Path}", extraFiles.Count, eventArgs.File.Path);
        foreach (var (absolutePath, _, _, isDirectory) in extraFiles)
        {
            try
            {
                if (isDirectory)
                    Directory.Delete(absolutePath, true);
                else
                    File.Delete(absolutePath);
                _logger.LogInformation("Deleted extra file {Path}", absolutePath);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to delete extra file {Path}", absolutePath);
            }
        }
    }

    private static readonly HashSet<string> _subtitleExtensions = [".srt", ".sub", ".ssa", ".ass", ".smi", ".smil", ".dfxp", ".dvdsub", ".dca", ".pgs", ".pgs", ".pjs", ".vtt", ".webvtt"];

    private static readonly HashSet<string> _audioExtensions = [".mp3", ".ogg", ".wav"];

    private static readonly HashSet<string> _nfoExtensions = [".nfo"];

    private static readonly HashSet<string> _thumbnailExtensions = [".jpg", ".jpeg", ".png", ".gif", ".bmp"];

    private static readonly HashSet<string> _jellyfinTrickplayExtensions = [".trickplay"];

    private void MoveFile(string extraAbsolutePath, string extraNewAbsolutePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(extraAbsolutePath);
        ArgumentException.ThrowIfNullOrEmpty(extraNewAbsolutePath);

        if (!File.Exists(extraAbsolutePath))
            return;

        if (File.Exists(extraNewAbsolutePath))
        {
            File.Delete(extraAbsolutePath);
            _logger.LogInformation("New file {NewPath} already exists, deleted extra file {Path}", extraNewAbsolutePath, extraAbsolutePath);
            return;
        }

        File.Move(extraAbsolutePath, extraNewAbsolutePath);
        _logger.LogInformation("Moved extra file {Path} to {NewPath}", extraAbsolutePath, extraNewAbsolutePath);
    }

    private void MoveDirectory(string extraAbsolutePath, string extraNewAbsolutePath)
    {
        if (Directory.Exists(extraNewAbsolutePath))
        {
            Directory.Delete(extraAbsolutePath, true);
            _logger.LogInformation("New directory {NewPath} already exists, deleted extra directory {Path}", extraNewAbsolutePath, extraAbsolutePath);
            return;
        }

        Directory.Move(extraAbsolutePath, extraNewAbsolutePath);
        _logger.LogInformation("Moved extra directory {Path} to {NewPath}", extraAbsolutePath, extraNewAbsolutePath);
    }

    private IEnumerable<(string extraAbsolutePath, string? extraRelativePath, string extraFileName, bool isDirectory)> FindFilesToMoveOrDelete(string videoAbsolutePath)
    {
        var directory = Path.GetDirectoryName(videoAbsolutePath);
        if (string.IsNullOrEmpty(directory))
            yield break;

        var videoFileName = Path.GetFileName(videoAbsolutePath);
        var potentialExtraFiles = Directory.EnumerateFiles(directory, $"{videoFileName}.*", new EnumerationOptions()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            MaxRecursionDepth = 1,
        })
            .Select(path => (path, extName: Path.GetExtension(path), relativePath: path[(directory.Length + 1)..^Path.GetFileName(path).Length], extraBit: Path.GetFileNameWithoutExtension(path)[videoFileName.Length..]))
            .Where(tuple => tuple.path != videoAbsolutePath && !string.IsNullOrEmpty(tuple.extName) && tuple.path.StartsWith(videoAbsolutePath))
            .ToList();

        foreach (var (absolutePath, extName, relativePath, extraBit) in potentialExtraFiles)
        {
            if (_subtitleExtensions.Contains(extName))
            {
                _logger.LogInformation("Found subtitle file {Path} for {VideoPath}", absolutePath, videoAbsolutePath);
                yield return (absolutePath, relativePath, $"{extraBit}{extName}", false);
            }
            else if (_audioExtensions.Contains(extName))
            {
                _logger.LogInformation("Found audio file {Path} for {VideoPath}", absolutePath, videoAbsolutePath);
                yield return (absolutePath, relativePath, $"{extraBit}{extName}", false);
            }
            else if (_nfoExtensions.Contains(extName))
            {
                _logger.LogInformation("Found nfo file {Path} for {VideoPath}", absolutePath, videoAbsolutePath);
                yield return (absolutePath, relativePath, $"{extraBit}{extName}", false);
            }
            else if (_thumbnailExtensions.Contains(extName))
            {
                _logger.LogInformation("Found thumbnail file {Path} for {VideoPath}", absolutePath, videoAbsolutePath);
                yield return (absolutePath, relativePath, $"{extraBit}{extName}", false);
            }
        }

        var potentialExtraDirectories = Directory.EnumerateDirectories(directory, $"{videoFileName}.*", new EnumerationOptions()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            MaxRecursionDepth = 1,
        })
            .Select(path => (path, extName: Path.GetExtension(path), relativePath: path[(directory.Length + 1)..^Path.GetFileName(path).Length], extraBit: Path.GetFileNameWithoutExtension(path)[videoFileName.Length..]))
            .Where(tuple => tuple.path != videoAbsolutePath && !string.IsNullOrEmpty(tuple.extName) && tuple.path.StartsWith(videoAbsolutePath))
            .ToList();
        foreach (var (absolutePath, extName, relativePath, extraBit) in potentialExtraFiles)
        {
            if (_jellyfinTrickplayExtensions.Contains(extName))
            {
                _logger.LogInformation("Found Jellyfin trickplay file {Path} for {VideoPath}", absolutePath, videoAbsolutePath);
                yield return (absolutePath, relativePath, $"{extraBit}{extName}", true);
            }
        }

        yield break;
    }
}
