using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shoko.Abstractions.Extensions;

/// <summary>
/// Writes time spans out as English text, in the invariant culture, for logs,
/// messages and UIs.
/// </summary>
public static class TimeSpanExtensions
{
    /// <summary>
    /// Writes a time span out as a duration in days, hours, minutes and
    /// seconds, leaving out the parts that are zero, as "6 hours",
    /// "1 hour 30 minutes" or "1 day 2 hours".
    /// </summary>
    /// <param name="timeSpan">
    /// The time span. A fraction of a second is dropped, so one under a second
    /// reads as "0 minutes", as does zero. A negative one reads as its length.
    /// </param>
    /// <returns>The text.</returns>
    public static string ToDurationString(this TimeSpan timeSpan)
    {
        var length = timeSpan == TimeSpan.MinValue ? TimeSpan.MaxValue : timeSpan.Duration();
        var parts = new List<string>(4);
        AddPart(parts, length.Days, "day");
        AddPart(parts, length.Hours, "hour");
        AddPart(parts, length.Minutes, "minute");
        AddPart(parts, length.Seconds, "second");
        return parts.Count is 0 ? "0 minutes" : string.Join(" ", parts);
    }

    /// <summary>
    /// Writes a time span out as how long ago something happened, so it reads
    /// after "it last ran": "less than a minute ago" under a minute, or else
    /// the two largest parts among days, hours and minutes that are not zero,
    /// as "1 hour ago", "2 hours 5 minutes ago" or "3 days 4 hours ago".
    /// </summary>
    /// <param name="elapsed">
    /// The time that has passed since. From a minute on it is rounded to the
    /// nearest minute first, and the parts past the two largest are dropped. A
    /// negative one, as from a time ahead of the clock, counts as zero.
    /// </param>
    /// <returns>The text.</returns>
    public static string ToTimeAgoString(this TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.FromMinutes(1))
            return "less than a minute ago";

        var minutes = elapsed.Ticks / TimeSpan.TicksPerMinute;
        if (elapsed.Ticks % TimeSpan.TicksPerMinute >= TimeSpan.TicksPerMinute / 2 && minutes < TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMinute)
            minutes++;

        var rounded = TimeSpan.FromTicks(minutes * TimeSpan.TicksPerMinute);
        var parts = new List<string>(3);
        AddPart(parts, rounded.Days, "day");
        AddPart(parts, rounded.Hours, "hour");
        AddPart(parts, rounded.Minutes, "minute");
        return $"{string.Join(" ", parts.GetRange(0, Math.Min(parts.Count, 2)))} ago";
    }

    /// <summary>
    /// Adds a part of a time span to the text, unless it is zero.
    /// </summary>
    /// <param name="parts">The parts written so far.</param>
    /// <param name="count">How many of the unit there are.</param>
    /// <param name="unit">The unit, in the singular.</param>
    private static void AddPart(List<string> parts, int count, string unit)
    {
        if (count > 0)
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{count} {unit}{(count is 1 ? string.Empty : "s")}"));
    }
}
