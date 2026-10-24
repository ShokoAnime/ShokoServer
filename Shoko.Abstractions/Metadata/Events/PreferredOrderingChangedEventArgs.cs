using System;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
///   Dispatched when the ordering chosen for a series changes, by hand or
///   because the chosen one was removed.
/// </summary>
public class PreferredOrderingChangedEventArgs : EventArgs
{
    /// <summary>
    ///   The ID of the series.
    /// </summary>
    public required MetadataGuid SeriesID { get; init; }

    /// <summary>
    ///   The ordering chosen before, or <c>null</c> when the default one was
    ///   in use.
    /// </summary>
    public MetadataGuid? PreviousOrderingID { get; init; }

    /// <summary>
    ///   The ordering chosen now, or <c>null</c> when the default one is.
    /// </summary>
    public MetadataGuid? OrderingID { get; init; }

    /// <summary>
    ///   The API token of whoever made the change, or <c>null</c> when the
    ///   system did it. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
