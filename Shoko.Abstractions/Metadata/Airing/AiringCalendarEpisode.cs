using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   The airings of one episode on one day of a calendar, shown together.
///   An airing of an unknown episode stands alone.
/// </summary>
/// <param name="Lead">
///   The airing shown first: the preferred one, else the first timed one,
///   else the date-only one.
/// </param>
/// <param name="Others">
///   The episode's other airings that day, the date-only one first and the
///   rest in time order; empty when the calendar shows the leads only.
/// </param>
public sealed record AiringCalendarEpisode(AiringCalendarEntry Lead, IReadOnlyList<AiringCalendarEntry> Others);
