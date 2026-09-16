using System;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Event arguments for airing schedule events. This is dispatched when an
///   airing schedule is added, updated or removed.
/// </summary>
public class AiringScheduleEventArgs : EventArgs
{
    /// <summary>
    ///   Why the event was dispatched, which is one of
    ///   <see cref="UpdateReason.Added"/>, <see cref="UpdateReason.Updated"/>
    ///   or <see cref="UpdateReason.Removed"/>.
    /// </summary>
    public required UpdateReason Reason { get; init; }

    /// <summary>
    ///   The schedule that was added, updated or removed.
    /// </summary>
    public required IAiringSchedule Schedule { get; init; }
}
