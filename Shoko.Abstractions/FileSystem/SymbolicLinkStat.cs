namespace Shoko.Abstractions.FileSystem;

/// <summary>
///   What the file system says about a symbolic link itself, read without
///   following it. The identity and times are the link's own, not its
///   target's.
/// </summary>
public sealed record SymbolicLinkStat : FileSystemStat
{
    /// <summary>
    ///   The path the link points to, as stored in it: possibly relative to
    ///   the link's folder, and possibly naming nothing.
    /// </summary>
    public string? Target { get; init; }

    /// <summary>
    ///   Whether it is a directory link, made to point to a directory, or a
    ///   file link. Windows makes the two apart and keeps the kind on the link
    ///   itself, so it is known there even when the target is gone. A Unix
    ///   link has no kind of its own, so it is <c>null</c> there: read what it
    ///   points to by following it.
    /// </summary>
    public bool? IsDirectoryLink { get; init; }
}
