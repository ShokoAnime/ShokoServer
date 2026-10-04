using System;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Video.Release;

namespace Shoko.Abstractions.Video.Events;

/// <summary>
/// Dispatched when a video release is saved or deleted.
/// </summary>
public class VideoReleaseSavedEventArgs : EventArgs
{
    /// <summary>
    /// The video.
    /// </summary>
    public required IVideo Video { get; init; }

    /// <summary>
    /// The release information for the video.
    /// </summary>
    public required IReleaseInfo ReleaseInfo { get; init; }

    /// <summary>
    ///   The API token of whoever caused the release to be saved, or
    ///   <c>null</c> when the system did it. Stamped when the event is
    ///   raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
