using System;

namespace Shoko.Server.Providers.AniDB;

public static class AniDBExtensions
{
    /// <summary>
    ///   Converts AniDB's unix seconds to a date. <c>0</c> is AniDB's
    ///   1970-01-01 placeholder for a date before 1970, so it gives no date.
    /// </summary>
    /// <param name="secs">The seconds, or <c>null</c> when there is no date.</param>
    /// <returns>The date, or <c>null</c> for no date and for the placeholder.</returns>
    public static DateTime? GetAniDBDateAsDate(int? secs)
    {
        if (secs is not { } seconds || seconds == 0) return null;
        var thisDate = new DateTime(1970, 1, 1, 0, 0, 0);
        thisDate = thisDate.AddSeconds(seconds);
        return thisDate;
    }

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

    /// <summary>
    ///   Converts an episode's air date to the unix seconds it is stored as,
    ///   as <see cref="GetAniDBDateAsSeconds"/> does, but keeps a missing date
    ///   apart from AniDB's 1970-01-01 placeholder.
    /// </summary>
    /// <param name="airDate">The air date, or <c>null</c> when AniDB gave none.</param>
    /// <returns>The seconds, <c>0</c> for the placeholder, or <c>null</c> when there is no date.</returns>
    public static int? GetAniDBAirDateAsSeconds(DateTime? airDate)
        => airDate is { } date ? GetAniDBDateAsSeconds(date) : null;
}
