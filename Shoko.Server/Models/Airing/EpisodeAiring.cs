using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Models.Airing;

/// <summary>
/// One episode on one airing schedule: a time, the slot it was first scheduled
/// for, a delay flag, and an optional link to the other airings of the same
/// slot.
/// </summary>
/// <remarks>
/// Only what the provider submitted, plus the delay state a write inferred, is
/// stored. Estimates are never rows.
/// </remarks>
public class EpisodeAiring
{
    #region Database Columns

    /// <summary>
    /// Local database ID. It doubles as the link head reference.
    /// </summary>
    public int EpisodeAiringID { get; set; }

    /// <summary>
    /// The local database ID of the schedule the airing belongs to.
    /// </summary>
    public int AiringScheduleID { get; set; }

    /// <summary>
    /// The provider's own key, or the one derived from the episode when the
    /// provider gave none.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The source of the episode the airing is for.
    /// </summary>
    public DataSource EpisodeSource { get; set; }

    /// <summary>
    /// The ID of the episode the airing is for, relative to
    /// <see cref="EpisodeSource"/>.
    /// </summary>
    public string EpisodeID { get; set; } = string.Empty;

    /// <summary>
    /// The airing's own page, or <c>null</c> when the provider gave none and
    /// the schedule's is to be used instead.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// When the episode airs, in UTC, or <c>null</c> when the airing has no
    /// current slot.
    /// </summary>
    public DateTime? AiredAt { get; set; }

    /// <summary>
    /// The slot the episode was first scheduled for, in UTC. Set whenever the
    /// slot moved, and <c>null</c> while it has not.
    /// </summary>
    public DateTime? OriginalAiredAt { get; set; }

    /// <summary>
    /// Whether this airing's own slot was postponed on this channel. Only the
    /// airing that caused a delay is flagged; the ones that merely shifted
    /// behind it are not.
    /// </summary>
    public bool IsDelayed { get; set; }

    /// <summary>
    /// The <see cref="EpisodeAiringID"/> of the link head, which is the member
    /// with the smallest local ID and points at itself. <c>null</c> when the
    /// airing is not linked.
    /// </summary>
    public int? LinkedToID { get; set; }

    /// <summary>
    /// When the airing was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the airing was last updated.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region Computed Properties

    /// <summary>
    /// The public ID of the airing, derived from its schedule's public ID and
    /// its key, so it is never stored.
    /// </summary>
    /// <exception cref="NullReferenceException">The schedule the airing belongs to no longer exists.</exception>
    /// <exception cref="ArgumentException"><see cref="Key"/> is blank.</exception>
    public Guid ID
        => AiringScheduleUtility.GetEpisodeAiringID(
            Schedule?.ID ?? throw new NullReferenceException($"Unable to find airing schedule with id {AiringScheduleID} in EpisodeAiring.ID"),
            Key
        );

    /// <summary>
    /// The UTC day the airing falls on, from its current slot or, when it has
    /// none, the slot it was first scheduled for. <c>null</c> when it has
    /// neither. Range reads bucket on this.
    /// </summary>
    public DateOnly? DayBucket
        => (AiredAt ?? OriginalAiredAt) is { } airedAt ? DateOnly.FromDateTime(airedAt) : null;

    /// <summary>
    /// The UTC day the airing was first scheduled for, but only while it is
    /// flagged as delayed, so a week an episode was delayed out of still has
    /// something to draw its gap from. <c>null</c> otherwise.
    /// </summary>
    public DateOnly? DelayedDayBucket
        => IsDelayed && OriginalAiredAt is { } originalAiredAt ? DateOnly.FromDateTime(originalAiredAt) : null;

    #endregion

    #region Navigation Properties

    /// <summary>
    /// The schedule the airing belongs to, or <c>null</c> when it is gone.
    /// </summary>
    public AiringSchedule? Schedule
        => RepoFactory.AiringSchedule.GetByID(AiringScheduleID);

    /// <summary>
    /// The link head, which is this airing itself when it is the head, or
    /// <c>null</c> when the airing is not linked.
    /// </summary>
    public EpisodeAiring? LinkHead
        => LinkedToID is { } linkedToID ? RepoFactory.EpisodeAiring.GetByID(linkedToID) : null;

    /// <summary>
    /// Every airing of the same slot, this one included, or an empty list when
    /// the airing is not linked.
    /// </summary>
    public IReadOnlyList<EpisodeAiring> LinkedAirings
        => LinkedToID is { } linkedToID ? RepoFactory.EpisodeAiring.GetByLinkedToID(linkedToID) : [];

    #endregion
}
