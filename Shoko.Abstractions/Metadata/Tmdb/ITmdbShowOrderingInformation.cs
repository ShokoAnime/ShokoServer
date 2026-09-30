using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
///   One of TMDB's own orderings of a show: its default ordering, made from
///   its seasons, or one of its episode groups. The generic
///   <see cref="IOrdering"/> with TMDB's typed navigation.
/// </summary>
public interface ITmdbShowOrderingInformation : IOrdering
{
    /// <summary>
    ///   The TMDB show ID, the same ID <see cref="IOrdering.SeriesID"/> holds
    ///   as text.
    /// </summary>
    int TmdbShowID { get; }

    MetadataGuid IOrdering.SeriesID { get => new(MetadataSource.TMDB, MetadataEntityType.Series, TmdbShowID.ToString()); }

    /// <summary>
    ///   The TMDB show the ordering orders.
    /// </summary>
    /// <exception cref="NullReferenceException">The series is missing.</exception>
    new ITmdbShow Series { get; }

    ISeries IOrdering.Series { get => Series; }

    /// <summary>
    ///   The ordering's seasons in viewing order: the show's own seasons for
    ///   the default ordering, or the episode group's groups.
    /// </summary>
    new IReadOnlyList<ITmdbSeason> Seasons { get; }

    IReadOnlyList<ISeason> IOrdering.Seasons { get => Seasons; }

    /// <summary>
    ///   The ordering's episodes in viewing order, each once, where it first
    ///   comes.
    /// </summary>
    new IReadOnlyList<ITmdbEpisode> Episodes { get; }

    IReadOnlyList<IEpisode> IOrdering.Episodes { get => Episodes; }
}
