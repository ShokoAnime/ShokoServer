using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   A whole airing schedule, as a provider submits it: one run of a series,
///   optionally narrowed to a season, on one channel or none, with a fixed set
///   of tracks.
/// </summary>
public sealed record AiringScheduleData
{
    /// <summary>
    ///   The series the schedule is for. The service stores its source and ID.
    /// </summary>
    public required ISeries Series { get; init; }

    /// <summary>
    ///   Optional. Narrows the schedule to a single season of the series.
    /// </summary>
    public ISeason? Season { get; init; }

    /// <summary>
    ///   Optional. The channel the run airs on. It must be registered through
    ///   <c>FindOrRegisterChannel</c> first, and cannot be changed afterwards:
    ///   a different channel is a different schedule.
    /// </summary>
    public Guid? ChannelID { get; init; }

    /// <summary>
    ///   What the schedule releases. At least one track is required, and every
    ///   kind must be declared by the provider. Duplicate tracks collapse.
    /// </summary>
    public required IReadOnlyList<AiringTrackData> Tracks { get; init; }

    /// <summary>
    ///   Optional. The first episode the schedule covers, in the series' (or
    ///   season's) numbering. <c>null</c> leaves the range open-ended.
    /// </summary>
    public int? FirstEpisodeNumber { get; init; }

    /// <summary>
    ///   Optional. The last episode the schedule covers, in the series' (or
    ///   season's) numbering. <c>null</c> leaves the range open-ended.
    /// </summary>
    public int? LastEpisodeNumber { get; init; }

    /// <summary>
    ///   Whether the provider considers the run finished, so no more airings
    ///   will be added. Estimates never run past a finished schedule.
    /// </summary>
    public bool IsFinished { get; init; }

    /// <summary>
    ///   Optional. The time zone the source's own times are in, for display
    ///   only. Times are always submitted, stored and returned in UTC.
    /// </summary>
    public TimeZoneInfo? TimeZone { get; init; }

    /// <summary>
    ///   Optional. A key that is stable for this provider, forming the
    ///   schedule's identity together with the provider, series and season.
    ///   <c>null</c> derives one from the channel and the full track set, so
    ///   adding a language to a keyless schedule makes a new schedule.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    ///   Optional. The series' page on the channel.
    /// </summary>
    public string? Url { get; init; }
}
