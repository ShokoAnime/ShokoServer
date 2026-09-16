using System;

namespace Shoko.Server.Utilities.Airing;

/// <summary>
/// One of a schedule's own airings, as profile learning and the cadence helper
/// read it. Estimates are never samples, so only stored airings belong here.
/// </summary>
public sealed record AiringProfileSample
{
    /// <summary>
    /// The key of the episode the airing is for.
    /// </summary>
    public required string EpisodeKey { get; init; }

    /// <summary>
    /// Optional. The episode's number in the schedule's numbering.
    /// </summary>
    public int? EpisodeNumber { get; init; }

    /// <summary>
    /// Optional. The episode's AniDB air date, taken as midnight UTC.
    /// </summary>
    public DateTime? AnidbAirDate { get; init; }

    /// <summary>
    /// The airing's current slot, in UTC, or <see langword="null"/> when it has
    /// none.
    /// </summary>
    public DateTime? AiredAt { get; init; }

    /// <summary>
    /// The slot the airing was first scheduled for, in UTC. Read for a slotless,
    /// delayed airing, where it is where this schedule's hiatus starts.
    /// </summary>
    public DateTime? OriginalAiredAt { get; init; }

    /// <summary>
    /// Whether this airing's own slot was postponed. A delayed airing is never a
    /// sample for the offset.
    /// </summary>
    public bool IsDelayed { get; init; }

    /// <summary>
    /// Optional. The key of the link head this airing belongs to. A link set
    /// counts once, from its earliest member, in every step and in every minimum.
    /// </summary>
    public string? LinkKey { get; init; }

    /// <summary>
    /// Optional. The episode's earliest known real Original airing, anywhere, in
    /// UTC. An estimated Original airing is never passed here.
    /// </summary>
    public DateTime? FirstOriginalAiringAt { get; init; }
}
