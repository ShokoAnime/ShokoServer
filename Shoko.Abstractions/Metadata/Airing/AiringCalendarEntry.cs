using System;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   An airing placed on a calendar, at the slot that falls in the range.
/// </summary>
/// <param name="Airing">The airing.</param>
/// <param name="Time">
///   When it is shown, in the calendar's time zone: its slot, or the slot
///   it was moved out of when only that falls in the range, or the start of
///   its day for a date-only entry.
/// </param>
/// <param name="IsAllDay">Whether it is a date-only entry, shown for the whole day.</param>
/// <param name="MovedFrom">
///   The slot it was moved out of, when it is shown at its new slot after a
///   delay; <c>null</c> otherwise.
/// </param>
/// <param name="MovedTo">
///   The slot it was moved to, when it is shown at the slot it was moved out
///   of; <c>null</c> otherwise.
/// </param>
public sealed record AiringCalendarEntry(
    IEpisodeAiring Airing,
    DateTimeOffset Time,
    bool IsAllDay,
    DateTimeOffset? MovedFrom,
    DateTimeOffset? MovedTo
);
