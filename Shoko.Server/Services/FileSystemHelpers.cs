using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Mono.Unix;
using Shoko.Abstractions.Utilities;

#pragma warning disable CS0618
namespace Shoko.Server.Services;

public class FileSystemHelpers
{
    private readonly ILogger<FileSystemHelpers> _logger;

    public FileSystemHelpers(ILogger<FileSystemHelpers> logger)
    {
        _logger = logger;
    }

    private static readonly EnumerationOptions _cachedEnumerationOptions = new() { RecurseSubdirectories = false, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System };

    public string[] GetDirectoryPaths(string directoryPath, bool recursive = false, Func<string, bool, bool>? filter = null, CancellationToken cancellationToken = default)
        => GetFileSystemEntryPaths(directoryPath, recursive, filter: filter, outputFiles: false, outputDirectories: true, cancellationToken: cancellationToken);

    public string[] GetFilePaths(string directoryPath, bool recursive = false, IEnumerable<string>? extensions = null, Func<string, bool, bool>? filter = null, CancellationToken cancellationToken = default)
        => GetFileSystemEntryPaths(directoryPath, recursive, extensions, filter, outputFiles: true, outputDirectories: false, cancellationToken: cancellationToken);

    private string[] GetFileSystemEntryPaths(string directoryPath, bool recursive = false, IEnumerable<string>? extensions = null, Func<string, bool, bool>? filter = null, bool outputFiles = true, bool outputDirectories = true, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
            return [];
        var outputBag = new ConcurrentBag<string>();
        var canProceed = GetPathValidator(extensions, filter);
        Parallelize(directoryPath, path =>
        {
            if (outputFiles)
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", _cachedEnumerationOptions))
                {
                    if (canProceed(file, false))
                        outputBag.Add(file);
                }
            }
            if (outputDirectories || recursive)
            {
                var outputs = new List<string>();
                foreach (var directory in Directory.EnumerateDirectories(path, "*", _cachedEnumerationOptions))
                {
                    if (!canProceed(directory, true))
                        continue;
                    if (outputDirectories)
                        outputBag.Add(directory);
                    outputs.Add(directory);
                }
                return outputs;
            }
            return null;
        }, cancellationToken).Wait(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return outputBag.ToArray();
    }

    public Func<string, bool, bool> GetPathValidator(IEnumerable<string>? extensions, Func<string, bool, bool>? filter)
    {
        if (extensions is null)
            return filter ?? ((_, _) => true);
        var extensionSet = extensions is IReadOnlyCollection<string> readOnlyCollection ? readOnlyCollection : extensions.ToHashSet();
        if (filter is not null)
            return (path, isDirectory) => (isDirectory || extensionSet.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) && filter(path, isDirectory);
        return (path, isDirectory) => isDirectory || extensionSet.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    private Task Parallelize<T>(T initialValue, Func<T, IEnumerable<T>?> action, CancellationToken cancellationToken = default)
    {
        var pendingCount = 1;
        var bufferBlock = new BufferBlock<T>(new() { BoundedCapacity = DataflowBlockOptions.Unbounded });
        var actionBlock = new ActionBlock<T>(
            inputValue =>
            {
                try
                {
                    var output = action(inputValue) ?? [];
                    foreach (var outputAction in output)
                    {
                        Interlocked.Increment(ref pendingCount);
                        bufferBlock.Post(outputAction);
                    }
                }
                catch (OperationCanceledException)
                {
                    // still must stop the whole walk on cancellation
                    throw;
                }
                catch (Exception ex)
                {
                    // Treat a failing entry as a dead end rather than faulting the whole block -
                    // one inaccessible subdirectory shouldn't discard everything already collected
                    // from the rest of the tree.
                    _logger.LogWarning(ex, "Failed to enumerate {Path}; skipping", inputValue);
                }
                finally
                {
                    if (Interlocked.Decrement(ref pendingCount) == 0)
                    {
                        bufferBlock.Complete();
                    }
                }
            },
            new()
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = GetThreadCount(),
                BoundedCapacity = DataflowBlockOptions.Unbounded
            }
        );
        bufferBlock.LinkTo(actionBlock, new() { PropagateCompletion = true });
        bufferBlock.Post(initialValue);
        return actionBlock.Completion;
    }

    private static int GetThreadCount()
        => Math.Max(1, Math.Min((int)Math.Floor((decimal)Environment.ProcessorCount / 2), 10));

    /// <summary>
    /// Attempts to retrieve the inode number (Unix) or file ID (Windows) of the
    /// file.
    /// </summary>
    /// <remarks>
    /// The inode number is a unique identifier for files on Unix-based systems,
    /// while the file ID serves a similar purpose on Windows. Both are unique
    /// within a specific volume but are not guaranteed to be unique across
    /// different volumes. This method attempts to retrieve the appropriate
    /// platform-specific identifier depending on the system it is running on.
    /// </remarks>
    /// <param name="path">The path of the file for which the unique identifier
    /// is to be obtained.</param>
    /// <returns>
    /// The inode number (Unix) or file ID (Windows) if successful, or null if
    /// the file doesn't exist or the platform-specific identifier cannot be
    /// obtained.
    /// </returns>
    public long? GetVideoFileUID(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        // We're running on Windows, so try to get the file ID (similar to an inode on Unix, just for Windows).
        if (PlatformUtility.IsWindows)
        {
            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (GetFileInformationByHandle(fileStream.SafeFileHandle.DangerousGetHandle(), out var fileInfo))
                return (long)(((ulong)fileInfo.FileIndexHigh << 32) | fileInfo.FileIndexLow);
        }
        // We're running on Unix, so try to get the inode number.
        else if (PlatformUtility.IsUnixLike)
        {
            if (UnixFileSystemInfo.TryGetFileSystemEntry(path, out var unixFile))
                return unixFile.Inode;
        }

        // We couldn't get an unique id for the file for whatever reason.
        return null;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(IntPtr hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

    [StructLayout(LayoutKind.Sequential)]
    private struct BY_HANDLE_FILE_INFORMATION
    {
        public uint FileAttributes;
        public FILETIME CreationTime;
        public FILETIME LastAccessTime;
        public FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }
}
