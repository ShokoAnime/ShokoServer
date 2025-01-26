using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Shoko.Server.Utilities.FileSystemWatcher;

namespace Shoko.Server.Services;

public class FileWatcherService
{
    private List<RecoveringFileSystemWatcher> _fileWatchers;
    private readonly ILogger<FileWatcherService> _logger;
    private readonly ISettingsProvider _settingsProvider;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ImportFolderRepository _importFolder;

    public FileWatcherService(ILogger<FileWatcherService> logger, ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory, ImportFolderRepository importFolder)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _schedulerFactory = schedulerFactory;
        _importFolder = importFolder;
        importFolder.ImportFolderSaved += ImportFolderSaved;
    }

    ~FileWatcherService()
    {
        _importFolder.ImportFolderSaved -= ImportFolderSaved;
    }

    private void ImportFolderSaved(object sender, EventArgs e)
    {
        StopWatchingFiles();
        StartWatchingFiles();
    }

    public void StartWatchingFiles()
    {
        _fileWatchers = new List<RecoveringFileSystemWatcher>();
        var settings = _settingsProvider.GetSettings();

        foreach (var share in _importFolder.GetAll())
        {
            try
            {
                if (!share.FolderIsWatched)
                {
                    _logger.LogInformation("ImportFolder found but not watching: {Name} || {Location}", share.ImportFolderName,
                        share.ImportFolderLocation);
                    continue;
                }

                _logger.LogInformation("Watching ImportFolder: {ImportFolderName} || {ImportFolderLocation}", share.ImportFolderName, share.ImportFolderLocation);

                if (!Directory.Exists(share.ImportFolderLocation)) continue;

                _logger.LogInformation("Parsed ImportFolderLocation: {ImportFolderLocation}", share.ImportFolderLocation);

                var fsw = new RecoveringFileSystemWatcher(share.ImportFolderLocation,
                    filters: settings.Import.VideoExtensions.Select(a => "." + a.ToLowerInvariant().TrimStart('.')),
                    pathExclusions: settings.Import.Exclude);
                fsw.Options = new FileSystemWatcherLockOptions
                {
                    Enabled = settings.Import.FileLockChecking,
                    Aggressive = settings.Import.AggressiveFileLockChecking,
                    WaitTimeMilliseconds = settings.Import.FileLockWaitTimeMS,
                    FileAccessMode = share.IsDropSource == 1 ? FileAccess.ReadWrite : FileAccess.Read,
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

    private void FileAdded(object sender, string path)
    {
        if (!File.Exists(path)) return;
        if (!Utils.IsVideo(path)) return;

        _logger.LogInformation("Found file {Path}", path);
        var tup = _importFolder.GetFromFullPath(path);
        if (tup == default)
        {
            _logger.LogWarning("File path could not be parsed into an import folder location: {Path}", path);
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                ShokoEventHandler.Instance.OnFileDetected(tup.Item1, new FileInfo(path));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to run File Detected Event for File: {File}", path);
            }

            try
            {
                var scheduler = await _schedulerFactory.GetScheduler();
                await scheduler.StartJob<DiscoverFileJob>(a => a.FilePath = path).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to Schedule DiscoverFileJob for new file: {File}", path);
            }
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
