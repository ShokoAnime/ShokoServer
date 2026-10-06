using System;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   One episode on one airing schedule, as a provider submits it: its place
///   on the schedule's numbered line, an episode to pin it to, or both.
/// </summary>
/// <remarks>
///   <list type="bullet">
///     <item>A sequence number alone is resolved when the airing is read: to
///     the regular episode numbered <c>FirstEpisodeNumber + n - 1</c> in the
///     schedule's series or season, or to no episode yet.</item>
///     <item>A regular episode alone takes the sequence number its own number
///     gives it.</item>
///     <item>Any other episode alone is pinned to it, off the numbered
///     line.</item>
///     <item>Both pin the airing to the episode, and the sequence number places
///     it on the line.</item>
///   </list>
/// </remarks>
public sealed record EpisodeAiringData
{
    /// <summary>
    ///   Optional. The episode to pin the airing to. It must belong to the
    ///   schedule's series, and to its season when one is set.
    /// </summary>
    public IEpisode? Episode { get; init; }

    /// <summary>
    ///   Optional. The airing's place on the schedule's numbered line, counted
    ///   from <c>1</c>, which is episode <c>FirstEpisodeNumber</c>. It may not
    ///   run past the schedule's <c>LastEpisodeNumber</c>.
    /// </summary>
    public int? SequenceNumber { get; init; }

    /// <summary>
    ///   When the episode airs, in UTC. <c>null</c> means the airing has no
    ///   slot, which is how a source indicates an indefinite postponement.
    /// </summary>
    public required DateTime? AiredAt { get; init; }

    /// <summary>
    ///   Optional. A key that is stable for this schedule. <c>null</c> derives
    ///   one from the sequence number, or from the episode when the airing is
    ///   off the line.
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

    /// <summary>
    ///   Optional. What kind of showing the airing is. An advance screening or
    ///   a rerun of an episode that also has its regular showing on the same
    ///   schedule needs a <see cref="Key"/> of its own.
    ///   <see cref="EpisodeAiringKind.DetectedRerun"/> is the core's own, and
    ///   a write carrying it is refused.
    /// </summary>
    public EpisodeAiringKind Kind { get; init; } = EpisodeAiringKind.Normal;
}
