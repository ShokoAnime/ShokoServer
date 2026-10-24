using System;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
///   Dispatched when a group update occurs.
/// </summary>
public class GroupInfoUpdatedEventArgs : EventArgs
{
    /// <summary>
    ///   The reason for the update.
    /// </summary>
    public UpdateReason Reason { get; init; }

    /// <summary>
    ///   The group info.
    /// </summary>
    public IShokoGroup GroupInfo { get; init; }

    /// <summary>
    ///   Initializes a new instance of the <see cref="GroupInfoUpdatedEventArgs"/> class.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="reason">The reason for the update.</param>
    public GroupInfoUpdatedEventArgs(IShokoGroup group, UpdateReason reason)
    {
        GroupInfo = group;
        Reason = reason;
    }

    /// <summary>
    ///   The API token of whoever made the change, or <c>null</c> when the
    ///   system did it. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
