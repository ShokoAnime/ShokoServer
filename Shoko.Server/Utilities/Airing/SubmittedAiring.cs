using System;

namespace Shoko.Server.Utilities.Airing;

/// <summary>
/// An episode airing as a provider submitted it, reduced to the values the delay
/// inference reads.
/// </summary>
public sealed record SubmittedAiring
{
    /// <summary>
    /// The airing's key, unique within its schedule. Either the provider's own
    /// key or the one derived from the episode.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// The key of the episode the airing is for, as
    /// <c>{EpisodeSource}:{EpisodeID}</c>.
    /// </summary>
    public required string EpisodeKey { get; init; }

    /// <summary>
    /// The submitted slot, in UTC. <see langword="null"/> means the provider
    /// reports no slot at all.
    /// </summary>
    public DateTime? AiredAt { get; init; }

    /// <summary>
    /// Optional. The first slot the provider says the episode was scheduled for.
    /// A value set here always wins over what the inference would have derived.
    /// </summary>
    public DateTime? OriginalAiredAt { get; init; }

    /// <summary>
    /// Optional. Whether the provider says this airing was postponed. A value set
    /// here always wins over the inferred cause detection.
    /// </summary>
    public bool? IsDelayed { get; init; }
}
