using System.Threading;
using Shoko.Abstractions.FileSystem.Services;

namespace Shoko.Abstractions.FileSystem;

/// <summary>
///   An entry of the file system as a listing found it: its path, whether it
///   is a symbolic link and, most often, whether it is a directory, known at
///   once, and what the file system says about it, read only when first asked
///   for and then kept.
/// </summary>
/// <remarks>
///   Handed out where most users only need the path, such as to a listing's
///   filter or an ignore rule, so a scan reads nothing it does not use. The
///   stat is never read by serializing an entry, except through
///   <see cref="IsDirectory"/> of a link the listing left unread.
/// </remarks>
public sealed class FileSystemEntry
{
    private readonly IFileSystem _fileSystem;

    private readonly Lock _lock = new();

    private bool _isRead;

    private bool? _isDirectory;

    private FileSystemStat? _stat;

    /// <summary>
    ///   Creates an entry.
    /// </summary>
    /// <param name="fileSystem">The file system to read the stat from.</param>
    /// <param name="path">The entry's absolute path.</param>
    /// <param name="isDirectory">
    ///   Whether it is a directory, or a link to one; <c>null</c> to read it
    ///   from the file system when first asked for.
    /// </param>
    /// <param name="isSymbolicLink">Whether it is a symbolic link.</param>
    public FileSystemEntry(IFileSystem fileSystem, string path, bool? isDirectory, bool isSymbolicLink = false)
    {
        _fileSystem = fileSystem;
        Path = path;
        Name = System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(path));
        _isDirectory = isDirectory;
        IsSymbolicLink = isSymbolicLink;
    }

    /// <summary>
    ///   The entry's absolute path.
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///   The entry's name, the last part of its path.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///   Whether it is a directory, or a link to one, as the listing found it.
    ///   A listing that does not need to know what a link points to leaves it
    ///   for the first access, which reads <see cref="Stat"/>: a link whose
    ///   target is gone or cannot be read is not a directory.
    /// </summary>
    public bool IsDirectory
    {
        get
        {
            lock (_lock)
            {
                if (_isDirectory is { } isDirectory)
                    return isDirectory;
            }

            bool resolved;
            try
            {
                resolved = Stat is DirectoryStat;
            }
            catch (System.Exception ex) when (ex is System.UnauthorizedAccessException or System.IO.IOException)
            {
                resolved = false;
            }

            lock (_lock)
                return _isDirectory ??= resolved;
        }
    }

    /// <summary>
    ///   Whether it is a symbolic link (a junction too, on Windows), as the
    ///   listing found it. <see cref="Stat"/> reads what it points to; read
    ///   the link itself with <see cref="IFileSystem.GetStat"/> and
    ///   <c>followSymbolicLinks: false</c>.
    /// </summary>
    public bool IsSymbolicLink { get; }

    /// <summary>
    ///   What the file system says about the entry, with symbolic links
    ///   followed: read on first access by <see cref="IFileSystem.GetStat"/>
    ///   and kept. <c>null</c> when it is gone by then.
    /// </summary>
    /// <exception cref="System.UnauthorizedAccessException">The entry may not be read.</exception>
    /// <exception cref="System.IO.IOException">
    ///   It could not be read for another reason. A failed read is not kept,
    ///   so the next access tries again.
    /// </exception>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public FileSystemStat? Stat
    {
        get
        {
            lock (_lock)
            {
                if (!_isRead)
                {
                    _stat = _fileSystem.GetStat(Path);
                    _isRead = true;
                }

                return _stat;
            }
        }
    }

    /// <summary>
    ///   <see cref="Stat"/>, when the entry is a file.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public FileStat? FileStat
        => Stat as FileStat;

    /// <summary>
    ///   <see cref="Stat"/>, when the entry is a directory.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public DirectoryStat? DirectoryStat
        => Stat as DirectoryStat;

    /// <inheritdoc/>
    public override string ToString()
        => Path;
}
