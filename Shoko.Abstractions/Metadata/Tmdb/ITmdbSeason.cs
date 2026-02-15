using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Tmdb.CrossReferences;

namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
/// A TMDB season.
/// </summary>
public interface ITmdbSeason : ISeason, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    /// The ordering ID.
    /// </summary>
    string OrderingID { get; }

    /// <summary>
    /// Get the currently in use show ordering, if available.
    /// </summary>
    ITmdbShowOrderingInformation? CurrentShowOrdering { get; }

    /// <summary>
    /// Get the TMDB show info for the season, if available.
    /// </summary>
    new ITmdbShow? Series { get; }

    /// <summary>
    /// All episodes for the the TMDB season.
    /// </summary>
    new IReadOnlyList<ITmdbEpisode> Episodes { get; }

    /// <summary>
    /// All Shoko series ↔ TMDB season cross references linked to the TMDB season.
    /// </summary>
    IReadOnlyList<ITmdbSeasonCrossReference> TmdbSeasonCrossReferences { get; }

    /// <summary>
    /// All Shoko episode ↔ TMDB episode cross references linked to the TMDB season.
    /// </summary>
    IReadOnlyList<ITmdbEpisodeCrossReference> TmdbEpisodeCrossReferences { get; }
}
