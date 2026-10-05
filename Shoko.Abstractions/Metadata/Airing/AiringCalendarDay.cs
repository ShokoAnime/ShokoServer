using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   One day of a calendar, in its time zone, with what airs on it.
/// </summary>
/// <param name="Date">The local date.</param>
/// <param name="Episodes">
///   The day's episodes, ordered by their lead airings: the date-only
///   entries first, then by time.
/// </param>
public sealed record AiringCalendarDay(DateOnly Date, IReadOnlyList<AiringCalendarEpisode> Episodes);
