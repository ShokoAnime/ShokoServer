using System;

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

    /// <summary>
    ///   What is at the path the link points to, read without following it,
    ///   when first asked for and then kept: one step, so a link to a link
    ///   gives the second link's own stat, whose <see cref="TargetStat"/> is
    ///   not read. <c>null</c> when nothing is there or it cannot be read, so
    ///   a broken link has none, and on a target stat itself.
    /// </summary>
    public FileSystemStat? TargetStat
        => LazyTargetStat?.Value;

    /// <summary>
    ///   Reads <see cref="TargetStat"/> when it is first asked for; set by the
    ///   file system service that read the link.
    /// </summary>
    internal Lazy<FileSystemStat?>? LazyTargetStat { get; init; }

    /// <inheritdoc/>
    public bool Equals(SymbolicLinkStat? other)
        => other is not null && base.Equals(other) && Target == other.Target && IsDirectoryLink == other.IsDirectoryLink;

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Target, IsDirectoryLink);
}
