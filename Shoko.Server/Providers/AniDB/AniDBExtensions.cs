using System;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Providers.AniDB;

public static class AniDBExtensions
{
    public static DateTime? GetAniDBDateAsDate(int secs)
    {
        if (secs == 0) return null;
        var thisDate = new DateTime(1970, 1, 1, 0, 0, 0);
        thisDate = thisDate.AddSeconds(secs);
        return thisDate;
    }

    public static DateOnly? GetAniDBDateAsDateOnly(int secs)
        => GetAniDBDateAsDate(secs) is { } date ? DateOnly.FromDateTime(date) : null;

    public static PartialDateOnly? GetAniDBDateAsPartialDateOnly(int secs)
        => GetAniDBDateAsDate(secs) is { Year: > 0, Month: > 0, Day: > 0 } date ? PartialDateOnly.FromDateTime(date) : null;

    /// <summary>
    /// Drops sub-second precision, which the AniDB wire format — unix seconds —
    /// cannot carry. Applying this to a date before sending it means the value
    /// held locally is exactly the value AniDB will report back, so an
    /// optimistically cached entry compares equal to the fetched one.
    /// </summary>
    public static DateTime? TruncateToAniDBPrecision(DateTime? dtDate)
        => dtDate is { } date ? new DateTime(date.Ticks - date.Ticks % TimeSpan.TicksPerSecond, date.Kind) : null;

    /// <summary>
    /// Converts a point in time to the unix seconds AniDB expects.
    ///
    /// AniDB works in UTC, so a <see cref="DateTimeKind.Local"/> value is
    /// converted first. Sending its wall clock as-is would skew the value by the
    /// local offset, which is how watched dates ended up ahead of themselves on
    /// AniDB for years.
    ///
    /// <see cref="DateTimeKind.Unspecified"/> is taken at face value instead of
    /// converted: the callers passing one are sending a calendar date, such as
    /// an air date, rather than an instant, and shifting midnight by an offset
    /// can land it on the wrong day.
    /// </summary>
    public static int GetAniDBDateAsSeconds(DateTime? dtDate)
    {
        if (dtDate is not { } date) return 0;

        if (date.Kind is DateTimeKind.Local)
            date = date.ToUniversalTime();

        return (int)(date - DateTime.UnixEpoch).TotalSeconds;
    }
}
