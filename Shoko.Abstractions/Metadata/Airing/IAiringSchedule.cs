using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   One provider's run of a series, optionally narrowed to a season, on one
///   channel or none, with a fixed set of tracks. A schedule is owned by the
///   provider that created it.
/// </summary>
/// <remarks>
///   Everything but the provider's own submission is resolved when the schedule
///   is read, so a new alias, a relink or a priority change never leaves stale
///   enrichment behind. A lookup that fails is not fatal: the unresolved parts
///   are <c>null</c>, while the keys always remain.
/// </remarks>
public interface IAiringSchedule
{
    /// <summary>
    ///   The ID of the schedule, derived from the provider ID, the series, the
    ///   season and the key.
    /// </summary>
    Guid ID { get; }

    /// <summary>
    ///   The provider's own key, or the one derived from the channel and the
    ///   track set when the provider gave none.
    /// </summary>
    string Key { get; }

    /// <summary>
    ///   The ID of the provider that owns the schedule.
    /// </summary>
    Guid ProviderID { get; }

    /// <summary>
    ///   The name of the provider that owns the schedule, kept after the plugin
    ///   is uninstalled.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    ///   Information about the provider that owns the schedule, or <c>null</c>
    ///   when the provider is gone.
    /// </summary>
    AiringScheduleProviderInfo? Provider { get; }

    /// <summary>
    ///   The source of the series the schedule is for.
    /// </summary>
    DataSource SeriesSource { get; }

    /// <summary>
    ///   The ID of the series the schedule is for, relative to
    ///   <see cref="SeriesSource"/>.
    /// </summary>
    string SeriesID { get; }

    /// <summary>
    ///   The series the schedule is for, or <c>null</c> when it could not be
    ///   resolved.
    /// </summary>
    ISeries? Series { get; }

    /// <summary>
    ///   The ID of the season the schedule is narrowed to, relative to
    ///   <see cref="SeriesSource"/>, or <c>null</c> when it covers the whole
    ///   run.
    /// </summary>
    string? SeasonID { get; }

    /// <summary>
    ///   The season the schedule is narrowed to, or <c>null</c> when it covers
    ///   the whole run or the season could not be resolved.
    /// </summary>
    ISeason? Season { get; }

    /// <summary>
    ///   The channel the run airs on, or <c>null</c> when the provider names
    ///   none.
    /// </summary>
    IAiringChannel? Channel { get; }

    /// <summary>
    ///   The series' page on the channel, or <c>null</c> when the provider
    ///   gave none.
    /// </summary>
    string? Url { get; }

    /// <summary>
    ///   What the schedule releases. Only tracks of an enabled kind are
    ///   listed.
    /// </summary>
    IReadOnlyList<IAiringTrack> Tracks { get; }

    /// <summary>
    ///   The first episode the schedule covers, in the series' (or season's)
    ///   numbering, or <c>null</c> when the range is open-ended.
    /// </summary>
    int? FirstEpisodeNumber { get; }

    /// <summary>
    ///   The last episode the schedule covers, in the series' (or season's)
    ///   numbering, or <c>null</c> when the range is open-ended. Estimates
    ///   never run past it.
    /// </summary>
    int? LastEpisodeNumber { get; }

    /// <summary>
    ///   Whether the provider considers the run finished, so no more airings
    ///   will be added.
    /// </summary>
    bool IsFinished { get; }

    /// <summary>
    ///   The time zone the source's own times are in, exactly as stored: an
    ///   IANA id, or a fixed <c>"±HH:MM"</c> offset. <c>null</c> when the
    ///   provider gave none.
    /// </summary>
    string? TimeZoneID { get; }

    /// <summary>
    ///   The resolved <see cref="TimeZoneID"/>, or <c>null</c> when the
    ///   provider gave none or this host cannot resolve the id.
    /// </summary>
    TimeZoneInfo? TimeZone { get; }

    /// <summary>
    ///   When the schedule was created.
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    ///   When the schedule was last updated.
    /// </summary>
    DateTime LastUpdatedAt { get; }
}
