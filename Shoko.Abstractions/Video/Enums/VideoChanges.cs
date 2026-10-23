using System;

namespace Shoko.Abstractions.Video.Enums;

/// <summary>
///   What changed on a video, as told by
///   <see cref="Events.VideoUpdatedEventArgs"/>.
/// </summary>
[Flags]
public enum VideoChanges : byte
{
    /// <summary>
    ///   Nothing changed.
    /// </summary>
    None = 0,

    /// <summary>
    ///   The video was marked as ignored, or unmarked.
    /// </summary>
    Ignored = 1,

    /// <summary>
    ///   The video was marked as a variation, or unmarked.
    /// </summary>
    Variation = 2,
}
