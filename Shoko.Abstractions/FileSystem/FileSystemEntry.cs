using System.Threading;
using Shoko.Abstractions.FileSystem.Services;

namespace Shoko.Abstractions.FileSystem;

/// <summary>
///   An entry of the file system as a listing found it: its path and whether
///   it is a directory, known at once, and what the file system says about
///   it, read only when first asked for and then kept.
/// </summary>
/// <remarks>
///   Handed out where most users only need the path, such as to a listing's
///   filter or an ignore rule, so a scan reads nothing it does not use. The
///   stat is never read by serializing an entry.
/// </remarks>
public sealed class FileSystemEntry
{
    private readonly IFileSystem _fileSystem;

    private readonly Lock _lock = new();

    private bool _isRead;

    private FileSystemStat? _stat;

    /// <summary>
    ///   Creates an entry.
    /// </summary>
    /// <param name="fileSystem">The file system to read the stat from.</param>
    /// <param name="path">The entry's absolute path.</param>
    /// <param name="isDirectory">Whether it is a directory.</param>
    public FileSystemEntry(IFileSystem fileSystem, string path, bool isDirectory)
    {
        _fileSystem = fileSystem;
        Path = path;
        Name = System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(path));
        IsDirectory = isDirectory;
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
    ///   Whether it is a directory, as the listing found it.
    /// </summary>
    public bool IsDirectory { get; }

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
