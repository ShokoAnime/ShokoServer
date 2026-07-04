using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Events;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services.FileSystemWatcher;
using Shoko.Server.Settings;

#pragma warning disable CS0618
namespace Shoko.Server.Services;

public class FileWatcherService
{
    // Guards every read/write of _fileWatchers. ConfigurationSaved/ManagedFoldersChanged are wired
    // to events (ConfigurationProvider.Saved, ShokoManagedFolderRepository.ManagedFolder*) that are
    // each raised via independent, unsynchronized Task.Run calls, so two rebuild sequences can
    // genuinely run concurrently on different threadpool threads without this lock.
    private readonly object _watchersLock = new();
    private List<RecoveringFileSystemWatcher> _fileWatchers = [];

    private readonly ILogger<FileWatcherService> _logger;

    private readonly ConfigurationProvider<ServerSettings> _settingsProvider;

    private readonly ShokoManagedFolderRepository _managedFolders;

    private readonly FileSystemHelpers _fileSystemHelpers;

    private List<string>? _videoExtensions;

    private IReadOnlyList<Regex>? _excludeExpressions;

    private IVideoService? _videoService;

    public FileWatcherService(ILogger<FileWatcherService> logger, ConfigurationProvider<ServerSettings> settingsProvider, ShokoManagedFolderRepository managedFolders, FileSystemHelpers fileSystemHelpers)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _fileSystemHelpers = fileSystemHelpers;
        _settingsProvider.Saved += ConfigurationSaved;
        _managedFolders = managedFolders;
        _managedFolders.ManagedFolderAdded += ManagedFoldersChanged;
        _managedFolders.ManagedFolderUpdated += ManagedFoldersChanged;
        _managedFolders.ManagedFolderRemoved += ManagedFoldersChanged;
    }

    private void ConfigurationSaved(object? sender, ConfigurationSavedEventArgs<ServerSettings> e)
    {
        if (_excludeExpressions is null || _videoExtensions is null)
            return;
        var settings = e.Configuration;
        if (_excludeExpressions == settings.Import.ExcludeExpressions && _videoExtensions == settings.Import.VideoExtensions)
            return;

        lock (_watchersLock)
        {
            StopWatchingFilesLocked();
            StartWatchingFilesLocked();
        }
    }

    private void ManagedFoldersChanged(object? sender, EventArgs e)
    {
        lock (_watchersLock)
        {
            StopWatchingFilesLocked();
            StartWatchingFilesLocked();
        }
    }

    public void StartWatchingFiles()
    {
        lock (_watchersLock)
        {
            StartWatchingFilesLocked();
        }
    }

    // Must be called while holding _watchersLock.
    private void StartWatchingFilesLocked()
    {
        _videoService ??= ISystemService.StaticServices.GetRequiredService<IVideoService>();
        _fileWatchers = [];
        var settings = _settingsProvider.Load();

        _videoExtensions = settings.Import.VideoExtensions;
        _excludeExpressions = settings.Import.ExcludeExpressions;

        foreach (var share in _managedFolders.GetAll())
        {
            try
            {
                if (!share.IsWatched)
                {
                    _logger.LogInformation("Managed folder found but not watching: {Name} || {Location}", share.Name,
                        share.Path);
                    continue;
                }

                _logger.LogInformation("Watching managed folder: {Name} || {Path}", share.Name, share.Path);

                if (!Directory.Exists(share.Path)) continue;

                _logger.LogInformation("Parsed Path: {Path}", share.Path);

                var fsw = new RecoveringFileSystemWatcher(share.Path,
                    filters: _videoExtensions,
                    pathExclusions: _excludeExpressions,
                    fileSystemHelpers: _fileSystemHelpers);
                fsw.Options = new FileSystemWatcherLockOptions
                {
                    Enabled = settings.Import.FileLockChecking,
                    Aggressive = settings.Import.AggressiveFileLockChecking,
                    WaitTimeMilliseconds = settings.Import.FileLockWaitTimeMS,
                    FileAccessMode = share.IsDropSource ? FileAccess.ReadWrite : FileAccess.Read,
                    AggressiveWaitTimeSeconds = settings.Import.AggressiveFileLockWaitTimeSeconds
                };
                fsw.FileAdded += FileAdded;
                fsw.Start();
                _fileWatchers.Add(fsw);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred initializing the Filesystem Watchers: {Ex}", ex.ToString());
            }
        }
    }

    private void FileAdded(object? sender, string path)
    {
        if (!_videoService!.IsAllowedVideoExtension(path)) return;

        _logger.LogInformation("Found file {Path}", path);
        var tuple = _managedFolders.GetFromAbsolutePath(path);
        if (tuple == default)
        {
            _logger.LogWarning("File path could not be parsed into a managed folder location: {Path}", path);
            return;
        }

        Task.Run(async () =>
        {
            await _videoService!.NotifyVideoFileChangeDetected(path);
        });
    }

    public void AddFileWatcherExclusion(string path)
    {
        RecoveringFileSystemWatcher? watcher;
        lock (_watchersLock)
        {
            watcher = _fileWatchers.FirstOrDefault(a => a.IsPathWatched(path));
        }
        if (watcher is null) return;
        watcher.AddExclusion(path);
        _logger.LogTrace("Added {Path} to filesystem watcher exclusions", path);
    }

    public void RemoveFileWatcherExclusion(string path)
    {
        RecoveringFileSystemWatcher? watcher;
        lock (_watchersLock)
        {
            watcher = _fileWatchers.FirstOrDefault(a => a.IsPathWatched(path));
        }
        if (watcher is null) return;
        watcher.RemoveExclusion(path);
        _logger.LogTrace("Removed {Path} from filesystem watcher exclusions", path);
    }

    public void StopWatchingFiles()
    {
        lock (_watchersLock)
        {
            StopWatchingFilesLocked();
        }
    }

    // Must be called while holding _watchersLock.
    private void StopWatchingFilesLocked()
    {
        if (_fileWatchers.Count == 0)
        {
            return;
        }

        foreach (var fsw in _fileWatchers)
        {
            fsw.Stop();
            fsw.Dispose();
        }

        _fileWatchers.Clear();
    }
}
