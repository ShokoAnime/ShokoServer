using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Tmdb.CrossReferences;

namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
/// A TMDB show.
/// </summary>
public interface ITmdbShow : ISeries, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    /// The TvDB ID for the TMDB show.
    /// </summary>
    int? TvdbShowID { get; }

    /// <summary>
    /// The original language the TMDB show was shot in.
    /// </summary>
    string OriginalLanguageCode { get; }

    /// <summary>
    /// ISO-3166 alpha-2 country codes.
    /// </summary>
    IReadOnlyList<string> ProductionCountries { get; }

    /// <summary>
    /// The keywords for the TMDB show.
    /// </summary>
    IReadOnlyList<string> Keywords { get; }

    /// <summary>
    /// The genres for the TMDB show.
    /// </summary>
    IReadOnlyList<string> Genres { get; }

    /// <summary>
    /// The preferred ordering information for the TMDB show.
    /// </summary>
    ITmdbShowOrderingInformation PreferredOrdering { get; }

    /// <summary>
    /// All locally available ordering information for the TMDB show.
    /// </summary>
    IReadOnlyList<ITmdbShowOrderingInformation> AllOrderings { get; }

    /// <summary>
    /// All seasons for the TMDB show.
    /// </summary>
    new IReadOnlyList<ITmdbSeason> Seasons { get; }

    /// <summary>
    /// All episodes for the the TMDB show.
    /// </summary>
    new IReadOnlyList<ITmdbEpisode> Episodes { get; }

    /// <summary>
    /// All Shoko series ↔ TMDB show cross references linked to the TMDB show.
    /// </summary>
    IReadOnlyList<ITmdbShowCrossReference> TmdbShowCrossReferences { get; }

    /// <summary>
    /// All Shoko series ↔ TMDB season cross references linked to the TMDB show.
    /// </summary>
    IReadOnlyList<ITmdbSeasonCrossReference> TmdbSeasonCrossReferences { get; }

    /// <summary>
    /// All Shoko episode ↔ TMDB episode cross references linked to the TMDB show.
    /// </summary>
    IReadOnlyList<ITmdbEpisodeCrossReference> TmdbEpisodeCrossReferences { get; }
}
