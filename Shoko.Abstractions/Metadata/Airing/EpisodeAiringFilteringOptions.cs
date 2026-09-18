using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;

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
    ///   airings owned by the given provider.
    /// </summary>
    public Guid? ProviderID { get; set; }

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
    ///   airings on one of the given channels.
    /// </summary>
    public IReadOnlySet<Guid>? ChannelIDs { get; set; }

    /// <summary>
    ///   Optional. Whether to also return the estimated airings computed from
    ///   the surviving schedules. Defaults to <c>true</c>.
    /// </summary>
    public bool IncludeEstimates { get; set; } = true;

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
    ///   <see cref="IShokoSeries"/>, <see cref="IShokoSeason"/> and
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
    ///   <see cref="LinkedEntityAirings"/> asked for them — anchoring to shoko
    ///   without walking the links would only ever answer nothing.
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
}
