using System;

namespace Shoko.Abstractions.Metadata.Anidb.Enums;

/// <summary>
///   Which watched states to import from the AniDB MyList into the local
///   user data. Only applies to the video-level operations, since importing
///   a state requires a local video and user to import it onto.
/// </summary>
[Flags]
public enum MyListReadStates : sbyte
{
    /// <summary>
    ///   Use the read states configured in the server settings.
    /// </summary>
    Auto = -1,

    /// <summary>
    ///   Do not import any watched states.
    /// </summary>
    None = 0,

    /// <summary>
    ///   Import the watched state when the entry is watched on AniDB but not
    ///   watched locally.
    /// </summary>
    Watched = 1,

    /// <summary>
    ///   Import the unwatched state when the entry is not watched on AniDB
    ///   but watched locally.
    /// </summary>
    Unwatched = 2,

    /// <summary>
    ///   Import the watched state in either direction.
    /// </summary>
    Default = Watched | Unwatched,
}
