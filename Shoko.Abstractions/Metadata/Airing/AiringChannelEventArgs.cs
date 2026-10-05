using System;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Event arguments for airing channel events. This is dispatched when a
///   channel is registered, when its aliases are updated, for both sides of a
///   merge (the merged channels as removed, the target as updated), and when a
///   channel takes a country (its old ID as removed, its new one as added).
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
    ///   The channel that was registered, updated or removed.
    /// </summary>
    public required IAiringChannel Channel { get; init; }
}
