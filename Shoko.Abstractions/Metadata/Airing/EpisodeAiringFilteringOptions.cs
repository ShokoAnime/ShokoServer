using System;
using System.Collections.Generic;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Options for filtering, ordering and fetching episode airings from
///   <c>IAiringScheduleService</c>.
/// </summary>
public sealed class EpisodeAiringFilteringOptions
{
    #region Filters

    /// <summary>
    ///   Optional. If set, will restrict the returned list to only containing
    ///   airings owned by one of the given providers.
    /// </summary>
    public IReadOnlySet<Guid>? ProviderIDs { get; set; }

    /// <summary>
    ///   Optional. If set, will restrict the returned list to only containing
    ///   airings whose schedule has a track of one of the given kinds.
    /// </summary>
    public IReadOnlySet<AiringKind>? Kinds { get; set; }

    /// <summary>
    ///   Optional. If set, will restrict the returned list to only containing
    ///   airings whose schedule has a track in one of the given languages.
    /// </summary>
    public IReadOnlySet<TitleLanguage>? Languages { get; set; }

    /// <summary>
    ///   Optional. If set, will restrict the returned list to only containing
    ///   airings on one of the given channels, hidden or not. If unset, the
    ///   airings on the channels the server hides are left out.
    /// </summary>
    /// <remarks>
    ///   The hidden channels are
    ///   <c>IAiringScheduleService.HiddenChannelIDs</c>. A schedule read
    ///   returns every airing of its schedule either way.
    /// </remarks>
    public IReadOnlySet<Guid>? ChannelIDs { get; set; }

    /// <summary>
    ///   Optional. If set, will restrict the returned list to only containing
    ///   airings of episodes of one of the given types. An unresolved airing
    ///   counts as <see cref="EpisodeType.Episode"/>.
    /// </summary>
    public IReadOnlySet<EpisodeType>? EpisodeTypes { get; set; }

    /// <summary>
    ///   Optional. If set, will restrict the returned list to only containing
    ///   airings whose <see cref="IEpisodeAiring.Kind"/> is one of the given
    ///   kinds of showing. A read that wants no reruns leaves out both
    ///   <see cref="EpisodeAiringKind.Rerun"/> and
    ///   <see cref="EpisodeAiringKind.DetectedRerun"/>.
    /// </summary>
    /// <remarks>
    ///   A date-only entry counts as <see cref="EpisodeAiringKind.Normal"/>.
    /// </remarks>
    public IReadOnlySet<EpisodeAiringKind>? EpisodeKinds { get; set; }

    /// <summary>
    ///   Optional. Filters on whether the airing's series is in the
    ///   collection, which means it has a shoko series. Defaults to
    ///   <see cref="InclusionFilter.True"/>, keeping everything.
    /// </summary>
    /// <remarks>
    ///   <see cref="InclusionFilter.Only"/> keeps the airings of series in the
    ///   collection, and <see cref="InclusionFilter.False"/> the airings of
    ///   series not in it. An airing whose series resolves to nothing counts
    ///   as not in the collection.
    /// </remarks>
    public InclusionFilter InCollection { get; set; } = InclusionFilter.True;

    /// <summary>
    ///   Optional. If set, will restrict the returned list to only containing
    ///   airings of the Shoko series the filter passes, evaluated for
    ///   <see cref="User"/>. Airings of series not in the collection are left
    ///   out, and series without local files only when the filter says so.
    /// </summary>
    /// <remarks>
    ///   The filter is evaluated once per read. A filter that depends on the
    ///   user needs <see cref="User"/> set, or the read throws an
    ///   <see cref="ArgumentNullException"/>.
    /// </remarks>
    public IFilter? Filter { get; set; }

    /// <summary>
    ///   Optional. Filters on whether the airing's series is restricted (H).
    ///   Defaults to <see cref="InclusionFilter.True"/>, keeping everything.
    /// </summary>
    public InclusionFilter IncludeRestricted { get; set; } = InclusionFilter.True;

    /// <summary>
    ///   Optional. The user the read is for. If set, the airings of series the
    ///   user is not allowed to see are left out.
    /// </summary>
    public IUser? User { get; set; }

    /// <summary>
    ///   Optional. Whether to also return the estimated airings computed from
    ///   the surviving schedules. Defaults to <c>true</c>.
    /// </summary>
    public bool IncludeEstimates { get; set; } = true;

    /// <summary>
    ///   Optional. Whether to also return the unresolved airings, the ones
    ///   whose place on the line no source lists an episode for yet. Defaults
    ///   to <c>true</c>.
    /// </summary>
    /// <remarks>
    ///   An entity read returns the unresolved airings of the schedules on its
    ///   series, or on its season, and an episode read never does.
    /// </remarks>
    public bool IncludeUnresolved { get; set; } = true;

    /// <summary>
    ///   Optional. Whether to also return a date-only entry for each AniDB
    ///   episode with an air date but no airing at all. Defaults to
    ///   <c>false</c>.
    /// </summary>
    /// <remarks>
    ///   A date-only entry has <see cref="IEpisodeAiring.IsDateOnly"/> set and
    ///   no schedule, provider, channel or time. It counts as an
    ///   <see cref="AiringKind.Original"/> showing in no particular language,
    ///   so a <see cref="ProviderIDs"/>, <see cref="ChannelIDs"/> or
    ///   <see cref="Languages"/> filter leaves it out. Schedule reads never
    ///   return one. An undated regular episode of an anime starting by
    ///   1970-01-01, which its anime's start date does not stand in for,
    ///   takes the earliest pre-1970 air date of its linked episodes.
    /// </remarks>
    public bool IncludeDateOnly { get; set; }

    /// <summary>
    ///   Optional. Whether to also return airings hidden because their provider
    ///   is disabled or none of their schedule's tracks are of an enabled kind.
    ///   Defaults to <c>false</c>.
    /// </summary>
    public bool IncludeDisabled { get; set; }

    /// <summary>
    ///   Optional. Whether a range read also matches a delayed airing by its
    ///   original slot, so a week an episode was delayed out of still has
    ///   something to draw its gap from. Defaults to <c>true</c>.
    /// </summary>
    public bool IncludeDelayedOriginalSlots { get; set; } = true;

    /// <summary>
    ///   Optional. Set to <c>false</c> to only retrieve the entity's own
    ///   airings. Set to <c>true</c> to also retrieve the airings of other
    ///   entities linked to the entity. Set to <c>null</c> to let the service
    ///   decide based on the entity, which means linked for
    ///   <see cref="IShokoSeries"/>, a season of one and
    ///   <see cref="IShokoEpisode"/>, and own-only for everything else.
    ///   Defaults to <c>null</c>.
    /// </summary>
    public bool? LinkedEntityAirings { get; set; }

    /// <summary>
    ///   Optional. Which entities the airings are anchored to. Defaults to
    ///   <see cref="AiringEntityAnchor.Auto"/>, which takes the anchor from the
    ///   entity the read was given and falls back to
    ///   <see cref="AiringEntityAnchor.Raw"/> for a read that takes none.
    /// </summary>
    /// <remarks>
    ///   <see cref="AiringEntityAnchor.Shoko"/> drops every airing that does
    ///   not resolve to an <see cref="IShokoEpisode"/>, and follows the entity
    ///   links needed to reach one whether or not
    ///   <see cref="LinkedEntityAirings"/> asked for them, since anchoring to
    ///   shoko without walking the links would only ever answer nothing.
    /// </remarks>
    public AiringEntityAnchor EntityAnchor { get; set; }

    #endregion

    #region Preference

    /// <summary>
    ///   Optional. The channels the caller would rather watch on, in order.
    ///   <c>null</c> falls back to the server's preference, and an empty list
    ///   means no channel preference at all.
    /// </summary>
    public IReadOnlyList<Guid>? PreferredChannels { get; set; }

    /// <summary>
    ///   Optional. The tracks the caller would rather watch, in order.
    ///   <c>null</c> falls back to the server's preference, and an empty list
    ///   means no track preference at all.
    /// </summary>
    public IReadOnlyList<AiringTrackPreference>? PreferredTracks { get; set; }

    /// <summary>
    ///   Optional. Whether a list read is reduced to one airing per episode,
    ///   the one <c>GetAiringForEpisode</c> would return. Defaults to
    ///   <c>false</c>.
    /// </summary>
    /// <remarks>
    ///   A range read reduces after it has narrowed the airings to its window,
    ///   so an episode is answered with the best airing it has <em>in the
    ///   window</em> rather than dropping out of it over a better airing
    ///   somewhere else.
    /// </remarks>
    public bool PreferredOnly { get; set; }

    #endregion

    #region Next

    /// <summary>
    ///   Optional. Whether a list read is reduced to the next airing of each
    ///   group <see cref="NextPer"/> describes. Defaults to <c>false</c>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     The next airing is the first one on air at, or starting at or
    ///     after, the start of a range read, or <see cref="At"/> for any
    ///     other read, of an episode still to premiere. An airing is on air until its
    ///     <see cref="IEpisodeAiring.EndsAt"/>, so an episode stays next while
    ///     it airs. An episode with a real
    ///     <see cref="EpisodeAiringKind.Normal"/> airing that ended before that
    ///     point, on any schedule of any provider and channel and through the
    ///     links the read walks, is never next, so a delayed regional
    ///     broadcast of it is not either. In each group the earliest episode
    ///     wins, and the group answers with that episode's best airing by
    ///     preference, so the preferred channel and track still decide where
    ///     it is shown.
    ///   </para>
    ///   <para>
    ///     A delayed airing's original slot is no airing, so it is never
    ///     next. A date-only entry is next from the start of its day, in the
    ///     range's offset or in UTC, and not once its AniDB date has passed.
    ///     Subscriptions ignore this.
    ///   </para>
    /// </remarks>
    public bool NextOnly { get; set; }

    /// <summary>
    ///   Optional. What <see cref="NextOnly"/> keeps one airing per. The parts
    ///   combine. <c>null</c> means per <see cref="AiringNextGrouping.Series"/>,
    ///   and an empty set keeps the single next airing of the whole read.
    /// </summary>
    public IReadOnlySet<AiringNextGrouping>? NextPer { get; set; }

    #endregion

    #region Reference Time

    /// <summary>
    ///   Optional. The time the read is as of, in UTC. <c>null</c> means now,
    ///   by the service's clock.
    /// </summary>
    /// <remarks>
    ///   Whatever a read counts from now counts from this instead: the next
    ///   airing of a next-only entity read, whether an episode has premiered,
    ///   and whether a later showing has overtaken a slotless airing. A range
    ///   read's next airing still counts from the start of its range. It only
    ///   changes what the read answers, never what is stored. A time without
    ///   a kind is read as UTC. Subscriptions ignore this.
    /// </remarks>
    public DateTime? At { get; set; }

    #endregion
}
