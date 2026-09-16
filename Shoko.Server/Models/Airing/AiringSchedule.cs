using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Models.Airing;

/// <summary>
/// One provider's run of a series, optionally narrowed to a season, on one
/// channel or none, with a fixed set of tracks.
/// </summary>
/// <remarks>
/// Only what the provider submitted is stored. The provider info, the series,
/// the season, the channel and the tracks' languages are all resolved when the
/// schedule is read, so nothing here can go stale.
/// </remarks>
public class AiringSchedule
{
    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int AiringScheduleID { get; set; }

    /// <summary>
    /// The ID of the provider that owns the schedule.
    /// </summary>
    public Guid ProviderID { get; set; }

    /// <summary>
    /// The name of the provider that owns the schedule, kept after the plugin
    /// is uninstalled.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// The source of the series the schedule is for.
    /// </summary>
    public DataSource SeriesSource { get; set; }

    /// <summary>
    /// The ID of the series the schedule is for, relative to
    /// <see cref="SeriesSource"/>.
    /// </summary>
    public string SeriesID { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the season the schedule is narrowed to, relative to
    /// <see cref="SeriesSource"/>. An empty string when the schedule covers the
    /// whole run, so the unique key works on every backend.
    /// </summary>
    public string SeasonID { get; set; } = string.Empty;

    /// <summary>
    /// The provider's own key, or the one derived from the channel and the
    /// track set when the provider gave none.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the channel the run airs on, or <c>null</c> when the provider
    /// names none.
    /// </summary>
    public Guid? ChannelID { get; set; }

    /// <summary>
    /// What the schedule releases, stored as a JSON array.
    /// </summary>
    public List<AiringTrackData> Tracks { get; set; } = [];

    /// <summary>
    /// The first episode the schedule covers, in the series' (or season's)
    /// numbering, or <c>null</c> when the range is open-ended.
    /// </summary>
    public int? FirstEpisodeNumber { get; set; }

    /// <summary>
    /// The last episode the schedule covers, in the series' (or season's)
    /// numbering, or <c>null</c> when the range is open-ended.
    /// </summary>
    public int? LastEpisodeNumber { get; set; }

    /// <summary>
    /// Whether the provider considers the run finished, so no more airings will
    /// be added.
    /// </summary>
    public bool IsFinished { get; set; }

    /// <summary>
    /// The time zone the source's own times are in, as an IANA id or a fixed
    /// <c>±HH:MM</c> offset, or <c>null</c> when the provider gave none. It is
    /// normalised on write and is for display only, since every stored time is
    /// already in UTC.
    /// </summary>
    public string? TimeZoneID { get; set; }

    /// <summary>
    /// The series' page on the channel, or <c>null</c> when the provider gave
    /// none.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// When the schedule was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the schedule was last updated.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region Computed Properties

    /// <summary>
    /// The public ID of the schedule, derived from the provider, the series,
    /// the season and the key, so it is never stored.
    /// </summary>
    /// <exception cref="ArgumentException"><see cref="SeriesID"/> or <see cref="Key"/> is blank.</exception>
    public Guid ID
        => AiringScheduleUtility.GetScheduleID(ProviderID, SeriesSource, SeriesID, SeasonID, Key);

    #endregion

    #region Navigation Properties

    /// <summary>
    /// The channel the run airs on, or <c>null</c> when the schedule names none
    /// or the channel is gone.
    /// </summary>
    public AiringChannel? Channel
        => ChannelID is { } channelID ? RepoFactory.AiringChannel.GetByChannelID(channelID) : null;

    /// <summary>
    /// Every airing stored for the schedule, ordered by their current slot.
    /// </summary>
    public IReadOnlyList<EpisodeAiring> Airings
        => RepoFactory.EpisodeAiring.GetByScheduleID(AiringScheduleID);

    #endregion
}
