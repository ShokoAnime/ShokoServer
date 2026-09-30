using System;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   Where an episode sits in one ordering of its series: the group it is in
///   and its number there. An episode placed in more than one group of an
///   ordering has one of these for each place.
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
    ///   The episode's number: its own for the default ordering, and its place
    ///   in the group, from 1, for any other.
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
