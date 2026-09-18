using System;

#nullable enable
namespace Shoko.Server.API.v3.Models.Airing;

/// <summary>
/// Keeps every timestamp the airing schedule models put on the wire in UTC.
/// </summary>
/// <remarks>
/// Every airing timestamp in this feature is UTC, but they don't all arrive
/// knowing it: a stored airing comes back from the database with no kind at
/// all, while an estimate is produced as UTC. Serialized as they are, the same
/// field would carry a <c>Z</c> for one airing and none for the next, so a
/// kindless value is read as the UTC it already is rather than converted from
/// local time.
/// </remarks>
internal static class AiringDateTimeExtensions
{
    /// <summary>
    /// Reads <paramref name="value"/> as UTC.
    /// </summary>
    /// <param name="value">The timestamp.</param>
    /// <returns>The same point in time, as a UTC <see cref="DateTime"/>.</returns>
    public static DateTime ToUtc(this DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value.ToUniversalTime(),
        };

    /// <summary>
    /// Reads <paramref name="value"/> as UTC, when it has a value.
    /// </summary>
    /// <param name="value">The timestamp, or <c>null</c>.</param>
    /// <returns>The same point in time, as a UTC <see cref="DateTime"/>, or <c>null</c>.</returns>
    public static DateTime? ToUtc(this DateTime? value)
        => value is { } dateTime ? dateTime.ToUtc() : null;
}
