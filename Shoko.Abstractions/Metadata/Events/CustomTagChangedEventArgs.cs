using System;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
///   Dispatched when a custom tag is added, updated or removed.
/// </summary>
public class CustomTagChangedEventArgs : EventArgs
{
    /// <summary>
    ///   The custom tag, as it is after the change. A removed one is as it was
    ///   before.
    /// </summary>
    public required IShokoTag Tag { get; init; }

    /// <summary>
    ///   The name the tag had before an update changed it, or <c>null</c> when
    ///   the name stayed or the tag was added or removed.
    /// </summary>
    public string? PreviousName { get; init; }

    /// <summary>
    ///   The overview the tag had before an update changed it, or <c>null</c>
    ///   when the overview stayed or the tag was added or removed.
    /// </summary>
    public string? PreviousOverview { get; init; }

    /// <summary>
    ///   The API token of whoever made the change, or <c>null</c> when the
    ///   system did it. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
