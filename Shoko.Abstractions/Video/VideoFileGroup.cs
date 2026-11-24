using System.Collections.Generic;

namespace Shoko.Abstractions.Video;

/// <summary>
///   A group of a video's locations that are one file on disk: the file, its
///   hard links and the symbolic links to it, or a single location.
/// </summary>
/// <remarks>
///   Which locations are grouped is stored, as last seen on disk when a
///   location was found, moved or scanned, so reading groups reads nothing
///   from disk. Locations are only grouped while
///   <see cref="Services.IVideoService.GroupLinkedFiles"/> is on; otherwise
///   every location is a group of its own.
/// </remarks>
public sealed record VideoFileGroup
{
    /// <summary>
    ///   The <see cref="IVideoFile.ID"/> of its primary location, the file
    ///   itself rather than a link to it where it is in the collection, which
    ///   names the group (<see cref="IVideoFile.PrimaryFileID"/>).
    /// </summary>
    public required int ID { get; init; }

    /// <summary>
    ///   Its locations: the primary first, then its other names by ID.
    /// </summary>
    public required IReadOnlyList<IVideoFile> Files { get; init; }

    /// <summary>
    ///   Whether any of its locations is kept (<see cref="IVideoFile.IsKept"/>).
    ///   Keeping one name keeps the whole group.
    /// </summary>
    public required bool IsKept { get; init; }

    /// <summary>
    ///   Whether it has more than one location.
    /// </summary>
    public bool IsLinked
        => Files.Count > 1;

    /// <summary>
    ///   Whether it is a group to keep rather than a duplicate: every kept
    ///   group when the video has any, otherwise the one group holding the
    ///   video's first available location. Every other group is a duplicate.
    /// </summary>
    public required bool IsPreferred { get; init; }
}
