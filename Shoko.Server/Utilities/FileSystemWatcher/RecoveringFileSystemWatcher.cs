using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Server.Extensions;

namespace Shoko.Server.Utilities.FileSystemWatcher;

public class RecoveringFileSystemWatcher : IDisposable
{
    private System.IO.FileSystemWatcher _watcher;
    private Timer _recoveringTimer;
    private readonly TimeSpan _directoryFailedRetryInterval = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _directoryRetryInterval = TimeSpan.FromMinutes(5);
    private readonly ILogger _logger;
    private readonly IReadOnlyCollection<string> _filters;
    private readonly IReadOnlyCollection<Regex> _pathExclusions;
    private readonly ConcurrentDictionary<string, WatcherChangeTypes> _buffer = new();
    private readonly ObservableCollection<string> _exclusions = new();
    private readonly string _path;
    private bool ExclusionsEnabled { get; set; }
    public event EventHandler<string> FileAdded;
    public event EventHandler<string> FileDeleted;
    public FileSystemWatcherLockOptions Options { get; set; } = new();

    public RecoveringFileSystemWatcher(string path, IEnumerable<string> filters = null, IEnumerable<string> pathExclusions = null)
    {
        if (path == null) throw new ArgumentException(nameof(path) + " cannot be null");
        if (!Directory.Exists(path)) throw new ArgumentException(nameof(path) + $" must be a directory that exists: {path}");
        // bad, but meh for now
        _logger = Utils.ServiceContainer.GetRequiredService<ILoggerFactory>().CreateLogger("RecoveringFileSystemWatcher:" + path);
        _path = path;
        _filters = filters?.AsReadOnlyCollection() ?? Enumerable.Empty<string>().AsReadOnlyCollection();

        pathExclusions ??= Enumerable.Empty<string>();
        var exclusions = new List<Regex>();
        foreach (var exclusion in pathExclusions)
        {
            try
            {
                var regex = new Regex(exclusion, RegexOptions.Compiled);
                exclusions.Add(regex);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to add Exclusion Regex: {Regex}", exclusion);
            }
        }

        _pathExclusions = exclusions.AsReadOnlyCollection();
    }

    private void OnFileEvent(string path, WatcherChangeTypes type)
    {
        Task.Run(() =>
        {
            try
            {
                path = Utils.EnsureUsablePath(path);
                var resolvedPath = File.ResolveLinkTarget(path, true)?.FullName ?? path;
                if (resolvedPath != path)
                    _logger.LogTrace("File is a symbolic link. Resolved path: {ResolvedFilePath}", resolvedPath);
                switch (type)
                {
                    case WatcherChangeTypes.Created:
                    case WatcherChangeTypes.Changed:
                    case WatcherChangeTypes.Renamed:
                        if (!IsLocked(resolvedPath)) FileAdded?.Invoke(this, path);
                        break;
                    case WatcherChangeTypes.Deleted:
                        FileDeleted?.Invoke(this, path);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(type));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in FileSystemWatcher");
            }
            finally
            {
                _buffer.TryRemove(path, out _);
            }
        });
    }

    private void WatcherChangeDetected(object sender, FileSystemEventArgs e)
    {
        try
        {
            var item = (e.FullPath, Type: e.ChangeType);
            // Exclusion Settings
            if (_pathExclusions.Any(a => a.IsMatch(e.FullPath))) return;
            // Temporary Exclusions, like drop folders
            if (ExclusionsEnabled)
            {
                lock (_exclusions)
                {
                    if (_exclusions.Contains(e.FullPath)) return;
                }
            }
            // handle directories
            if (DirectoryExists(item)) return;
            // Is it a video file
            if (_filters.Any() && !_filters.Any(a => item.FullPath.ToLowerInvariant().EndsWith(a))) return;
            if (_buffer.ContainsKey(item.FullPath)) return;
            _logger.LogTrace("File Event Occurred (not added yet): {Event}, {Path}", e.ChangeType, e.FullPath);
            if (!_buffer.TryAdd(item.FullPath, item.Type)) return;
            OnFileEvent(item.FullPath, item.Type);
        }
        catch (DirectoryNotFoundException)
        {
            // ignore
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "{Ex}", exception);
        }
    }

    private bool DirectoryExists((string FullPath, WatcherChangeTypes Type) item)
    {
        // We get only a single event for directories
        if (!Directory.Exists(item.FullPath)) return false;
        if (item.Type == WatcherChangeTypes.Deleted) return true;
        _logger.LogTrace("New Directory Found. Iterating: {Path}", item.FullPath);
        // iterate and send a command for each containing file
        foreach (var file in Directory.GetFiles(item.FullPath, "*.*", SearchOption.AllDirectories))
        {
            var fileItem = item with { FullPath = file };
            if (_filters.Any() && !_filters.Any(a => fileItem.FullPath.ToLowerInvariant().EndsWith(a))) continue;
            if (_buffer.ContainsKey(fileItem.FullPath)) continue;
            if (!_buffer.TryAdd(fileItem.FullPath, fileItem.Type)) continue;
            OnFileEvent(fileItem.FullPath, fileItem.Type);
        }

        return true;
    }

    private void WatcherOnError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "Error in FileSystemWatcher. Attempting recovery");
        try
        {
            _watcher?.Dispose();
        }
        catch
        {
            // ignore
        }

        try
        {
            _watcher = InitWatcher();
            Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in FileSystemWatcher. Could not recover");
        }
    }

    private void WatcherDisposed(object sender, EventArgs e)
    {
        _watcher = null;
    }

    public void Start()
    {
        if (_watcher is { EnableRaisingEvents: true } && _recoveringTimer != null) return;
        _watcher ??= InitWatcher();
        _recoveringTimer ??= new Timer(_recoveringTimerElapsed);
        _recoveringTimer?.Change(_directoryRetryInterval, Timeout.InfiniteTimeSpan);
        // do this last to ensure the rest is done
        _watcher.EnableRaisingEvents = true;
    }

    private void _recoveringTimerElapsed(object state)
    {
        try
        {
            if (!Directory.Exists(_path))
            {
                _logger.LogWarning("Unable to find {Path}. Retrying in {Time}s", _path, _directoryFailedRetryInterval.TotalSeconds);
                if (_watcher != null)
                    _watcher.EnableRaisingEvents = false;
                _recoveringTimer?.Change(_directoryFailedRetryInterval, Timeout.InfiniteTimeSpan);
                return;
            }

            _watcher ??= InitWatcher();
            Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in FileSystemWatcher. Retrying in {Time}s", _directoryFailedRetryInterval.TotalSeconds);
            if (_watcher != null)
                _watcher.EnableRaisingEvents = false;
            _recoveringTimer?.Change(_directoryFailedRetryInterval, Timeout.InfiniteTimeSpan);
        }
    }


    public void Stop()
    {
        if (_watcher == null) return;
        _watcher.EnableRaisingEvents = false;
    }

    public void AddExclusion(string path)
    {
        lock (_exclusions)
        {
            if (!_exclusions.Contains(path)) _exclusions.Add(path);
        }
        ExclusionsEnabled = true;
    }

    public void RemoveExclusion(string path)
    {
        lock (_exclusions)
        {
            _exclusions.Remove(path);
            ExclusionsEnabled = _exclusions.Any();
        }
    }

    public bool IsPathWatched(string path) => path.Contains(_path);

    private System.IO.FileSystemWatcher InitWatcher()
    {
        var watcher = new System.IO.FileSystemWatcher
        {
            Path = _path,
            NotifyFilter = NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
            IncludeSubdirectories = true,
            InternalBufferSize = 65536, //64KiB
        };

        watcher.Created += WatcherChangeDetected;
        watcher.Changed += WatcherChangeDetected;
        watcher.Renamed += WatcherChangeDetected;
        watcher.Deleted += WatcherChangeDetected;
        watcher.Error += WatcherOnError;
        watcher.Disposed += WatcherDisposed;
        return watcher;
    }

    public void Dispose()
    {
        _recoveringTimer?.Dispose();
        _recoveringTimer = null;
        if (_watcher != null)
        {
            _watcher.Created -= WatcherChangeDetected;
            _watcher.Changed -= WatcherChangeDetected;
            _watcher.Deleted -= WatcherChangeDetected;
            _watcher.Error -= WatcherOnError;
            _watcher.Dispose();
        }
        // this will make it easier to check for disposed without needing to catch it
        _watcher = null;
        FileAdded = null;
        FileDeleted = null;

        GC.SuppressFinalize(this);
    }

    private bool IsLocked(string path)
    {
        if (!Options.Enabled) return false;
        Exception e = null;
        long filesize;
        var numAttempts = 0;
        var aggressive = Options.Aggressive;
        var waitTime = Options.WaitTimeMilliseconds;

        // At least 1s between to ensure that size has the chance to change
        if (waitTime < 1000)
        {
            waitTime = 4000;
        }

        if (!aggressive)
        {
            // Wait 1 minute before giving up on trying to access the file
            while ((filesize = CanAccessFile(path, ref e)) == 0 && numAttempts < 60)
            {
                if (!File.Exists(path))
                {
                    _logger.LogTrace("Failed to access. File no longer exists. Attempt # {NumAttempts}, {FileName}",
                        numAttempts, path);
                    return true;
                }

                numAttempts++;
                Thread.Sleep(waitTime);
                _logger.LogTrace("Failed to access (or filesize is 0). Attempt # {NumAttempts}, {FileName}",
                    numAttempts, path);
            }
        }
        else
        {
            // Wait 1 minute before giving up on trying to access the file
            // first only do read to not get in something's way
            while ((filesize = CanAccessFile(path, ref e)) == 0 && numAttempts < 60)
            {
                if (!File.Exists(path))
                {
                    _logger.LogTrace("Failed to access. File no longer exists. Attempt # {NumAttempts}, {FileName}",
                        numAttempts, path);
                    return true;
                }

                numAttempts++;
                Thread.Sleep(waitTime);
                _logger.LogTrace("Failed to access (or filesize is 0) Attempt # {NumAttempts}, {FileName}",
                    numAttempts, path);
            }

            if (numAttempts >= 60)
            {
                _logger.LogWarning("Could not access file: {Filename}", path);
                return true;
            }

            var seconds = Options.AggressiveWaitTimeSeconds;
            if (seconds < 0)
            {
                seconds = 8;
            }

            numAttempts = 0;

            //For systems with no locking
            while (FileModified(path, seconds, ref filesize, ref e) && numAttempts < 60)
            {
                if (!File.Exists(path))
                {
                    _logger.LogTrace("Failed to access. File no longer exists. Attempt # {NumAttempts}, {FileName}",
                        numAttempts, path);
                    return true;
                }

                numAttempts++;
                Thread.Sleep(waitTime);
                // Only show if it's more than 'seconds' past
                if (numAttempts == 0 || numAttempts * 2 % seconds != 0) continue;

                _logger.LogWarning(
                    "The modified date is too soon. Waiting to ensure that no processes are writing to it. {NumAttempts}/60 {FileName}",
                    numAttempts, path
                );
            }
        }

        if (numAttempts < 60 && filesize != 0) return false;

        _logger.LogWarning("Could not access file: {Filename}", path);
        return true;
    }

    //Added size return, since symbolic links return 0, we use this function also to return the size of the file.
    private long CanAccessFile(string fileName, ref Exception e)
    {
        var accessType = Options.FileAccessMode;
        try
        {
            return GetFileSize(fileName, accessType);
        }
        catch (IOException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            // This shouldn't cause a recursion, as it'll throw if failing
            _logger.LogTrace("File {FileName} is Read-Only. Attempting to unmark", fileName);
            try
            {
                var info = new FileInfo(fileName);
                if (info.IsReadOnly) info.IsReadOnly = false;

                // check to see if it stuck. On linux, we can't just WinAPI hack our way out, so don't recurse in that case, anyway
                if (!new FileInfo(fileName).IsReadOnly && !Utils.IsRunningOnLinuxOrMac()) return GetFileSize(fileName, accessType);
            }
            catch
            {
                // ignore, we tried
            }

            e = ex;
            return 0;
        }
    }

    private static long GetFileSize(string fileName, FileAccess accessType)
    {
        using var fs = File.Open(fileName, FileMode.Open, accessType, FileShare.ReadWrite);
        var size = fs.Seek(0, SeekOrigin.End);
        return size;
    }

    //Used to check if file has been modified within the last X seconds.
    private bool FileModified(string fileName, int seconds, ref long lastFileSize, ref Exception e)
    {
        try
        {
            var lastWrite = File.GetLastWriteTime(fileName);
            var creation = File.GetCreationTime(fileName);
            var now = DateTime.Now;
            // check that the size is also equal, since some copy utilities apply the previous modified date
            var size = CanAccessFile(fileName, ref e);
            if ((lastWrite <= now && lastWrite.AddSeconds(seconds) >= now) ||
                (creation <= now && creation.AddSeconds(seconds) > now) ||
                lastFileSize != size)
            {
                lastFileSize = size;
                return true;
            }
        }
        catch (Exception ex)
        {
            e = ex;
        }

        return false;
    }
}
