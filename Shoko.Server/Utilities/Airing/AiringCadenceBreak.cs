using System;
using System.Collections.Generic;

namespace Shoko.Server.Utilities.Airing;

/// <summary>
/// A release that arrived later than the line's cadence, so the break was already
/// there the first time the provider fetched the schedule.
/// </summary>
/// <param name="AiredAt">
/// When the release actually aired, in UTC, taken from its earliest member.
/// </param>
/// <param name="ExpectedAiredAt">
/// The slot the cadence would have put it in, in UTC.
/// </param>
/// <param name="Cadence">
/// The line's cadence, as the median gap between its releases.
/// </param>
/// <param name="SkippedSlots">
/// How many whole slots the break skipped.
/// </param>
/// <param name="EpisodeKeys">
/// The keys of every episode in the release, ordered as they were measured. A
/// link set, or a set of airings at the same time, is one release.
/// </param>
public sealed record AiringCadenceBreak(
    DateTime AiredAt,
    DateTime ExpectedAiredAt,
    TimeSpan Cadence,
    int SkippedSlots,
    IReadOnlyList<string> EpisodeKeys
);
