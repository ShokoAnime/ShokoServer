using System;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Video.Enums;

namespace Shoko.Abstractions.Video.Events;

/// <summary>
///   Dispatched when a video's own settings change, such as being marked as
///   ignored or as a variation.
/// </summary>
public class VideoUpdatedEventArgs : EventArgs
{
    /// <summary>
    ///   The video, as it is after the change.
    /// </summary>
    public required IVideo Video { get; init; }

    /// <summary>
    ///   What changed. Each flag named holds its new value on
    ///   <see cref="Video"/>, and held the opposite before.
    /// </summary>
    public required VideoChanges Changes { get; init; }

    /// <summary>
    ///   The API token of whoever made the change, or <c>null</c> when the
    ///   system did it. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
