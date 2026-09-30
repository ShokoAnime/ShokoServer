using System;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Video.Release;

namespace Shoko.Abstractions.Video.Events;

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

    /// <summary>
    /// The new release information replacing the deleted one, if any.
    /// </summary>
    public required IReleaseInfo? NewReleaseInfo { get; init; }

    /// <summary>
    ///   The API token of whoever caused the release to be deleted, or
    ///   <see langword="null"/> when the system did it. Stamped when the event is
    ///   raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
