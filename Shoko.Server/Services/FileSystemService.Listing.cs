using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Mono.Unix.Native;
using Shoko.Abstractions.Utilities;

namespace Shoko.Server.Services;

public partial class FileSystemService
{
    /// <summary>
    ///   An entry of a directory as reading the directory gave it.
    /// </summary>
    /// <param name="Name">Its name.</param>
    /// <param name="IsDirectory">Whether it is a directory, or a link to one.</param>
    /// <param name="IsLink">Whether it is a symbolic link.</param>
    private readonly record struct DirectoryItem(string Name, bool IsDirectory, bool IsLink);

    /// <summary>
    ///   Whether the directories are read through <c>readdir</c>, which gives
    ///   each entry's kind with no stat of its own. Off once the native library
    ///   turns out to be missing.
    /// </summary>
    private static bool _useReaddir = PlatformUtility.IsUnixLike;

    /// <summary>
    ///   Reads a directory's entries.
    /// </summary>
    /// <param name="path">The directory, as handed out.</param>
    /// <returns>Its entries.</returns>
    /// <exception cref="DirectoryNotFoundException">There is no directory at the path.</exception>
    /// <exception cref="UnauthorizedAccessException">It may not be read.</exception>
    /// <exception cref="IOException">It could not be read for another reason.</exception>
    private IReadOnlyList<DirectoryItem> ReadDirectory(string path)
    {
        if (_useReaddir)
        {
            try
            {
                return ReadDirectoryUnix(path);
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or TypeInitializationException)
            {
                _useReaddir = false;
                logger.LogWarning(ex, "Cannot read directories through readdir; listing through .NET instead");
            }
        }

        return ReadDirectoryDotNet(path);
    }

    /// <summary>
    ///   Reads a directory through .NET, where the kind and attributes come
    ///   with each entry for free on Windows; on Unix it is the fallback, at an
    ///   lstat per entry and a stat through each link.
    /// </summary>
    private static List<DirectoryItem> ReadDirectoryDotNet(string path)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = false,
            // Only the entries: a folder that cannot be read at all still throws, from the first move.
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.System,
        };
        var listing = new System.IO.Enumeration.FileSystemEnumerable<DirectoryItem>(
            PlatformUtility.EnsureUsablePath(path)!,
            (ref System.IO.Enumeration.FileSystemEntry entry) => new(entry.FileName.ToString(), entry.IsDirectory, (entry.Attributes & FileAttributes.ReparsePoint) != 0),
            options
        );
        return [.. listing];
    }

    /// <summary>
    ///   Reads a directory through <c>readdir_r</c>: the kind comes from
    ///   <c>d_type</c>, so an entry costs nothing more, a link one stat through
    ///   it to tell a link to a folder, and an entry of unknown kind (some
    ///   network and FUSE file systems) one lstat.
    /// </summary>
    private static List<DirectoryItem> ReadDirectoryUnix(string path)
    {
        const byte DT_UNKNOWN = 0, DT_DIR = 4, DT_LNK = 10;

        var handle = Syscall.opendir(path);
        if (handle == IntPtr.Zero)
            throw ToException(Stdlib.GetLastError(), path);

        var items = new List<DirectoryItem>();
        try
        {
            while (true)
            {
                // readdir_r, as Mono's readdir takes one lock for every directory read in the process, which
                // queues the parallel walk behind it; each read here is a round trip on a network share.
                var entry = new Dirent();
                if (Syscall.readdir_r(handle, entry, out var result) is not 0)
                    throw ToException(Stdlib.GetLastError(), path);
                if (result == IntPtr.Zero)
                    break;

                var name = entry.d_name;
                if (name is "." or "..")
                    continue;

                var type = entry.d_type;
                var fullPath = Path.Join(path, name);
                if (type is DT_UNKNOWN)
                {
                    // Gone since it was listed: leave it out.
                    if (Syscall.lstat(fullPath, out var stat) is not 0)
                        continue;
                    type = (stat.st_mode & FilePermissions.S_IFMT) switch
                    {
                        FilePermissions.S_IFDIR => DT_DIR,
                        FilePermissions.S_IFLNK => DT_LNK,
                        _ => 8,
                    };
                }

                if (type is DT_LNK)
                {
                    // A link to a folder counts as one, as .NET has it; a dead link is not one.
                    var isDirectory = Syscall.stat(fullPath, out var target) is 0 && (target.st_mode & FilePermissions.S_IFMT) is FilePermissions.S_IFDIR;
                    items.Add(new(name, isDirectory, IsLink: true));
                    continue;
                }

                items.Add(new(name, type is DT_DIR, IsLink: false));
            }
        }
        finally
        {
            Syscall.closedir(handle);
        }

        return items;
    }

    private static Exception ToException(Errno error, string path)
        => error switch
        {
            Errno.ENOENT or Errno.ENOTDIR => new DirectoryNotFoundException($"No directory at {path}."),
            Errno.EACCES or Errno.EPERM => new UnauthorizedAccessException($"The directory may not be read: {path}."),
            _ => new IOException($"The directory could not be read ({error}): {path}."),
        };
}
