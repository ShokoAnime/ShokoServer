using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

#nullable enable
namespace Shoko.Server.Utilities;

public static class FileSystemHelpers
{
    #region File System Path

    public static readonly EnumerationOptions _cachedEnumerationOptions = new() { RecurseSubdirectories = false, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System };

    public static bool ContainsFileSystemEntryPaths(string directoryPath)
        => Directory.EnumerateFileSystemEntries(directoryPath, "*", _cachedEnumerationOptions).Any();

    public static string[] GetFilePaths(string directoryPath, bool recursive = false, IEnumerable<string>? extensions = null, Func<string, bool>? filter = null, CancellationToken cancellationToken = default)
        => GetFileSystemEntryPaths(directoryPath, recursive, extensions, filter, outputFiles: true, outputDirectories: false, cancellationToken: cancellationToken);

    public static string[] GetFileSystemEntryPaths(string directoryPath, bool recursive = false, IEnumerable<string>? extensions = null, Func<string, bool>? filter = null, CancellationToken cancellationToken = default)
        => GetFileSystemEntryPaths(directoryPath, recursive, extensions, filter, outputFiles: true, outputDirectories: true, cancellationToken);

    private static string[] GetFileSystemEntryPaths(string directoryPath, bool recursive = false, IEnumerable<string>? extensions = null, Func<string, bool>? filter = null, bool outputFiles = true, bool outputDirectories = true, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
            return [];
        var outputBag = new ConcurrentBag<string>();
        var canOutputPath = GetPathValidator(extensions, filter);
        Parallelize(directoryPath, path =>
        {
            if (outputFiles)
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", _cachedEnumerationOptions))
                {
                    if (canOutputPath(file))
                        outputBag.Add(file);
                }
            }
            if (outputDirectories || recursive)
            {
                var outputs = new List<string>();
                foreach (var directory in Directory.EnumerateDirectories(path, "*", _cachedEnumerationOptions))
                {
                    if (outputDirectories && canOutputPath(directory))
                        outputBag.Add(directory);
                    if (recursive && Path.GetExtension(directory) is not ".trickplay")
                        outputs.Add(directory);
                }
                return outputs;
            }
            return null;
        }, cancellationToken).Wait(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return outputBag.ToArray();
    }

    public static Func<string, bool> GetPathValidator(IEnumerable<string>? extensions, Func<string, bool>? filter)
    {
        if (extensions is null)
            return filter ?? ((_) => true);
        var extensionSet = extensions is IReadOnlyCollection<string> readOnlyCollection ? readOnlyCollection : extensions.ToHashSet();
        if (filter is not null)
            return (path) => extensionSet.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) && filter(path);
        return (path) => extensionSet.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Parallelize

    private static Task Parallelize<T>(T initialValue, Func<T, IEnumerable<T>?> action, CancellationToken cancellationToken = default)
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

    #endregion
}
