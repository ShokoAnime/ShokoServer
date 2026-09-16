using System;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Event arguments for airing channel events. This is dispatched when a
///   channel is registered, or when its aliases are updated.
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
