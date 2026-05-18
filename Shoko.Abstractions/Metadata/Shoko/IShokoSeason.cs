using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Metadata.Tmdb.CrossReferences;

namespace Shoko.Abstractions.Metadata.Shoko;

/// <summary>
/// Fake "season" for the Shoko series.
/// </summary>
public interface IShokoSeason : ISeason, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    /// Get the Shoko series info for the "season," if available.
    /// </summary>
    new IShokoSeries Series { get; }

    /// <summary>
    /// All episodes for the Shoko series for the fake "season."
    /// </summary>
    new IReadOnlyList<IShokoEpisode> Episodes { get; }

    /// <summary>
    /// A direct link to all TMDB seasons linked to the Shoko series.
    /// </summary>
    IReadOnlyList<ITmdbSeason> TmdbSeasons { get; }

    /// <summary>
    /// A direct link to all TMDB movies linked to the Shoko series.
    /// </summary>
    IReadOnlyList<ITmdbMovie> TmdbMovies { get; }

    /// <summary>
    /// All Shoko series ↔ TMDB season cross references linked to the Shoko series.
    /// </summary>
    IReadOnlyList<ITmdbSeasonCrossReference> TmdbSeasonCrossReferences { get; }

    /// <summary>
    /// All Shoko episode ↔ TMDB episode cross references linked to the Shoko series.
    /// </summary>
    IReadOnlyList<ITmdbEpisodeCrossReference> TmdbEpisodeCrossReferences { get; }

    /// <summary>
    /// All Shoko episode ↔ TMDB movie cross references linked to the Shoko series.
    /// </summary>
    IReadOnlyList<ITmdbMovieCrossReference> TmdbMovieCrossReferences { get; }

    /// <summary>
    /// All seasons linked to the fake Shoko "season."
    /// </summary>
    IReadOnlyList<ISeason> LinkedSeasons { get; }
}
