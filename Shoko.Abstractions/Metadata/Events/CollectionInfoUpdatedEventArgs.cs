using System;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
/// Dispatched when collection data was updated.
/// </summary>
public class CollectionInfoUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// The reason this updated event was dispatched.
    /// </summary>
    public UpdateReason Reason { get; private set; }

    /// <summary>
    /// This is the full data. A diff was not performed for this. A removed
    /// collection is as it was before.
    /// </summary>
    public ICollection CollectionInfo { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionInfoUpdatedEventArgs"/> class.
    /// </summary>
    /// <param name="collectionInfo">The collection info.</param>
    /// <param name="reason">The reason it was updated.</param>
    public CollectionInfoUpdatedEventArgs(ICollection collectionInfo, UpdateReason reason)
    {
        Reason = reason;
        CollectionInfo = collectionInfo;
    }

    /// <summary>
    ///   The API token of whoever caused the update, or <c>null</c>
    ///   when the system did it. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
