using System;
using Shoko.Abstractions.Release;
using Shoko.Abstractions.Video;

namespace Shoko.Abstractions.Events;

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
}
