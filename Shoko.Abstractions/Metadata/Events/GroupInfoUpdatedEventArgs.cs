using System;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
///   Dispatched when a group update occurs.
/// </summary>
/// <param name="group">The group.</param>
/// <param name="reason">The reason for the update.</param>
public class GroupInfoUpdatedEventArgs(IShokoGroup group, UpdateReason reason) : EventArgs
{
    /// <summary>
    ///   The reason for the update.
    /// </summary>
    public UpdateReason Reason { get; init; } = reason;

    /// <summary>
    ///   The group info.
    /// </summary>
    public IShokoGroup GroupInfo { get; init; } = group;

    /// <summary>
    ///   The API token of whoever made the change, or <c>null</c> when the
    ///   system did it. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
