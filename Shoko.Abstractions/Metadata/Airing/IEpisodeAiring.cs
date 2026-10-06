using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   One episode on one airing schedule: a time, an original time, a delay
///   flag, and an optional link to the other airings of the same slot.
/// </summary>
/// <remarks>
///   This is a view built for the episode it was resolved for, not a row. When
///   one provider episode is linked to two AniDB episodes, the same stored
///   airing comes back twice, with a different
///   <see cref="AnidbEpisode"/> each time. Estimates are views too, and are
///   never stored. So is a date-only entry, which a read only returns when it
///   asks for one, and which has no schedule, provider, channel or time.
/// </remarks>
public interface IEpisodeAiring
{
    /// <summary>
    ///   The ID of the airing, derived from the schedule ID and the airing key.
    ///   Estimates get one too, and a date-only entry one derived from its
    ///   episode.
    /// </summary>
    Guid ID { get; }

    /// <summary>
    ///   The provider's own key, or the one derived from the episode when the
    ///   provider gave none.
    /// </summary>
    string Key { get; }

    /// <summary>
    ///   The schedule the airing belongs to, or <c>null</c> for a date-only
    ///   entry. Estimates belong to a schedule too.
    /// </summary>
    IAiringSchedule? Schedule { get; }

    /// <summary>
    ///   The ID of the provider that owns the airing, or <c>null</c> for a
    ///   date-only entry.
    /// </summary>
    Guid? ProviderID { get; }

    /// <summary>
    ///   The name of the provider that owns the airing, kept after the plugin
    ///   is uninstalled, or <c>null</c> for a date-only entry.
    /// </summary>
    string? ProviderName { get; }

    /// <summary>
    ///   Whether this is a date-only entry: an AniDB episode known only by its
    ///   air date, with no airing behind it. Its date is
    ///   <see cref="AirDate"/>, and it has no time.
    /// </summary>
    bool IsDateOnly { get; }

    /// <summary>
    ///   The AniDB air date of a date-only entry, or the linked one standing
    ///   in for an undated episode before 1970, a calendar date in no
    ///   particular time zone, or <c>null</c> for an airing with a time.
    /// </summary>
    DateOnly? AirDate { get; }

    /// <summary>
    ///   The episode the airing is for.
    /// </summary>
    MetadataGuid EpisodeID { get; }

    /// <summary>
    ///   The episode the airing is attached to, or <c>null</c> when it could
    ///   not be resolved.
    /// </summary>
    IEpisode? Episode { get; }

    /// <summary>
    ///   The AniDB episode this view was resolved for, or <c>null</c> when
    ///   there is none.
    /// </summary>
    IAnidbEpisode? AnidbEpisode { get; }

    /// <summary>
    ///   The shoko episode this view was resolved for, or <c>null</c> when
    ///   there is none.
    /// </summary>
    IShokoEpisode? ShokoEpisode { get; }

    /// <summary>
    ///   The schedule's channel, exposed here for convenience, or <c>null</c>
    ///   when the schedule names none.
    /// </summary>
    IAiringChannel? Channel { get; }

    /// <summary>
    ///   The airing's own page, else the schedule's, or <c>null</c> when
    ///   neither is known.
    /// </summary>
    string? Url { get; }

    /// <summary>
    ///   The schedule's tracks, exposed here for convenience. An airing never
    ///   carries tracks of its own.
    /// </summary>
    IReadOnlyList<IAiringTrack> Tracks { get; }

    /// <summary>
    ///   When the episode airs, in UTC, or <c>null</c> when the airing has no
    ///   current slot.
    /// </summary>
    DateTime? AiredAt { get; }

    /// <summary>
    ///   The slot the episode was first scheduled for, in UTC. Set whenever the
    ///   slot moved, and <c>null</c> while it has not.
    /// </summary>
    DateTime? OriginalAiredAt { get; }

    /// <summary>
    ///   How long the episode itself runs: its AniDB episode's length, else
    ///   the median length of its anime's regular episodes, or <c>null</c>
    ///   when neither is known.
    /// </summary>
    TimeSpan? Duration { get; }

    /// <summary>
    ///   When the airing's slot ends, in UTC, or <c>null</c> when it has no
    ///   current slot: <see cref="AiredAt"/> plus the <see cref="Duration"/>,
    ///   or 24 minutes when that is unknown. On a television channel, whose
    ///   slots carry ads, it is rounded up to the next 15 minutes, or ends at
    ///   the channel's next airing if that starts sooner, once the episode is over.
    /// </summary>
    DateTime? EndsAt { get; }

    /// <summary>
    ///   Whether this airing's own slot was postponed on this channel. Only the
    ///   airing that caused a delay is flagged; the ones that merely shifted
    ///   behind it are not.
    /// </summary>
    bool IsDelayed { get; }

    /// <summary>
    ///   Whether the airing was computed from the schedule's other airings
    ///   rather than reported by the provider. Estimates are never stored.
    /// </summary>
    bool IsEstimated { get; }

    /// <summary>
    ///   Whether this is the airing of its episode that a
    ///   <see cref="EpisodeAiringFilteringOptions.PreferredOnly"/> read with
    ///   the same filters and window would keep. A date-only entry always is.
    ///   Only the list reads set it: a lookup by ID and a link set leave it
    ///   <c>false</c>.
    /// </summary>
    bool IsPreferred { get; }

    /// <summary>
    ///   What kind of showing the airing is: the provider's own kind, or
    ///   <see cref="EpisodeAiringKind.DetectedRerun"/> for an airing the
    ///   provider left <see cref="EpisodeAiringKind.Normal"/> on a schedule the
    ///   core detected as a rerun. Estimates take their schedule's kind.
    /// </summary>
    EpisodeAiringKind Kind { get; }

    /// <summary>
    ///   This airing's time minus the episode's earliest known real
    ///   <see cref="AiringKind.Original"/> airing, which a client can label as
    ///   a simulcast or a lag. <c>null</c> when this is that airing, or when
    ///   there is none. A negative offset is valid.
    /// </summary>
    TimeSpan? OffsetFromOriginal { get; }

    /// <summary>
    ///   The ID of the link head, shared by every airing of the same slot, or
    ///   <c>null</c> when the airing is not linked. It changes if the head is
    ///   removed.
    /// </summary>
    Guid? LinkID { get; }

    /// <summary>
    ///   When the airing was created.
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    ///   When the airing was last updated.
    /// </summary>
    DateTime LastUpdatedAt { get; }
}
