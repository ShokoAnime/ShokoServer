using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Event arguments for airing channel events. One change can raise several:
///   the IDs it took away as removed first, then the channel kept as added or
///   updated. <see cref="Kind"/> tells what happened, and the other members
///   link the events of one change and carry what the channel was before.
/// </summary>
public class AiringChannelEventArgs : EventArgs
{
    /// <summary>
    ///   Why the event was dispatched, which is one of
    ///   <see cref="UpdateReason.Added"/>, <see cref="UpdateReason.Updated"/>
    ///   or <see cref="UpdateReason.Removed"/>.
    /// </summary>
    public required UpdateReason Reason { get; init; }

    /// <summary>
    ///   What happened to the channel. Use it to tell a provider's change from
    ///   a hand-made one, as <see cref="Actor"/> alone cannot.
    /// </summary>
    public required AiringChannelChangeKind Kind { get; init; }

    /// <summary>
    ///   The channel that was registered, updated or removed. A removed one is
    ///   as it was before the change.
    /// </summary>
    public required IAiringChannel Channel { get; init; }

    /// <summary>
    ///   The ID the channel had before the change gave it a new one, on the
    ///   event for the channel kept, or <c>null</c> when its ID stayed.
    /// </summary>
    public Guid? PreviousChannelID { get; init; }

    /// <summary>
    ///   The ID of the channel now holding what a removed channel had, on a
    ///   <see cref="UpdateReason.Removed"/> event, or <c>null</c> otherwise.
    /// </summary>
    public Guid? TargetChannelID { get; init; }

    /// <summary>
    ///   The aliases the channel kept had before the change, on the event for
    ///   it after an alias change, a merge or a country taken or moved, or
    ///   <c>null</c> otherwise.
    /// </summary>
    public IReadOnlyList<string>? PreviousAliases { get; init; }

    /// <summary>
    ///   Whether the channel kept was hidden before the change, on the event
    ///   for it when the change hid or showed it, or <c>null</c> otherwise.
    /// </summary>
    public bool? PreviousIsHidden { get; init; }

    /// <summary>
    ///   The channels merged into the channel kept, as they were, on the event
    ///   for it. Empty when nothing was merged into it.
    /// </summary>
    public IReadOnlyList<IAiringChannel> MergedChannels { get; init; } = [];

    /// <summary>
    ///   The aliases a merge gave the channel kept, from the names of the
    ///   channels merged into it, on the event for it. Empty otherwise.
    /// </summary>
    public IReadOnlyList<string> AddedAliases { get; init; } = [];

    /// <summary>
    ///   The API token of whoever made the change, or <c>null</c> when the
    ///   system did it. Stamped when the event is raised.
    /// </summary>
    /// <remarks>
    ///   A provider's own changes run with the actor of the job that queued its
    ///   work, so read <see cref="Kind"/> as well:
    ///   <see cref="AiringChannelChangeKind.Registered"/>,
    ///   <see cref="AiringChannelChangeKind.CountryTaken"/> and
    ///   <see cref="AiringChannelChangeKind.CountryMoved"/> are a provider's.
    /// </remarks>
    public ApiToken? Actor { get; init; }
}
