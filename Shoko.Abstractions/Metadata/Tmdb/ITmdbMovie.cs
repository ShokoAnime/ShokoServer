using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Tmdb.CrossReferences;

namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
/// A TMDB movie.
/// </summary>
public interface ITmdbMovie : IMovie, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    /// Gets the TMDB collection ID.
    /// </summary>
    string? CollectionID { get; }

    /// <summary>
    /// Linked Imdb movie ID.
    /// </summary>
    /// <remarks>
    /// Will be <code>null</code> if not linked. Will be <code>0</code> if no
    /// Imdb link is found in TMDB. Otherwise, it will be the Imdb movie ID.
    /// </remarks>
    public string? ImdbMovieID { get; set; }

    /// <summary>
    /// The original language the TMDB movie was shot in.
    /// </summary>
    string OriginalLanguageCode { get; }

    /// <summary>
    /// ISO-3166 alpha-2 country codes.
    /// </summary>
    IReadOnlyList<string> ProductionCountries { get; }

    /// <summary>
    /// Gets the keywords for the TMDB movie.
    /// </summary>
    IReadOnlyList<string> Keywords { get; }

    /// <summary>
    /// Gets the genres for the TMDB movie.
    /// </summary>
    IReadOnlyList<string> Genres { get; }

    /// <summary>
    /// Gets the TMDB collection.
    /// </summary>
    ITmdbCollection? Collection { get; }

    /// <summary>
    /// All Shoko episode ↔ TMDB movie cross references linked to the TMDB movie.
    /// </summary>
    IReadOnlyList<ITmdbMovieCrossReference> TmdbMovieCrossReferences { get; }
}
