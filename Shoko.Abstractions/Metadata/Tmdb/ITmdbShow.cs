using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
/// A TMDB show.
/// </summary>
public interface ITmdbShow : ISeries, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    ///   The TMDB show ID, the same ID <see cref="IMetadata.ID"/> holds as
    ///   text.
    /// </summary>
    int TmdbID { get; }

    /// <summary>
    /// The ID TMDB lists for this show among its external IDs, if known.
    /// </summary>
    int? TvdbShowID { get; }

    /// <summary>
    /// The original language the TMDB show was shot in.
    /// </summary>
    new string OriginalLanguageCode { get; }

    string? ISeries.OriginalLanguageCode { get => OriginalLanguageCode; }

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
    ///   TMDB's own orderings of the show: its default ordering, made from its
    ///   seasons, first, then its locally available episode groups. The
    ///   orderings others made of it are in <see cref="ISeries.Orderings"/>.
    /// </summary>
    IReadOnlyList<ITmdbShowOrderingInformation> TmdbOrderings { get; }

    /// <summary>
    /// All seasons for the TMDB show.
    /// </summary>
    new IReadOnlyList<ITmdbSeason> Seasons { get; }

    /// <summary>
    /// All episodes for the TMDB show.
    /// </summary>
    new IReadOnlyList<ITmdbEpisode> Episodes { get; }

    /// <summary>
    /// The shows TMDB suggests to someone looking at this one, its
    /// recommendations and its similar titles alike.
    /// </summary>
    new IReadOnlyList<ITmdbShowSuggestion> Suggestions { get; }

    /// <summary>
    /// The shows TMDB suggests this one from.
    /// </summary>
    new IReadOnlyList<ITmdbShowSuggestion> SuggestedBy { get; }
}
