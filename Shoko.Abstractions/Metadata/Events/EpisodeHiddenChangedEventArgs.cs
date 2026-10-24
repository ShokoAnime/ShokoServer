using System;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
///   Dispatched when an episode of any source is hidden or shown.
/// </summary>
public class EpisodeHiddenChangedEventArgs : EventArgs
{
    /// <summary>
    ///   The ID of the episode.
    /// </summary>
    public required MetadataGuid EpisodeID { get; init; }

    /// <summary>
    ///   Whether the episode is hidden now.
    /// </summary>
    public required bool IsHidden { get; init; }

    /// <summary>
    ///   The API token of whoever made the change, or <c>null</c> when the
    ///   system did it. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
