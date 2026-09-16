using System;

namespace Shoko.Server.Utilities.Airing;

/// <summary>
/// What one airing schedule's own airings say about its slot, so the episodes it
/// has no airing for can be estimated. A profile belongs to a schedule, never to
/// an anime: one channel, one set of tracks, one line of airings.
/// </summary>
/// <param name="Anchor">
/// What <paramref name="Offset"/> is measured from.
/// </param>
/// <param name="Offset">
/// The schedule's slot relative to the anchor, or <see langword="null"/> when too
/// few samples were known to trust one. Without it nothing is estimated.
/// </param>
/// <param name="TrailingShiftDays">
/// A whole number of days the latest airings sit away from the older ones, so a
/// run that slipped a week keeps estimating the slipped slot. Always <c>0</c> for
/// the <see cref="AiringAnchor.FirstOriginalAiring"/> anchor.
/// </param>
/// <param name="HiatusFrom">
/// The slot this schedule's first slotless, delayed airing would have had, or
/// <see langword="null"/> when it has none. From there on this schedule estimates
/// no slots, and nothing else is affected.
/// </param>
/// <param name="LastEstimableEpisode">
/// The highest episode number this schedule may estimate: <see langword="null"/>
/// when the coverage is open-ended, and <c>0</c> when the schedule is finished and
/// nothing at all may be estimated.
/// </param>
/// <param name="AnidbOffset">
/// The offset measured from midnight UTC of the AniDB air date, kept even when
/// the anchor is the Original airing, so an episode without a real Original airing
/// still falls back to the AniDB anchor. Equal to <paramref name="Offset"/> for
/// the <see cref="AiringAnchor.AnidbDate"/> anchor.
/// </param>
public sealed record AiringScheduleProfile(
    AiringAnchor Anchor,
    TimeSpan? Offset,
    int TrailingShiftDays,
    DateTime? HiatusFrom,
    int? LastEstimableEpisode,
    TimeSpan? AnidbOffset = null
);
