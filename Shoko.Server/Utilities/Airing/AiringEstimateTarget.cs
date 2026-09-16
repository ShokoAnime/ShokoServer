using System;

namespace Shoko.Server.Utilities.Airing;

/// <summary>
/// An episode a schedule has no airing for, as estimate production reads it.
/// </summary>
public sealed record AiringEstimateTarget
{
    /// <summary>
    /// The key of the episode to estimate for.
    /// </summary>
    public required string EpisodeKey { get; init; }

    /// <summary>
    /// Optional. The episode's number in the schedule's numbering, checked
    /// against the profile's coverage.
    /// </summary>
    public int? EpisodeNumber { get; init; }

    /// <summary>
    /// Whether the episode is a normal episode. Specials are never estimated.
    /// </summary>
    public bool IsNormalEpisode { get; init; } = true;

    /// <summary>
    /// Optional. The episode's AniDB air date, taken as midnight UTC.
    /// </summary>
    public DateTime? AnidbAirDate { get; init; }

    /// <summary>
    /// Optional. The episode's earliest known real Original airing, anywhere, in
    /// UTC. An estimated Original airing is never passed here, so an estimate is
    /// never anchored on another estimate.
    /// </summary>
    public DateTime? FirstOriginalAiringAt { get; init; }
}
