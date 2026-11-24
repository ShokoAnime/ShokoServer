using System;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Video.Events;

/// <summary>
///   Dispatched when a video file location is marked as kept, or unmarked.
/// </summary>
public class VideoFileKeptChangedEventArgs : EventArgs
{
    /// <summary>
    ///   The video file location, as it is after the change.
    /// </summary>
    public required IVideoFile File { get; init; }

    /// <summary>
    ///   Whether it is kept now. It was the opposite before.
    /// </summary>
    public required bool IsKept { get; init; }

    /// <summary>
    ///   The API token of whoever made the change, or <c>null</c> when the
    ///   system did it. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
