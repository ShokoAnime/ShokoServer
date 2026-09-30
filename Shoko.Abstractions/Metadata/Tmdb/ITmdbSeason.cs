using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
/// A TMDB season.
/// </summary>
public interface ITmdbSeason : ISeason, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    ///   The ID of the TMDB show this belongs to, the same ID
    ///   <see cref="ISeason.SeriesID"/> holds as text.
    /// </summary>
    int TmdbShowID { get; }

    MetadataGuid ISeason.SeriesID { get => new(MetadataSource.TMDB, MetadataEntityType.Series, TmdbShowID.ToString()); }

    /// <summary>
    ///   TMDB's own ID for the ordering the season is part of: the show ID for
    ///   one of the show's own seasons, or the episode group collection ID for
    ///   an alternate one. <see cref="ISeason.OrderingID"/> is the same
    ///   ordering as a <see cref="MetadataGuid"/>, and <c>null</c> for the
    ///   show's own seasons.
    /// </summary>
    string TmdbOrderingID { get; }

    /// <summary>
    ///   The TMDB ordering the season is part of: the show's default ordering
    ///   for one of its own seasons, or the episode group of an alternate one,
    ///   if it is available.
    /// </summary>
    ITmdbShowOrderingInformation? CurrentShowOrdering { get; }

    /// <summary>
    /// Get the TMDB show info for the season, if available.
    /// </summary>
    new ITmdbShow? Series { get; }

    /// <summary>
    /// All episodes for the TMDB season.
    /// </summary>
    new IReadOnlyList<ITmdbEpisode> Episodes { get; }
}
