using System;
using Shoko.Abstractions.Release;
using Shoko.Abstractions.Video;

namespace Shoko.Abstractions.Events;

/// <summary>
/// Dispatched when a video release is deleted.
/// </summary>
public class VideoReleaseDeletedEventArgs : EventArgs
{
    /// <summary>
    /// The video, if available when the event was dispatched. It may have been
    /// removed from the database at this point though, so don't assume the
    /// locations or hash digests are always available when using it.
    /// </summary>
    public required IVideo? Video { get; init; }

    /// <summary>
    /// The release information for the video.
    /// </summary>
    public required IReleaseInfo ReleaseInfo { get; init; }
}
