using System;

namespace Shoko.Abstractions.FileSystem;

/// <summary>
///   What the file system says about an entry, read in one go: a
///   <see cref="FileStat"/>, a <see cref="DirectoryStat"/>, a
///   <see cref="SpecialFileStat"/> for a pipe, socket or device, or, when
///   links are not followed, a <see cref="SymbolicLinkStat"/>.
/// </summary>
/// <remarks>
///   A snapshot of the moment it was read. Times are in UTC.
/// </remarks>
public abstract record FileSystemStat
{
    /// <summary>
    ///   The path it was read for.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    ///   The volume and the ID on it.
    /// </summary>
    public required StatIdentity Identity { get; init; }

    /// <summary>
    ///   How many names the entry has. For a file, more than one means it has
    ///   hard links. For a directory it depends on the file system (on Unix
    ///   usually two plus its subdirectories, on btrfs always one), so do not
    ///   read anything into it.
    /// </summary>
    public required long LinkCount { get; init; }

    /// <summary>
    ///   When the contents last changed.
    /// </summary>
    public required DateTime LastWriteTime { get; init; }

    /// <summary>
    ///   When it was last read. Many systems update it lazily or never
    ///   (<c>noatime</c>, <c>relatime</c>), so treat it as a hint.
    /// </summary>
    public required DateTime LastAccessTime { get; init; }

    /// <summary>
    ///   When its contents or its metadata (name, links, permissions) last
    ///   changed: <c>ctime</c> on Unix, the change time on Windows. Not the
    ///   creation time.
    /// </summary>
    public DateTime? ChangeTime { get; init; }

    /// <summary>
    ///   When it was created, or <c>null</c> when the platform or the file
    ///   system does not keep it (Unix other than Linux, or a Linux file
    ///   system without birth times).
    /// </summary>
    public DateTime? CreationTime { get; init; }
}
