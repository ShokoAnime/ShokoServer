using System;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
///   Dispatched when an ordering is stored, changed or removed, global or a
///   user's own.
/// </summary>
public class OrderingChangedEventArgs : EventArgs
{
    /// <summary>
    ///   The ordering's ID.
    /// </summary>
    public required MetadataGuid OrderingID { get; init; }

    /// <summary>
    ///   The ID of the series the ordering orders.
    /// </summary>
    public required MetadataGuid SeriesID { get; init; }

    /// <summary>
    ///   The ordering as it is after the change, or <c>null</c> when it was
    ///   removed.
    /// </summary>
    public IOrdering? Ordering { get; init; }

    /// <summary>
    ///   The API token of whoever made the change, or <c>null</c> when the
    ///   system did it. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
