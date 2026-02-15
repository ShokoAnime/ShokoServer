using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// Season Metadata.
/// </summary>
public interface ISeason : IWithTitles, IWithDescriptions, IWithImages, IWithCastAndCrew, IWithYearlySeasons, IMetadata<string>
{
    /// <summary>
    /// The TMDB show ID.
    /// </summary>
    int SeriesID { get; }

    /// <summary>
    /// Season number for default ordering.
    /// </summary>
    int SeasonNumber { get; }

    /// <summary>
    /// Default poster for the season.
    /// </summary>
    IImage? DefaultPoster { get; }

    /// <summary>
    /// Get the series info for the season, if available.
    /// </summary>
    ISeries? Series { get; }

    /// <summary>
    /// All episodes for the the season.
    /// </summary>
    IReadOnlyList<IEpisode> Episodes { get; }
}
