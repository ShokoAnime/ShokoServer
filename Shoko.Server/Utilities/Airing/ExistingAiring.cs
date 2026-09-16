using System;

namespace Shoko.Server.Utilities.Airing;

/// <summary>
/// An episode airing already stored for a schedule, as the delay inference sees it.
/// It carries no entities and no repository state, only the values the inference reads.
/// </summary>
public sealed record ExistingAiring
{
    /// <summary>
    /// The airing's key, unique within its schedule.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// The key of the episode the airing is for, as
    /// <c>{EpisodeSource}:{EpisodeID}</c>.
    /// </summary>
    public required string EpisodeKey { get; init; }

    /// <summary>
    /// Optional. The episode's number in the schedule's numbering, used to tell
    /// whether a removed airing is still inside the schedule's coverage.
    /// </summary>
    public int? EpisodeNumber { get; init; }

    /// <summary>
    /// The current slot, in UTC. <see langword="null"/> means the airing has no
    /// slot, which is how an indefinite postponement is stored.
    /// </summary>
    public DateTime? AiredAt { get; init; }

    /// <summary>
    /// The first slot the airing was scheduled for, in UTC, or
    /// <see langword="null"/> when it never moved.
    /// </summary>
    public DateTime? OriginalAiredAt { get; init; }

    /// <summary>
    /// Whether this airing's own slot was postponed.
    /// </summary>
    public bool IsDelayed { get; init; }

    /// <summary>
    /// Optional. The key of the link head this airing belongs to, or
    /// <see langword="null"/> when it isn't linked. The head carries its own key
    /// here, so a whole link set shares one value.
    /// </summary>
    public string? LinkKey { get; init; }
}
