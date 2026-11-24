using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Mono.Unix;
using Mono.Unix.Native;
using Shoko.Abstractions.FileSystem;
using Shoko.Abstractions.FileSystem.Services;
using Shoko.Abstractions.Utilities;

#pragma warning disable CS0618
namespace Shoko.Server.Services;

public partial class FileSystemService(ILogger<FileSystemService> logger, IServiceProvider? serviceProvider = null) : IFileSystem
{

    // Windows enforces a 260-character MAX_PATH limit on the classic Win32 file APIs that .NET calls
    // into, regardless of whether the OS-wide "enable long paths" policy is on (that policy also
    // requires an app-manifest opt-in this project doesn't have). Prefixing with \\?\ (or \\?\UNC\ for
    // shares) bypasses the limit unconditionally on every supported Windows version, so every raw
    // File/Directory call that can touch a managed-folder path (anime titles/episode names routinely
    // push well past 260 chars once nested under a library folder) must go through here instead of
    // calling System.IO directly. The prefix is applied right before the syscall and never returned,
    // so callers never see or persist the mutated form. PlatformUtility.EnsureUsablePath is itself a
    // no-op on non-Windows platforms, where the OS doesn't impose this limit.

    #region Files

    public bool FileExists(string? path)
        => !string.IsNullOrEmpty(path) && File.Exists(PlatformUtility.EnsureUsablePath(path));

    /// <summary>
    /// Gets a <see cref="FileInfo"/> for the path, or null if the file does not exist.
    /// </summary>
    public FileInfo? GetFileInfo(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        var usablePath = PlatformUtility.EnsureUsablePath(path);
        return File.Exists(usablePath) ? new FileInfo(usablePath) : null;
    }

    /// <summary>
    /// Gets the file size, or -1 if the file does not exist.
    /// </summary>
    public long GetFileSize(string path)
    {
        var usablePath = PlatformUtility.EnsureUsablePath(path);
        return File.Exists(usablePath) ? new FileInfo(usablePath).Length : -1;
    }

    public FileStream OpenFile(string path, FileMode mode, FileAccess access, FileShare share)
        => File.Open(PlatformUtility.EnsureUsablePath(path), mode, access, share);

    public FileStream OpenRead(string path)
        => File.OpenRead(PlatformUtility.EnsureUsablePath(path));

    public FileStream OpenFile(string path, FileStreamOptions options)
        => new(PlatformUtility.EnsureUsablePath(path)!, options);

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite = false)
        => Unwatched(() => File.Copy(PlatformUtility.EnsureUsablePath(sourcePath), PlatformUtility.EnsureUsablePath(destinationPath), overwrite), destinationPath);

    public void MoveFile(string sourcePath, string destinationPath, bool overwrite = false)
    {
        var usableSource = PlatformUtility.EnsureUsablePath(sourcePath);
        var usableDestination = PlatformUtility.EnsureUsablePath(destinationPath);

        // On a case-insensitive file system both spellings name the same file, which a move cannot rename by
        // itself, so it goes through a name of its own first.
        if (IsCaseOnlyRename(sourcePath, destinationPath, out var temporaryPath))
        {
            var usableTemporary = PlatformUtility.EnsureUsablePath(temporaryPath);
            Unwatched(() =>
            {
                File.Move(usableSource, usableTemporary);
                File.Move(usableTemporary, usableDestination);
            }, sourcePath, destinationPath, temporaryPath);
            return;
        }

        // .NET follows a link to see whether it is a file, so a link to a directory is "not found" as the source
        // and refused as the destination; rename(2) moves and replaces the link itself, in a single step.
        if (PlatformUtility.IsUnixLike && (IsUnixLink(sourcePath) || IsUnixLink(destinationPath)))
        {
            Unwatched(() => MoveUnixLink(sourcePath, destinationPath, overwrite), sourcePath, destinationPath);
            return;
        }

        // On Windows a link to a directory is a directory entry, so File.Move finds no file at the source and
        // will not replace one at the destination; it is moved as the directory entry it is.
        if (OperatingSystem.IsWindows() && (IsWindowsDirectoryLink(usableSource) || IsWindowsDirectoryLink(usableDestination)))
        {
            Unwatched(() => MoveWindowsDirectoryLink(usableSource!, usableDestination!, sourcePath, destinationPath, overwrite), sourcePath, destinationPath);
            return;
        }

        Unwatched(() => File.Move(usableSource, usableDestination, overwrite), sourcePath, destinationPath);
    }

    /// <summary>
    ///   The attributes of what is at a path, without following a link, or
    ///   <c>null</c> when nothing is there.
    /// </summary>
    private static FileAttributes? GetOwnAttributes(string path)
    {
        try
        {
            return File.GetAttributes(path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static bool IsWindowsDirectoryLink(string? path)
        => path is not null &&
            GetOwnAttributes(path) is { } attributes &&
            (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) is (FileAttributes.Directory | FileAttributes.ReparsePoint);

    /// <summary>
    ///   Moves a link or a file over a link or a file on Windows, where a link
    ///   to a directory is moved and removed as a directory entry. Replacing
    ///   takes two steps there: the link or file at the destination is removed
    ///   first, never what a link points to.
    /// </summary>
    private static void MoveWindowsDirectoryLink(string usableSource, string usableDestination, string sourcePath, string destinationPath, bool overwrite)
    {
        if (GetOwnAttributes(usableSource) is not { } source)
            throw new FileNotFoundException($"Cannot move {sourcePath} to {destinationPath}: there is nothing at the source.", sourcePath);
        if ((source & (FileAttributes.Directory | FileAttributes.ReparsePoint)) is FileAttributes.Directory)
            throw new UnauthorizedAccessException($"Cannot move {sourcePath} to {destinationPath}: the source is a directory, not a file or a link.");
        if (GetOwnAttributes(usableDestination) is { } existing)
        {
            var existingIsLink = (existing & FileAttributes.ReparsePoint) is not 0;
            if ((existing & FileAttributes.Directory) is not 0 && !existingIsLink)
                throw new UnauthorizedAccessException($"Cannot move {sourcePath} to {destinationPath}: a directory is there.");
            if (!overwrite)
                throw new IOException($"Cannot move {sourcePath} to {destinationPath}: something is already there.");
            if ((existing & FileAttributes.Directory) is not 0)
                Directory.Delete(usableDestination);
            else
                File.Delete(usableDestination);
        }

        if ((source & FileAttributes.Directory) is not 0)
            Directory.Move(usableSource, usableDestination);
        else
            File.Move(usableSource, usableDestination);
    }

    private static bool IsUnixLink(string path)
        => Syscall.lstat(path, out var stat) is 0 && (stat.st_mode & FilePermissions.S_IFMT) is FilePermissions.S_IFLNK;

    /// <summary>
    ///   Moves what is at the source, a link or a file, over a link or a file
    ///   at the destination, without following either.
    /// </summary>
    private static void MoveUnixLink(string sourcePath, string destinationPath, bool overwrite)
    {
        if (Syscall.lstat(sourcePath, out _) is not 0)
            throw MoveError(Stdlib.GetLastError(), sourcePath, destinationPath);
        if (Syscall.lstat(destinationPath, out var existing) is 0)
        {
            if ((existing.st_mode & FilePermissions.S_IFMT) is FilePermissions.S_IFDIR)
                throw new UnauthorizedAccessException($"Cannot move {sourcePath} to {destinationPath}: a directory is there.");
            if (!overwrite)
                throw new IOException($"Cannot move {sourcePath} to {destinationPath}: something is already there.");
        }

        if (Syscall.rename(sourcePath, destinationPath) is 0)
            return;

        var error = Stdlib.GetLastError();
        if (error is not Errno.EXDEV || new FileInfo(sourcePath).LinkTarget is not { } target)
            throw MoveError(error, sourcePath, destinationPath);

        // Another volume: the link is made anew beside the destination, with the same target as written, and
        // renamed over it, so the destination still changes in a single step.
        var temporaryPath = Path.Join(Path.GetDirectoryName(destinationPath), $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
        File.CreateSymbolicLink(temporaryPath, target);
        if (Syscall.rename(temporaryPath, destinationPath) is not 0)
        {
            error = Stdlib.GetLastError();
            File.Delete(temporaryPath);
            throw MoveError(error, sourcePath, destinationPath);
        }

        File.Delete(sourcePath);
    }

    private static Exception MoveError(Errno error, string sourcePath, string destinationPath)
        => error switch
        {
            Errno.ENOENT when !File.Exists(sourcePath) && new FileInfo(sourcePath).LinkTarget is null
                => new FileNotFoundException($"Could not find {sourcePath}.", sourcePath),
            Errno.ENOENT or Errno.ENOTDIR
                => new DirectoryNotFoundException($"A folder on the way from {sourcePath} to {destinationPath} does not exist."),
            Errno.EACCES or Errno.EPERM or Errno.EISDIR
                => new UnauthorizedAccessException($"Cannot move {sourcePath} to {destinationPath} ({error})."),
            _ => new IOException($"Cannot move {sourcePath} to {destinationPath} ({error})."),
        };

    public void DeleteFile(string path)
        => Unwatched(() => File.Delete(PlatformUtility.EnsureUsablePath(path)), path);

    public void CreateHardLink(string sourcePath, string destinationPath)
        => Unwatched(() =>
        {
            // Hard links are for files on every platform; say so rather than pass on the system's bare refusal.
            if (Directory.Exists(PlatformUtility.EnsureUsablePath(sourcePath)))
                throw new IOException($"Cannot hard link {sourcePath}: a directory cannot have hard links.");

            if (PlatformUtility.IsWindows)
            {
                if (!CreateHardLinkW(PlatformUtility.EnsureUsablePath(destinationPath), PlatformUtility.EnsureUsablePath(sourcePath), IntPtr.Zero))
                    throw new IOException($"Could not create a hard link at {destinationPath}.", Marshal.GetHRForLastWin32Error());
                return;
            }

            // Throws a UnixIOException, an IOException, naming the error (EXDEV for another volume).
            new UnixFileInfo(sourcePath).CreateLink(destinationPath);
        }, destinationPath);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateHardLinkW(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    #endregion

    #region Directories

    public bool DirectoryExists(string? path)
        => !string.IsNullOrEmpty(path) && Directory.Exists(PlatformUtility.EnsureUsablePath(path));

    public bool IsDirectoryEmpty(string path)
    {
        var usablePath = PlatformUtility.EnsureUsablePath(path);
        return Directory.Exists(usablePath) && !Directory.EnumerateFileSystemEntries(usablePath).Any();
    }

    public DirectoryInfo? GetDirectoryInfo(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        var usablePath = PlatformUtility.EnsureUsablePath(path);
        return Directory.Exists(usablePath) ? new DirectoryInfo(usablePath) : null;
    }

    public void CreateDirectory(string path)
        => Unwatched(() => Directory.CreateDirectory(PlatformUtility.EnsureUsablePath(path)), path);

    public void DeleteDirectory(string path, bool recursive = false)
        => Unwatched(() => Directory.Delete(PlatformUtility.EnsureUsablePath(path), recursive), path);

    public void MoveDirectory(string sourcePath, string destinationPath, bool overwrite = false)
    {
        var source = Path.GetFullPath(sourcePath);
        var destination = Path.GetFullPath(destinationPath);
        if (GetStat(source, followSymbolicLinks: false) is not DirectoryStat sourceStat)
            throw new DirectoryNotFoundException($"No directory to move at {source}.");

        if (IsCaseOnlyRename(source, destination, out var temporaryPath))
        {
            Unwatched(() =>
            {
                Directory.Move(PlatformUtility.EnsureUsablePath(source), PlatformUtility.EnsureUsablePath(temporaryPath));
                Directory.Move(PlatformUtility.EnsureUsablePath(temporaryPath), PlatformUtility.EnsureUsablePath(destination));
            }, source, destination, temporaryPath);
            return;
        }

        if (string.Equals(source, destination, PlatformUtility.StringComparison) || IsInside(destination, source))
            throw new IOException($"Cannot move {source} into itself.");

        switch (GetStat(destination, followSymbolicLinks: false))
        {
            // Renamed in one step on its own volume; a rename cannot cross volumes, so elsewhere it goes file by file.
            case null when GetStat(Path.GetDirectoryName(destination))?.Identity.VolumeID == sourceStat.Identity.VolumeID:
                Unwatched(() => Directory.Move(PlatformUtility.EnsureUsablePath(source), PlatformUtility.EnsureUsablePath(destination)), source, destination);
                return;
            case null:
                CreateDirectory(destination);
                MergeDirectory(source, destination, overwrite: false);
                break;
            case DirectoryStat when overwrite:
                MergeDirectory(source, destination, overwrite: true);
                break;
            case DirectoryStat:
                throw new IOException($"A directory already exists at {destination}.");
            default:
                throw new IOException($"Something that is not a directory is at {destination}.");
        }

        DeleteDirectory(source);
    }

    /// <summary>
    ///   Moves what a directory holds into another, existing one: files and
    ///   links as themselves, subdirectories merged into any of the same
    ///   name. The source is left empty.
    /// </summary>
    private void MergeDirectory(string source, string destination, bool overwrite)
    {
        foreach (var name in Directory.EnumerateFileSystemEntries(PlatformUtility.EnsureUsablePath(source)).Select(Path.GetFileName).ToList())
        {
            var entry = Path.Join(source, name);
            var target = Path.Join(destination, name);
            var entryStat = GetStat(entry, followSymbolicLinks: false);
            var targetStat = GetStat(target, followSymbolicLinks: false);
            switch (entryStat)
            {
                case null:
                    continue;
                case DirectoryStat when targetStat is DirectoryStat:
                    MergeDirectory(entry, target, overwrite);
                    DeleteDirectory(entry);
                    continue;
                case DirectoryStat when targetStat is null:
                    MoveDirectory(entry, target);
                    continue;
                case DirectoryStat:
                    throw new IOException($"Cannot move the directory {entry} over {target}, which is not a directory.");
                case SymbolicLinkStat link:
                    MoveSymbolicLink(entry, target, link, targetStat, overwrite);
                    continue;
                default:
                    if (targetStat is DirectoryStat)
                        throw new IOException($"Cannot move {entry} over the directory {target}.");
                    MoveFile(entry, target, overwrite);
                    continue;
            }
        }
    }

    /// <summary>
    ///   Moves a symbolic link as itself, by making it again at the
    ///   destination with the same target, which also works across volumes.
    /// </summary>
    private void MoveSymbolicLink(string entry, string target, SymbolicLinkStat link, FileSystemStat? targetStat, bool overwrite)
    {
        if (targetStat is DirectoryStat)
            throw new IOException($"Cannot move the link {entry} over the directory {target}.");
        if (targetStat is not null && !overwrite)
            throw new IOException($"Something already exists at {target}.");

        var linkTarget = link.Target ?? throw new IOException($"Could not read the link at {entry}.");
        var isDirectoryLink = link.IsDirectoryLink ?? GetStat(entry) is DirectoryStat;
        Unwatched(() =>
        {
            var usableTarget = PlatformUtility.EnsureUsablePath(target);
            if (targetStat is not null)
                File.Delete(usableTarget);
            if (isDirectoryLink)
                Directory.CreateSymbolicLink(usableTarget, linkTarget);
            else
                File.CreateSymbolicLink(usableTarget, linkTarget);
            File.Delete(PlatformUtility.EnsureUsablePath(entry));
        }, entry, target);
    }

    private static bool IsInside(string path, string directory)
        => path.StartsWith(directory.EndsWith(Path.DirectorySeparatorChar) ? directory : directory + Path.DirectorySeparatorChar, PlatformUtility.StringComparison);

    #endregion

    #region Shared

    public FileSystemInfo? GetFileSystemInfo(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        // The entry itself, so a dangling link is still found. A link takes the kind of its target, a file when
        // it points to nothing, as Windows makes file and directory links apart.
        var usablePath = PlatformUtility.EnsureUsablePath(path);
        var isDirectory = GetStat(path, followSymbolicLinks: false) switch
        {
            null => (bool?)null,
            DirectoryStat => true,
            SymbolicLinkStat link => link.IsDirectoryLink ?? GetStat(path) is DirectoryStat,
            _ => false,
        };
        return isDirectory switch
        {
            null => null,
            true => new DirectoryInfo(usablePath),
            false => new FileInfo(usablePath),
        };
    }

    public void CreateSymbolicLink(string sourcePath, string destinationPath, bool? isDirectory = null)
    {
        var usableLink = PlatformUtility.EnsureUsablePath(destinationPath);
        if (isDirectory is { } directory)
        {
            // The caller said which, so nothing is looked up, and the target need not exist yet.
            Unwatched(() =>
            {
                if (directory)
                    Directory.CreateSymbolicLink(usableLink, sourcePath);
                else
                    File.CreateSymbolicLink(usableLink, sourcePath);
            }, destinationPath);
            return;
        }

        // A relative target is read from the link's folder, so look for it there. The link stores it as given.
        var resolvedSource = Path.IsPathFullyQualified(sourcePath)
            ? sourcePath
            : Path.GetFullPath(sourcePath, Path.GetDirectoryName(Path.GetFullPath(destinationPath)) ?? string.Empty);
        var usableDestination = PlatformUtility.EnsureUsablePath(destinationPath);

        // Windows has file and directory links, so the next step must be there to tell which. It is read without
        // following: a link takes the kind of the link it points to, which Windows keeps on the link. Unix links
        // have no kind, so there it makes no difference.
        var next = GetStat(resolvedSource, followSymbolicLinks: false) ?? throw new FileNotFoundException($"Nothing to link to at {resolvedSource}.", resolvedSource);
        Unwatched(() =>
        {
            if (next is DirectoryStat or SymbolicLinkStat { IsDirectoryLink: true })
                Directory.CreateSymbolicLink(usableDestination, sourcePath);
            else
                File.CreateSymbolicLink(usableDestination, sourcePath);
        }, destinationPath);
    }

    public bool CanCreateSymbolicLinks(string directoryPath)
    {
        var directory = Path.GetFullPath(directoryPath);
        if (!Directory.Exists(PlatformUtility.EnsureUsablePath(directory)))
            throw new DirectoryNotFoundException($"No directory at {directory}.");

        var target = Path.Join(directory, $".shoko-link-test-{Guid.NewGuid():N}");
        var link = target + "-link";
        return Unwatched(() =>
        {
            // A folder that cannot be written to throws here: that says nothing about links.
            File.WriteAllBytes(PlatformUtility.EnsureUsablePath(target), []);
            try
            {
                File.CreateSymbolicLink(PlatformUtility.EnsureUsablePath(link), target);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogDebug(ex, "Cannot create symbolic links in {Directory}", directory);
                return false;
            }
            finally
            {
                TryDelete(link);
                TryDelete(target);
            }
        }, target, link);

        static void TryDelete(string path)
        {
            try
            {
                File.Delete(PlatformUtility.EnsureUsablePath(path));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    public string? ReadSymbolicLink(string path)
    {
        try
        {
            // One readlink: null for anything but a link.
            return new FileInfo(PlatformUtility.EnsureUsablePath(path)!).LinkTarget;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    ///   How many links a chain may go through, as on Linux.
    /// </summary>
    private const int MaxSymbolicLinks = 40;

    public string? ResolveSymbolicLink(string? path, bool returnFinalTarget = true)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        var current = Path.GetFullPath(path);
        var visited = new HashSet<string>(PlatformUtility.StringComparer) { current };
        string? resolved = null;
        while (true)
        {
            // LinkTarget reads the link itself, a dangling one included, and is null for anything else.
            var target = new FileInfo(PlatformUtility.EnsureUsablePath(current)).LinkTarget;
            if (target is null)
                return resolved;

            var next = Path.GetFullPath(target, Path.GetDirectoryName(current) ?? string.Empty);
            if (!returnFinalTarget)
                return next;

            if (!visited.Add(next))
                throw new SymbolicLinkLoopException(path, next);

            if (visited.Count > MaxSymbolicLinks + 1)
                throw new SymbolicLinkLoopException(path, next, tooLong: true);

            resolved = current = next;
        }
    }

    #region Shared | Listing

    public string[] GetFilePaths(string directoryPath, FileSystemListingOptions? options = null, CancellationToken cancellationToken = default)
        => List([directoryPath], options, outputFiles: true, outputDirectories: false, cancellationToken)[0].Select(entry => entry.Path).ToArray();

    public string[] GetDirectoryPaths(string directoryPath, FileSystemListingOptions? options = null, CancellationToken cancellationToken = default)
        => List([directoryPath], options, outputFiles: false, outputDirectories: true, cancellationToken)[0].Select(entry => entry.Path).ToArray();

    public IReadOnlyList<FileSystemEntry> GetFileEntries(string directoryPath, FileSystemListingOptions? options = null, CancellationToken cancellationToken = default)
        => List([directoryPath], options, outputFiles: true, outputDirectories: false, cancellationToken)[0];

    public IReadOnlyList<FileSystemEntry> GetDirectoryEntries(string directoryPath, FileSystemListingOptions? options = null, CancellationToken cancellationToken = default)
        => List([directoryPath], options, outputFiles: false, outputDirectories: true, cancellationToken)[0];

    public IReadOnlyList<FileSystemEntry> GetFileSystemEntries(string directoryPath, FileSystemListingOptions? options = null, CancellationToken cancellationToken = default)
        => List([directoryPath], options, outputFiles: true, outputDirectories: true, cancellationToken)[0];

    public IReadOnlyDictionary<string, IReadOnlyList<FileSystemEntry>> GetFileSystemEntriesByDirectory(
        IEnumerable<string> directoryPaths,
        FileSystemListingOptions? options = null,
        CancellationToken cancellationToken = default
    )
        => ListByDirectory(directoryPaths, options, outputFiles: true, outputDirectories: true, cancellationToken);

    public IReadOnlyDictionary<string, IReadOnlyList<FileSystemEntry>> GetFileEntriesByDirectory(
        IEnumerable<string> directoryPaths,
        FileSystemListingOptions? options = null,
        CancellationToken cancellationToken = default
    )
        => ListByDirectory(directoryPaths, options, outputFiles: true, outputDirectories: false, cancellationToken);

    public IReadOnlyDictionary<string, IReadOnlyList<FileSystemEntry>> GetDirectoryEntriesByDirectory(
        IEnumerable<string> directoryPaths,
        FileSystemListingOptions? options = null,
        CancellationToken cancellationToken = default
    )
        => ListByDirectory(directoryPaths, options, outputFiles: false, outputDirectories: true, cancellationToken);

    private Dictionary<string, IReadOnlyList<FileSystemEntry>> ListByDirectory(
        IEnumerable<string> directoryPaths,
        FileSystemListingOptions? options,
        bool outputFiles,
        bool outputDirectories,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(directoryPaths);
        var roots = directoryPaths.Distinct(StringComparer.Ordinal).ToList();
        foreach (var root in roots)
            ArgumentNullException.ThrowIfNull(root, nameof(directoryPaths));

        var listings = List(roots, options, outputFiles, outputDirectories, cancellationToken);
        var byDirectory = new Dictionary<string, IReadOnlyList<FileSystemEntry>>(roots.Count, StringComparer.Ordinal);
        for (var index = 0; index < roots.Count; index++)
            byDirectory[roots[index]] = listings[index];
        return byDirectory;
    }

    /// <summary>
    ///   A directory to read in a listing.
    /// </summary>
    /// <param name="Root">The index of the listed directory it belongs to.</param>
    /// <param name="Path">The directory.</param>
    /// <param name="IsRoot">Whether it is the listed directory itself.</param>
    private readonly record struct ListingItem(int Root, string Path, bool IsRoot);

    /// <summary>
    ///   Lists every root as its own listing, in one parallel walk.
    /// </summary>
    /// <returns>Each root's entries, in the roots' order.</returns>
    private IReadOnlyList<FileSystemEntry>[] List(
        IReadOnlyList<string> roots,
        FileSystemListingOptions? options,
        bool outputFiles,
        bool outputDirectories,
        CancellationToken cancellationToken
    )
    {
        options ??= FileSystemListingOptions.Default;
        if (options.MaxDegreeOfParallelism is < 1)
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxDegreeOfParallelism, "At least one directory must be read at a time.");

        var outputBags = roots.Select(_ => new ConcurrentBag<FileSystemEntry>()).ToArray();
        var canProceed = GetEntryValidator(options);
        var recursive = options.Recursive;
        var followLinks = recursive && options.FollowDirectoryLinks;

        // What a link points to is only needed to walk into it, or when links to directories and to files
        // are not all listed alike; otherwise the entry reads it if anything asks.
        var resolveLinks = followLinks || !outputFiles || !outputDirectories || !options.IncludeDirectoryLinks || !options.IncludeFileLinks;

        // While following links, the directories reached through one are remembered by identity, each
        // root's own included, so a link back up the tree is walked once rather than until the path is too
        // long. Each root keeps its own, so it lists as it would on its own.
        var walked = followLinks ? roots.Select(_ => new ConcurrentDictionary<StatIdentity, byte>()).ToArray() : null;

        List<ListingItem>? Take(int root, string path, IReadOnlyList<DirectoryItem> items)
        {
            // Each entry's name, kind and link flag came with the listing, so an entry costs no read of its
            // own. The paths handed out are built from the plain path, so a long path's prefix never leaves.
            var walkInto = recursive ? new List<ListingItem>() : null;
            foreach (var (name, isDirectory, isLink) in items)
            {
                // A link left unread is listed whatever it points to, and walked into by nothing.
                // A link that is neither listed nor walked into needs no entry at all.
                var walksInto = isDirectory is true && recursive && (!isLink || walked is not null);
                var outputs = isDirectory switch
                {
                    null => true,
                    true => outputDirectories && (!isLink || options.IncludeDirectoryLinks),
                    false => outputFiles && (!isLink || options.IncludeFileLinks),
                };
                if (!outputs && !walksInto)
                    continue;
                var entry = new FileSystemEntry(this, Path.Join(path, name), isDirectory, isLink);
                if (!canProceed(entry))
                    continue;
                if (outputs)
                    outputBags[root].Add(entry);
                if (!walksInto)
                    continue;
                if (!isLink || TryGetIdentity(entry.Path) is { } identity && walked![root].TryAdd(identity, 0))
                    walkInto!.Add(new(root, entry.Path, IsRoot: false));
            }
            return walkInto;
        }

        List<ListingItem>? Read(ListingItem item)
        {
            if (!item.IsRoot)
                return Take(item.Root, item.Path, ReadDirectory(item.Path, resolveLinks));

            // A root is read before anything is checked: reading it says whether it is there. A missing one
            // lists nothing and says nothing, unless asked to throw.
            IReadOnlyList<DirectoryItem> rootItems;
            try
            {
                rootItems = ReadDirectory(item.Path, resolveLinks);
            }
            catch (Exception ex) when (!options.ThrowOnError && ex is IOException or UnauthorizedAccessException)
            {
                if (ex is not DirectoryNotFoundException)
                    logger.LogWarning(ex, "Failed to enumerate {Path}; skipping", item.Path);
                return null;
            }

            if (walked is not null && TryGetIdentity(item.Path) is { } rootIdentity)
                walked[item.Root].TryAdd(rootIdentity, 0);
            return Take(item.Root, item.Path, rootItems);
        }

        var rootItems = roots.Select((root, index) => new ListingItem(index, root, IsRoot: true)).ToList();
        var threads = options.MaxDegreeOfParallelism ?? GetThreadCount();
        if (rootItems.Count is 1)
        {
            // One root is read on the caller's thread: a listing that does not walk needs nothing more.
            var subdirectories = Read(rootItems[0]);
            if (subdirectories is { Count: > 0 })
                Walk(subdirectories, Read, options.ThrowOnError, threads, cancellationToken);
        }
        else if (rootItems.Count > 1)
        {
            Walk(rootItems, Read, options.ThrowOnError, threads, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return [.. outputBags.Select(bag => (IReadOnlyList<FileSystemEntry>)bag.ToArray())];
    }

    /// <summary>
    ///   Runs a parallel walk to its end, throwing what stopped it as itself.
    /// </summary>
    private void Walk<T>(IReadOnlyList<T> initialValues, Func<T, IEnumerable<T>?> action, bool stopOnError, int threads, CancellationToken cancellationToken)
    {
        var walk = Parallelize(initialValues, action, stopOnError, threads, cancellationToken);
        try
        {
            walk.Wait(cancellationToken);
        }
        catch (AggregateException ex) when (ex.InnerExceptions is [var inner] && inner is not OperationCanceledException)
        {
            ExceptionDispatchInfo.Throw(inner);
        }
    }

    private StatIdentity? TryGetIdentity(string path)
    {
        try
        {
            return GetDirectoryStat(path)?.Identity;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static FileSystemEntryFilter GetEntryValidator(FileSystemListingOptions options)
    {
        var filter = options.Filter;
        if (options.Extensions is not { } extensions)
            return filter ?? (_ => true);
        if (filter is not null)
            return entry => (entry.IsDirectory || extensions.Any(ext => entry.Path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) && filter(entry);
        return entry => entry.IsDirectory || extensions.Any(ext => entry.Path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    private Task Parallelize<T>(IReadOnlyList<T> initialValues, Func<T, IEnumerable<T>?> action, bool stopOnError, int threads, CancellationToken cancellationToken = default)
    {
        var pendingCount = initialValues.Count;
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
                catch (Exception) when (stopOnError)
                {
                    // Faults the walk, which then says why.
                    throw;
                }
                catch (Exception ex)
                {
                    // Treat a failing entry as a dead end rather than faulting the whole block -
                    // one inaccessible subdirectory shouldn't discard everything already collected
                    // from the rest of the tree.
                    logger.LogWarning(ex, "Failed to enumerate {Path}; skipping", inputValue);
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
                MaxDegreeOfParallelism = threads,
                BoundedCapacity = DataflowBlockOptions.Unbounded
            }
        );
        bufferBlock.LinkTo(actionBlock, new() { PropagateCompletion = true });
        foreach (var initialValue in initialValues)
            bufferBlock.Post(initialValue);
        return actionBlock.Completion;
    }

    private static int GetThreadCount()
        => Math.Max(1, Math.Min((int)Math.Floor((decimal)Environment.ProcessorCount / 2), 10));

    #endregion

    #endregion

    #region Watcher Exclusions

    private FileWatcherService? _fileWatcherService;

    private bool _fileWatcherServiceResolved;

    /// <summary>
    ///   The file watcher, found on first use: it is built on this service
    ///   itself, and there is none before the server is up.
    /// </summary>
    private FileWatcherService? Watcher
    {
        get
        {
            if (!_fileWatcherServiceResolved)
            {
                _fileWatcherService = serviceProvider?.GetService(typeof(FileWatcherService)) as FileWatcherService;
                _fileWatcherServiceResolved = _fileWatcherService is not null;
            }

            return _fileWatcherService;
        }
    }

    /// <summary>
    ///   Runs a change with the paths it touches hidden from the file watcher,
    ///   so a move or a new file made here is not taken for one made by hand.
    /// </summary>
    private void Unwatched(Action action, params string?[] paths)
        => Unwatched(() =>
        {
            action();
            return true;
        }, paths);

    private T Unwatched<T>(Func<T> action, params string?[] paths)
    {
        var watcher = Watcher;
        var excluded = watcher is null ? [] : paths.OfType<string>().Where(path => path.Length > 0).Select(Path.GetFullPath).Distinct().ToList();
        foreach (var path in excluded)
            watcher!.AddFileWatcherExclusion(path);
        try
        {
            return action();
        }
        finally
        {
            foreach (var path in excluded)
                watcher!.RemoveFileWatcherExclusion(path);
        }
    }

    /// <summary>
    ///   Whether a move only changes the case of the name of one entry: the
    ///   two paths differ only in case and name the same entry, which happens
    ///   on a case-insensitive file system. Gives a name to move it through.
    /// </summary>
    private bool IsCaseOnlyRename(string sourcePath, string destinationPath, out string temporaryPath)
    {
        var source = Path.GetFullPath(sourcePath);
        var destination = Path.GetFullPath(destinationPath);
        temporaryPath = $"{destination}.{Guid.NewGuid():N}.tmp";
        if (string.Equals(source, destination, StringComparison.Ordinal) || !string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            return GetStat(source, followSymbolicLinks: false) is { } sourceStat && GetStat(destination, followSymbolicLinks: false) is { } destinationStat && sourceStat.Identity == destinationStat.Identity;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    #endregion
}
