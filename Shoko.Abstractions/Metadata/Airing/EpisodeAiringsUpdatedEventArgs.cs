using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Event arguments for episode airing events. This is dispatched when the
///   airings of one schedule are added, updated or removed, once per reason so
///   a batch write is a handful of events rather than one per airing.
/// </summary>
public class EpisodeAiringsUpdatedEventArgs : EventArgs
{
    /// <summary>
    ///   Why the event was dispatched, which is one of
    ///   <see cref="UpdateReason.Added"/>, <see cref="UpdateReason.Updated"/>
    ///   or <see cref="UpdateReason.Removed"/>.
    /// </summary>
    public required UpdateReason Reason { get; init; }

    /// <summary>
    ///   The schedule the airings belong to.
    /// </summary>
    public required IAiringSchedule Schedule { get; init; }

    /// <summary>
    ///   The airings that were added, updated or removed.
    /// </summary>
    public required IReadOnlyList<IEpisodeAiring> Airings { get; init; }
}
