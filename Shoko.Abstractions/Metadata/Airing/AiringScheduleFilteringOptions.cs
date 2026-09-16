using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Options for filtering and fetching airing schedules from
///   <c>IAiringScheduleService</c>.
/// </summary>
public sealed class AiringScheduleFilteringOptions
{
    /// <summary>
    ///   Optional. If set, will restrict the returned list to only containing
    ///   schedules owned by the given provider.
    /// </summary>
    public Guid? ProviderID { get; set; }

    /// <summary>
    ///   Optional. If set, will restrict the returned list to only containing
    ///   schedules with a track of the given kind.
    /// </summary>
    public AiringKind? Kind { get; set; }

    /// <summary>
    ///   Optional. If set, will restrict the returned list to only containing
    ///   schedules with a track in the given language.
    /// </summary>
    public TitleLanguage? Language { get; set; }

    /// <summary>
    ///   Optional. If set, will restrict the returned list to only containing
    ///   schedules on one of the given channels.
    /// </summary>
    public IReadOnlySet<Guid>? ChannelIDs { get; set; }

    /// <summary>
    ///   Optional. Whether a series read also returns the schedules narrowed to
    ///   one of its seasons. Defaults to <c>true</c>.
    /// </summary>
    public bool IncludeSeasonSchedules { get; set; } = true;

    /// <summary>
    ///   Optional. Whether to also return schedules hidden because their
    ///   provider is disabled or none of their tracks are of an enabled kind.
    ///   Defaults to <c>false</c>.
    /// </summary>
    public bool IncludeDisabled { get; set; }

    /// <summary>
    ///   Optional. Set to <c>false</c> to only retrieve the entity's own
    ///   schedules. Set to <c>true</c> to also retrieve the schedules of other
    ///   entities linked to the entity. Set to <c>null</c> to let the service
    ///   decide based on the entity, which means linked for
    ///   <see cref="IShokoSeries"/>, <see cref="IShokoSeason"/> and
    ///   <see cref="IShokoEpisode"/>, and own-only for everything else.
    ///   Defaults to <c>null</c>.
    /// </summary>
    public bool? LinkedEntitySchedules { get; set; }
}
