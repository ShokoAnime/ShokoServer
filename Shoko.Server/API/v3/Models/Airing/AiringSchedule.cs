using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;

#nullable enable
namespace Shoko.Server.API.v3.Models.Airing;

/// <summary>
/// One provider's run of a series, optionally narrowed to a season, on one
/// channel or none, with a fixed set of tracks.
/// </summary>
/// <param name="schedule">The schedule.</param>
/// <exception cref="ArgumentNullException"><paramref name="schedule"/> is <c>null</c>.</exception>
public class AiringSchedule(IAiringSchedule schedule)
{
    /// <summary>
    /// The ID of the schedule.
    /// </summary>
    [Required]
    public Guid ID { get; init; } = schedule.ID;

    /// <summary>
    /// The provider's own key for the run, or the one derived from the channel
    /// and the track set when the provider gave none.
    /// </summary>
    [Required]
    public string Key { get; init; } = schedule.Key;

    /// <summary>
    /// The provider that owns the schedule.
    /// </summary>
    [Required]
    public AiringSource Source { get; init; } = new(schedule.ProviderID, schedule.ProviderName);

    /// <summary>
    /// Whether the provider that owns the schedule is still installed. The
    /// schedule is read-only, and hidden from the default reads, while it is
    /// not.
    /// </summary>
    [Required]
    public bool IsProviderInstalled { get; init; } = schedule.Provider is not null;

    /// <summary>
    /// The source of the series the schedule is for.
    /// </summary>
    [Required]
    public DataSource SeriesSource { get; init; } = schedule.SeriesSource;

    /// <summary>
    /// The ID of the series the schedule is for, relative to
    /// <see cref="SeriesSource"/>.
    /// </summary>
    [Required]
    public string SeriesID { get; init; } = schedule.SeriesID;

    /// <summary>
    /// The ID of the season the schedule is narrowed to, relative to
    /// <see cref="SeriesSource"/>, or <c>null</c> when it covers the whole run.
    /// </summary>
    public string? SeasonID { get; init; } = schedule.SeasonID;

    /// <summary>
    /// The channel the run airs on, or <c>null</c> when the provider names
    /// none.
    /// </summary>
    public AiringChannelReference? Channel { get; init; } = schedule.Channel is { } channel ? new(channel) : null;

    /// <summary>
    /// The series' page on the channel, or <c>null</c> when the provider gave
    /// none.
    /// </summary>
    public string? Url { get; init; } = schedule.Url;

    /// <summary>
    /// What the schedule releases. Only tracks of an enabled kind are listed.
    /// </summary>
    [Required]
    public IReadOnlyList<AiringTrack> Tracks { get; init; } = [.. schedule.Tracks.Select(track => new AiringTrack(track))];

    /// <summary>
    /// The first episode the schedule covers, in the series' (or season's)
    /// numbering, or <c>null</c> when the range is open-ended.
    /// </summary>
    public int? FirstEpisodeNumber { get; init; } = schedule.FirstEpisodeNumber;

    /// <summary>
    /// The last episode the schedule covers, in the series' (or season's)
    /// numbering, or <c>null</c> when the range is open-ended. Estimates never
    /// run past it.
    /// </summary>
    public int? LastEpisodeNumber { get; init; } = schedule.LastEpisodeNumber;

    /// <summary>
    /// Whether the provider considers the run finished, so no more airings will
    /// be added and nothing is estimated.
    /// </summary>
    [Required]
    public bool IsFinished { get; init; } = schedule.IsFinished;

    /// <summary>
    /// The time zone the source's own times are in, or <c>null</c> when the
    /// provider gave none. The times themselves are always in UTC.
    /// </summary>
    public AiringTimeZone? TimeZone { get; init; } = schedule.TimeZoneID is { } timeZoneID ? new(timeZoneID, schedule.TimeZone) : null;

    /// <summary>
    /// When the schedule was created.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; init; } = schedule.CreatedAt.ToUniversalTime();

    /// <summary>
    /// When the schedule was last updated.
    /// </summary>
    [Required]
    public DateTime LastUpdatedAt { get; init; } = schedule.LastUpdatedAt.ToUniversalTime();
}
