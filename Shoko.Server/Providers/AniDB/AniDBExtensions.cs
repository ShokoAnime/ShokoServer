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

    public static int GetAniDBDateAsSeconds(DateTime? dtDate)
    {
        if (dtDate == null) return 0;
        var startDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var ts = dtDate.Value - startDate;
        return (int)ts.TotalSeconds;
    }
}
