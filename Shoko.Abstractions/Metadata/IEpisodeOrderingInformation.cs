using System;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   Where an episode sits in one ordering of its series: the group it is in
///   and its number there. An episode in more than one regular group has one
///   of these for each place. A placed special, in the special group and a
///   regular one, has only its special group place, which says where it airs.
/// </summary>
public interface IEpisodeOrderingInformation : IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    ///   The series the episode belongs to.
    /// </summary>
    MetadataGuid SeriesID { get; }

    /// <summary>
    ///   The ordering this place is in.
    /// </summary>
    MetadataGuid OrderingID { get; }

    /// <summary>
    ///   The group the episode is in: its own season for the default
    ///   ordering, or <c>null</c> when the source puts it in none.
    /// </summary>
    MetadataGuid? SeasonID { get; }

    /// <summary>
    ///   The episode.
    /// </summary>
    MetadataGuid EpisodeID { get; }

    /// <summary>
    ///   The group's number: the season number for the default ordering. In
    ///   an ordering the core stores, <c>0</c> for its special group and the
    ///   other groups' place among themselves, from 1; a core source's own
    ///   orderings keep the source's numbers. <c>null</c> when in no season.
    /// </summary>
    int? SeasonNumber { get; }

    /// <summary>
    ///   The episode's number: its own for the default ordering, and for any
    ///   other its place among the group's home episodes, from 1, placed
    ///   specials skipped.
    /// </summary>
    int EpisodeNumber { get; }

    /// <summary>
    ///   What the episode is in this ordering: its own type for the default
    ///   ordering, and for any other <see cref="EpisodeType.Special"/> in the
    ///   special group (season <c>0</c>) or else <see cref="EpisodeType.Episode"/>.
    /// </summary>
    EpisodeType EpisodeType { get; }

    /// <summary>
    ///   Whether the place is in the series' default ordering.
    /// </summary>
    bool IsDefault { get; }

    /// <summary>
    ///   Whether the place is in the ordering chosen for the series.
    /// </summary>
    bool IsPreferred { get; }

    /// <summary>
    ///   The season of the regular episode a placed special airs before.
    ///   Set only on a placed special's place, its special group place, and
    ///   only when a regular episode of the same group follows it.
    /// </summary>
    int? AirsBeforeSeasonNumber { get; }

    /// <summary>
    ///   The number of the regular episode a placed special airs before,
    ///   within <see cref="AirsBeforeSeasonNumber"/>. Set only on a placed
    ///   special's place, its special group place.
    /// </summary>
    int? AirsBeforeEpisodeNumber { get; }

    /// <summary>
    ///   The season a placed special airs after, when no regular episode of
    ///   that season follows it. Set only on a placed special's place, its
    ///   special group place.
    /// </summary>
    int? AirsAfterSeasonNumber { get; }

    /// <summary>
    ///   The regular episode a placed special airs right after, in any group,
    ///   or <c>null</c> when it airs first. Set only on a placed special's
    ///   place, its special group place. Computed, never stored.
    /// </summary>
    MetadataGuid? AirsAfterEpisodeID { get; }

    /// <summary>
    ///   The regular episode a placed special airs right before, in any
    ///   group, or <c>null</c> when it airs last. Set only on a placed
    ///   special's place, its special group place. Computed, never stored.
    /// </summary>
    MetadataGuid? AirsBeforeEpisodeID { get; }

    /// <summary>
    ///   The series.
    /// </summary>
    /// <exception cref="NullReferenceException">The series is missing.</exception>
    ISeries Series { get; }

    /// <summary>
    ///   The group the episode is in, if it is available.
    /// </summary>
    ISeason? Season { get; }

    /// <summary>
    ///   The episode.
    /// </summary>
    IEpisode Episode { get; }
}

/// <summary>
///   Where an episode sits in one ordering of its series, with the series,
///   group and episode typed.
/// </summary>
/// <typeparam name="TSeries">The series' type.</typeparam>
/// <typeparam name="TEpisode">The episodes' type.</typeparam>
public interface IEpisodeOrderingInformation<out TSeries, out TEpisode> : IEpisodeOrderingInformation
    where TSeries : class, ISeries
    where TEpisode : class, IEpisode
{
    /// <summary>
    ///   The series.
    /// </summary>
    /// <exception cref="NullReferenceException">The series is missing.</exception>
    new TSeries Series { get; }

    ISeries IEpisodeOrderingInformation.Series { get => Series; }

    /// <summary>
    ///   The group the episode is in, if it is available.
    /// </summary>
    new ISeason<TSeries, TEpisode>? Season { get; }

    ISeason? IEpisodeOrderingInformation.Season { get => Season; }

    /// <summary>
    ///   The episode.
    /// </summary>
    new TEpisode Episode { get; }

    IEpisode IEpisodeOrderingInformation.Episode { get => Episode; }
}
