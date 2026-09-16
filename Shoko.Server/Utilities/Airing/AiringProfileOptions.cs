namespace Shoko.Server.Utilities.Airing;

/// <summary>
/// What profile learning needs to know about the schedule the samples came from.
/// </summary>
public sealed record AiringProfileOptions
{
    /// <summary>
    /// Whether the schedule may anchor on the episode's earliest known Original
    /// airing: <see langword="true"/> when every one of its tracks is subtitled or
    /// dubbed. It still falls back to the AniDB date without enough anchored
    /// samples.
    /// </summary>
    public bool AnchorOnFirstOriginalAiring { get; init; }

    /// <summary>
    /// Whether the provider considers the run finished, so nothing may be
    /// estimated for it at all.
    /// </summary>
    public bool IsFinished { get; init; }

    /// <summary>
    /// Optional. The last episode the schedule covers. Estimates never run past
    /// it.
    /// </summary>
    public int? LastEpisodeNumber { get; init; }

    /// <summary>
    /// How many of the most recent samples to learn the offset from, so a
    /// long-running series follows its current time slot.
    /// </summary>
    public int Window { get; init; } = AiringScheduleUtility.DefaultWindow;

    /// <summary>
    /// How many samples are needed before an offset, an anchor or a trailing
    /// shift is trusted.
    /// </summary>
    public int MinimumSamples { get; init; } = 2;
}
