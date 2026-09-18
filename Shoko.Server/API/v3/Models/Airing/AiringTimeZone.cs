using System;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Airing;

/// <summary>
/// The time zone a schedule's source reports its times in. Times themselves go
/// over the wire in UTC; this is what a client needs to render them the way the
/// source wrote them, e.g. as <c>23:30 JST</c>, without a second lookup.
/// </summary>
/// <remarks>
/// <see cref="ID"/> is always set, even for a zone this host cannot resolve.
/// Everything else is only filled in when <see cref="IsResolved"/> is
/// <c>true</c>. Nothing on the REST API writes a time zone, so this object is
/// output only.
/// </remarks>
public class AiringTimeZone
{
    /// <summary>
    /// The ID of the time zone, as stored: an IANA id such as
    /// <c>Asia/Tokyo</c>, or a fixed <c>±HH:MM</c> offset.
    /// </summary>
    [Required]
    public string ID { get; init; }

    /// <summary>
    /// Whether this host could resolve <see cref="ID"/> to a real time zone.
    /// </summary>
    [Required]
    public bool IsResolved { get; init; }

    /// <summary>
    /// The display name of the time zone, or <c>null</c> when it is
    /// unresolved.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// The standard name of the time zone, or <c>null</c> when it is
    /// unresolved.
    /// </summary>
    public string? StandardName { get; init; }

    /// <summary>
    /// The time zone's base offset from UTC, or <c>null</c> when it is
    /// unresolved.
    /// </summary>
    public TimeSpan? BaseUtcOffset { get; init; }

    /// <summary>
    /// The time zone's offset from UTC right now, which differs from
    /// <see cref="BaseUtcOffset"/> while daylight saving time is in effect, or
    /// <c>null</c> when it is unresolved.
    /// </summary>
    public TimeSpan? CurrentUtcOffset { get; init; }

    /// <summary>
    /// Whether the time zone observes daylight saving time, or <c>null</c> when
    /// it is unresolved.
    /// </summary>
    public bool? SupportsDaylightSavingTime { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AiringTimeZone"/> class for
    /// a stored time zone ID and, when this host knows it, the zone it resolved
    /// to.
    /// </summary>
    /// <param name="id">The stored time zone ID.</param>
    /// <param name="zone">The resolved time zone, or <c>null</c> when this host cannot resolve the ID.</param>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    public AiringTimeZone(string id, TimeZoneInfo? zone)
    {
        ArgumentNullException.ThrowIfNull(id);

        ID = id;
        IsResolved = zone is not null;
        if (zone is null)
            return;

        DisplayName = zone.DisplayName;
        StandardName = zone.StandardName;
        BaseUtcOffset = zone.BaseUtcOffset;
        CurrentUtcOffset = zone.GetUtcOffset(DateTime.UtcNow);
        SupportsDaylightSavingTime = zone.SupportsDaylightSavingTime;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AiringTimeZone"/> class for
    /// a resolved time zone.
    /// </summary>
    /// <param name="zone">The time zone.</param>
    /// <exception cref="ArgumentNullException"><paramref name="zone"/> is <c>null</c>.</exception>
    public AiringTimeZone(TimeZoneInfo zone) : this(zone?.Id!, zone) { }
}
