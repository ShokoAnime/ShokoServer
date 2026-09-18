using System;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   One episode on one airing schedule, as a provider submits it.
/// </summary>
public sealed record EpisodeAiringData
{
    /// <summary>
    ///   The episode the airing is for. It must belong to the schedule's
    ///   series, and to its season when one is set.
    /// </summary>
    public required IEpisode Episode { get; init; }

    /// <summary>
    ///   When the episode airs, in UTC. <c>null</c> means the airing has no
    ///   slot, which is how a source indicates an indefinite postponement.
    /// </summary>
    public required DateTime? AiredAt { get; init; }

    /// <summary>
    ///   Optional. A key that is stable for this schedule. <c>null</c> derives
    ///   one from the episode.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    ///   Optional. The episode's own page, overriding the schedule's URL.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    ///   Optional. The slot the episode was first scheduled for, in UTC.
    ///   <c>null</c> lets the service keep or infer it.
    /// </summary>
    public DateTime? OriginalAiredAt { get; init; }

    /// <summary>
    ///   Optional. Whether this airing's own slot was postponed. <c>null</c>
    ///   lets the service infer it.
    /// </summary>
    public bool? IsDelayed { get; init; }
}
