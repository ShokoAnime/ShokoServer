using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Shoko.Server.Utilities.FileSystemWatcher;

#nullable enable
namespace Shoko.Server.Services;

public class FileWatcherService
{
    private List<RecoveringFileSystemWatcher> _fileWatchers = [];

    private readonly ILogger<FileWatcherService> _logger;

    private readonly ISettingsProvider _settingsProvider;

    private readonly ShokoManagedFolderRepository _managedFolders;

    private IVideoService? _videoService;

    public FileWatcherService(ILogger<FileWatcherService> logger, ISettingsProvider settingsProvider, ShokoManagedFolderRepository managedFolders)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _managedFolders = managedFolders;
        _managedFolders.ManagedFolderAdded += ManagedFoldersChanged;
        _managedFolders.ManagedFolderUpdated += ManagedFoldersChanged;
        _managedFolders.ManagedFolderRemoved += ManagedFoldersChanged;
    }

    ~FileWatcherService()
    {
        _managedFolders.ManagedFolderAdded -= ManagedFoldersChanged;
        _managedFolders.ManagedFolderUpdated -= ManagedFoldersChanged;
        _managedFolders.ManagedFolderRemoved -= ManagedFoldersChanged;
    }

    private void ManagedFoldersChanged(object? sender, EventArgs e)
    {
        StopWatchingFiles();
        StartWatchingFiles();
    }

    public void StartWatchingFiles()
    {
        _videoService ??= Utils.ServiceContainer.GetRequiredService<IVideoService>();
        _fileWatchers = [];
        var settings = _settingsProvider.GetSettings();

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
                    filters: settings.Import.VideoExtensions.Select(a => "." + a.ToLowerInvariant().TrimStart('.')),
                    pathExclusions: settings.Import.Exclude);
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
        if (!Utils.IsVideo(path)) return;

        _logger.LogInformation("Found file {Path}", path);
        var tuple = _managedFolders.GetFromAbsolutePath(path);
        if (tuple == default)
        {
            _logger.LogWarning("File path could not be parsed into an managed folder location: {Path}", path);
            return;
        }

        Task.Run(async () =>
        {
            await _videoService!.NotifyVideoFileChangeDetected(path);
        });
    }

    public void AddFileWatcherExclusion(string path)
    {
        if (_fileWatchers == null || !_fileWatchers.Any()) return;
        var watcher = _fileWatchers.FirstOrDefault(a => a.IsPathWatched(path));
        watcher?.AddExclusion(path);
        _logger.LogTrace("Added {Path} to filesystem watcher exclusions", path);
    }

    public void RemoveFileWatcherExclusion(string path)
    {
        if (_fileWatchers == null || !_fileWatchers.Any()) return;
        var watcher = _fileWatchers.FirstOrDefault(a => a.IsPathWatched(path));
        watcher?.RemoveExclusion(path);
        _logger.LogTrace("Removed {Path} from filesystem watcher exclusions", path);
    }

    public void StopWatchingFiles()
    {
        if (_fileWatchers == null || !_fileWatchers.Any())
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
