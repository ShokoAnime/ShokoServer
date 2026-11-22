using System.Collections.Generic;

namespace Shoko.Abstractions.FileSystem;

/// <summary>
///   How a listing of the file system walks and what it keeps.
/// </summary>
public sealed record FileSystemListingOptions
{
    /// <summary>
    ///   The options a listing takes when given none: only the directory
    ///   itself, everything in it, links included.
    /// </summary>
    public static FileSystemListingOptions Default { get; } = new();

    /// <summary>
    ///   Whether to look in subdirectories too.
    /// </summary>
    public bool Recursive { get; init; }

    /// <summary>
    ///   Only list files with these extensions, such as <c>.mkv</c>, compared
    ///   ignoring case. Directories are not filtered by them. <c>null</c>
    ///   lists every file.
    /// </summary>
    public IReadOnlyCollection<string>? Extensions { get; init; }

    /// <summary>
    ///   Decides which entries to keep, after the other options. A directory
    ///   left out is not walked either.
    /// </summary>
    public FileSystemEntryFilter? Filter { get; init; }

    /// <summary>
    ///   Whether a recursive listing walks into links to directories. Off by
    ///   default: such a link can lead out of the directory, or back up into
    ///   it. When on, a directory reached through a link is never walked
    ///   twice, so a link back up the tree ends.
    /// </summary>
    public bool FollowDirectoryLinks { get; init; }

    /// <summary>
    ///   Whether to list links to directories, walked into or not.
    /// </summary>
    public bool IncludeDirectoryLinks { get; init; } = true;

    /// <summary>
    ///   Whether to list links to files, and links whose target is gone.
    /// </summary>
    public bool IncludeFileLinks { get; init; } = true;

    /// <summary>
    ///   Whether a directory that cannot be read throws rather than being left
    ///   out: the one listed (<see cref="System.IO.DirectoryNotFoundException"/>
    ///   when it is not there) or any it walks into. Off by default, when a
    ///   missing directory lists nothing and one that cannot be read is logged
    ///   and skipped, so an empty listing cannot be told from a failed one.
    /// </summary>
    public bool ThrowOnError { get; init; }
}
