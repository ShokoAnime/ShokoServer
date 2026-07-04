using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Utilities;

#pragma warning disable CS0618
namespace Shoko.Server.Services.FileSystemWatcher;

public class RecoveringFileSystemWatcher : IDisposable
{
    private const int MaxConcurrentFileChecks = 8;

    private readonly record struct WatcherEvent(string Path, WatcherChangeTypes Type, bool IsDirectory);

    // Guards every read/write of _watcher and _recoveringTimer. These fields are touched from the
    // watcher's own callback thread (via WatcherOnError), the recovering Timer's threadpool callback,
    // and whichever thread calls Start()/Stop()/Dispose() - without this lock those can race and,
    // e.g., re-enable or re-dispose an already-disposed native watcher.
    private readonly object _watcherLock = new();
    private System.IO.FileSystemWatcher? _watcher;
    private Timer? _recoveringTimer;
    private CancellationTokenSource? _consumerCts;
    private Task? _consumerTask;

    private readonly TimeSpan _directoryFailedRetryInterval = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _directoryRetryInterval = TimeSpan.FromMinutes(5);
    private readonly ILogger _logger;
    private readonly FileSystemHelpers _fileSystemHelpers;
    private readonly IReadOnlyCollection<string> _filters;
    private readonly IReadOnlyCollection<Regex> _pathExclusions;
    private readonly ConcurrentDictionary<string, byte> _buffer = new();

    // Raw watcher callbacks only do cheap filtering and enqueue here; all heavy work (directory
    // expansion, lock-checking) happens on the bounded async consumers below, off the watcher's
    // own event thread.
    private readonly Channel<WatcherEvent> _channel = Channel.CreateUnbounded<WatcherEvent>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly ObservableCollection<string> _exclusions = new();
    private readonly string _path;
    private bool ExclusionsEnabled { get; set; }
    public event EventHandler<string>? FileAdded;
    public event EventHandler<string>? FileDeleted;
    public FileSystemWatcherLockOptions Options { get; set; } = new();

    public RecoveringFileSystemWatcher(string path, IReadOnlyCollection<string> filters, IReadOnlyCollection<Regex> pathExclusions, FileSystemHelpers fileSystemHelpers)
    {
        if (path == null) throw new ArgumentException(nameof(path) + " cannot be null");
        if (!Directory.Exists(path)) throw new ArgumentException(nameof(path) + $" must be a directory that exists: {path}");
        // bad, but meh for now
        _logger = ISystemService.StaticServices.GetRequiredService<ILoggerFactory>().CreateLogger("RecoveringFileSystemWatcher:" + path);
        _path = path;
        _filters = filters;
        _pathExclusions = pathExclusions;
        _fileSystemHelpers = fileSystemHelpers;
    }

    private void WatcherChangeDetected(object sender, FileSystemEventArgs e)
    {
        try
        {
            var fullPath = e.FullPath;
            // Exclusion Settings
            if (_pathExclusions.Any(a => a.IsMatch(fullPath))) return;
            // Temporary Exclusions, like drop folders
            if (ExclusionsEnabled)
            {
                lock (_exclusions)
                {
                    if (_exclusions.Contains(fullPath)) return;
                }
            }

            // Handle directories: we get only a single event for them. Don't walk the tree here -
            // that can be arbitrarily slow and must not block the watcher's own callback thread.
            // Hand it off to a consumer instead.
            if (Directory.Exists(fullPath))
            {
                if (e.ChangeType == WatcherChangeTypes.Deleted) return;
                if (!_buffer.TryAdd(fullPath, 0)) return;
                _logger.LogTrace("New Directory Found. Queuing for expansion: {Path}", fullPath);
                Enqueue(new WatcherEvent(fullPath, e.ChangeType, true));
                return;
            }

            // Is it a video file
            if (_filters.Count > 0 && !_filters.Any(a => fullPath.EndsWith(a, StringComparison.OrdinalIgnoreCase))) return;
            if (!_buffer.TryAdd(fullPath, 0)) return;
            _logger.LogTrace("File Event Occurred (not added yet): {Event}, {Path}", e.ChangeType, fullPath);
            Enqueue(new WatcherEvent(fullPath, e.ChangeType, false));
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

    private void Enqueue(WatcherEvent item)
    {
        // Unbounded writer; TryWrite only fails once the channel has been completed (i.e. during
        // Dispose), in which case just drop the buffer reservation we took for it.
        if (!_channel.Writer.TryWrite(item))
            _buffer.TryRemove(item.Path, out _);
    }

    // Parallel.ForEachAsync pulls items off the channel one at a time (safe with SingleReader) and
    // dispatches up to MaxConcurrentFileChecks of them to run concurrently. This matters because
    // ProcessDirectoryAsync/ProcessFileAsync usually complete without ever actually suspending
    // (e.g. an already-unlocked file returns before hitting any `await`) - a hand-rolled
    // "SemaphoreSlim + fire-and-forget Task" consumer would run those synchronously on the single
    // reader thread instead of achieving real concurrency, which Parallel.ForEachAsync avoids.
    private Task RunConsumerAsync(CancellationToken token) =>
        Parallel.ForEachAsync(_channel.Reader.ReadAllAsync(token), new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentFileChecks, CancellationToken = token }, ProcessAsync);

    private ValueTask ProcessAsync(WatcherEvent item, CancellationToken token) =>
        new(item.IsDirectory ? ProcessDirectoryAsync(item, token) : ProcessFileAsync(item, token));

    private async Task ProcessDirectoryAsync(WatcherEvent item, CancellationToken token)
    {
        try
        {
            _logger.LogTrace("New Directory Found. Iterating: {Path}", item.Path);
            // iterate and queue a command for each containing file
            bool IsMatch(string p, bool isDirectory) => isDirectory || (!_pathExclusions.Any(a => a.IsMatch(p)) && _buffer.TryAdd(p, 0));
            foreach (var file in _fileSystemHelpers.GetFilePaths(item.Path, recursive: true, extensions: _filters, filter: IsMatch, cancellationToken: token))
                Enqueue(new WatcherEvent(file, item.Type, false));
        }
        catch (OperationCanceledException)
        {
            // expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error expanding directory {Path}", item.Path);
        }
        finally
        {
            _buffer.TryRemove(item.Path, out _);
        }
    }

    private async Task ProcessFileAsync(WatcherEvent item, CancellationToken token)
    {
        try
        {
            var usablePath = PlatformUtility.EnsureUsablePath(item.Path);
            var resolvedPath = File.Exists(usablePath)
                ? File.ResolveLinkTarget(usablePath, true)?.FullName
                : null;
            if (string.IsNullOrEmpty(resolvedPath))
                resolvedPath = usablePath;
            else
                _logger.LogTrace("File is a symbolic link. Resolved path: {ResolvedFilePath}", resolvedPath);

            switch (item.Type)
            {
                case WatcherChangeTypes.Created:
                case WatcherChangeTypes.Changed:
                case WatcherChangeTypes.Renamed:
                    if (!await IsLockedAsync(resolvedPath, token))
                        FileAdded?.Invoke(this, usablePath);
                    break;
                case WatcherChangeTypes.Deleted:
                    FileDeleted?.Invoke(this, usablePath);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(item.Type));
            }
        }
        catch (OperationCanceledException)
        {
            // expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in FileSystemWatcher");
        }
        finally
        {
            // Always key off the original, un-mutated path - it's what was used to reserve the
            // _buffer slot in WatcherChangeDetected/ProcessDirectoryAsync.
            _buffer.TryRemove(item.Path, out _);
        }
    }

    private void WatcherOnError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "Error in FileSystemWatcher. Attempting recovery");
        // Never dispose/reinit synchronously from here: this callback runs on the watcher's own
        // internal event thread (on Linux/macOS this is a dedicated thread reading from
        // inotify/FSEvents). Disposing a FileSystemWatcher from within its own event thread can
        // hang (self-join) or crash the process on those platforms, so recovery must happen on an
        // unrelated thread.
        Task.Run(RecoverWatcher);
    }

    private void RecoverWatcher()
    {
        lock (_watcherLock)
        {
            try
            {
                _watcher?.Dispose();
            }
            catch
            {
                // ignore
            }

            _watcher = null;

            try
            {
                _watcher = InitWatcher();
                StartLocked();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in FileSystemWatcher. Could not recover");
            }
        }
    }

    public void Start()
    {
        lock (_watcherLock)
        {
            StartLocked();
        }
    }

    // Must be called while holding _watcherLock.
    private void StartLocked()
    {
        if (_watcher is { EnableRaisingEvents: true } && _recoveringTimer != null && _consumerTask is { IsCompleted: false })
            return;

        _watcher ??= InitWatcher();
        _recoveringTimer ??= new Timer(RecoveringTimerElapsed);
        _recoveringTimer.Change(_directoryRetryInterval, Timeout.InfiniteTimeSpan);
        EnsureConsumerRunningLocked();
        // do this last to ensure the rest is done
        _watcher.EnableRaisingEvents = true;
    }

    // Must be called while holding _watcherLock.
    private void EnsureConsumerRunningLocked()
    {
        if (_consumerTask is { IsCompleted: false }) return;
        _consumerCts?.Dispose();
        _consumerCts = new CancellationTokenSource();
        _consumerTask = Task.Run(() => RunConsumerAsync(_consumerCts.Token));
    }

    private void RecoveringTimerElapsed(object? state)
    {
        lock (_watcherLock)
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

                StartLocked();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in FileSystemWatcher. Retrying in {Time}s", _directoryFailedRetryInterval.TotalSeconds);
                if (_watcher != null)
                    _watcher.EnableRaisingEvents = false;
                _recoveringTimer?.Change(_directoryFailedRetryInterval, Timeout.InfiniteTimeSpan);
            }
        }
    }

    public void Stop()
    {
        lock (_watcherLock)
        {
            if (_watcher != null)
                _watcher.EnableRaisingEvents = false;
            // Otherwise the recovering timer would silently re-Start() us a few minutes later.
            _recoveringTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
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
        // Deferred rather than immediate: the OS can deliver a change notification for `path`
        // slightly after the caller's FS operation returns (and thus after they call this), so
        // removing the exclusion right away can let that late event slip through and re-trigger
        // processing of a file we just finished handling. A short grace period is cheap defense in
        // depth; callers don't wait on this method today, so deferring the actual removal is
        // transparent to them.
        _ = Task.Delay(TimeSpan.FromSeconds(2)).ContinueWith(_ =>
        {
            lock (_exclusions)
            {
                _exclusions.Remove(path);
                ExclusionsEnabled = _exclusions.Any();
            }
        }, TaskScheduler.Default);
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
        return watcher;
    }

    public void Dispose()
    {
        CancellationTokenSource? cts;
        Task? consumerTask;
        lock (_watcherLock)
        {
            _recoveringTimer?.Dispose();
            _recoveringTimer = null;

            if (_watcher != null)
            {
                _watcher.Created -= WatcherChangeDetected;
                _watcher.Changed -= WatcherChangeDetected;
                _watcher.Renamed -= WatcherChangeDetected;
                _watcher.Deleted -= WatcherChangeDetected;
                _watcher.Error -= WatcherOnError;
                _watcher.Dispose();
            }
            // this will make it easier to check for disposed without needing to catch it
            _watcher = null;

            cts = _consumerCts;
            consumerTask = _consumerTask;
            _consumerCts = null;
            _consumerTask = null;
        }

        // Signal shutdown, then best-effort drain in-flight work before tearing down shared state.
        // Parallel.ForEachAsync's task only completes once every in-flight iteration has finished
        // (or observed cancellation), so waiting on it alone is enough to drain.
        _channel.Writer.TryComplete();
        cts?.Cancel();
        try
        {
            consumerTask?.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // best-effort; the consumer loop observes cancellation and unwinds on its own
        }

        cts?.Dispose();

        FileAdded = null;
        FileDeleted = null;

        GC.SuppressFinalize(this);
    }

    private async Task<bool> IsLockedAsync(string path, CancellationToken token)
    {
        if (!Options.Enabled) return false;
        var waitTime = Options.WaitTimeMilliseconds;
        // At least 1s between to ensure that size has the chance to change
        if (waitTime < 1000)
        {
            waitTime = 4000;
        }

        // Wait 1 minute before giving up on trying to access the file. In aggressive mode this is
        // just the first (read-only) phase, so as not to get in another process's way.
        var (fileGone, filesize, numAttempts) = await WaitUntilAccessibleAsync(path, waitTime, token);
        if (fileGone) return true;

        if (Options.Aggressive)
        {
            if (numAttempts >= 60)
            {
                _logger.LogWarning("Could not access file: {Filename}", path);
                return true;
            }

            var seconds = Options.AggressiveWaitTimeSeconds;
            if (seconds <= 0)
            {
                seconds = 8;
            }

            Exception? e = null;
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
                await Task.Delay(waitTime, token);
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

    // Shared by both the non-aggressive path and aggressive mode's first (read-only) phase - the two
    // used to be copy-pasted identically apart from a log message.
    private async Task<(bool FileGone, long FileSize, int Attempts)> WaitUntilAccessibleAsync(string path, int waitTime, CancellationToken token)
    {
        Exception? e = null;
        long filesize;
        var numAttempts = 0;
        while ((filesize = CanAccessFile(path, ref e)) == 0 && numAttempts < 60)
        {
            if (!File.Exists(path))
            {
                _logger.LogTrace("Failed to access. File no longer exists. Attempt # {NumAttempts}, {FileName}",
                    numAttempts, path);
                return (true, 0, numAttempts);
            }

            numAttempts++;
            await Task.Delay(waitTime, token);
            _logger.LogTrace("Failed to access (or filesize is 0). Attempt # {NumAttempts}, {FileName}",
                numAttempts, path);
        }

        return (false, filesize, numAttempts);
    }

    //Added size return, since symbolic links return 0, we use this function also to return the size of the file.
    private long CanAccessFile(string fileName, ref Exception? e)
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
                if (!new FileInfo(fileName).IsReadOnly && PlatformUtility.IsWindows) return GetFileSize(fileName, accessType);
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
    private bool FileModified(string fileName, int seconds, ref long lastFileSize, ref Exception? e)
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
