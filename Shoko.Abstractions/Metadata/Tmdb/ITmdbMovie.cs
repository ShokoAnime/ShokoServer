using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
/// A TMDB movie.
/// </summary>
public interface ITmdbMovie : IMovie, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    ///   The TMDB movie ID, the same ID <see cref="IMetadata.ID"/> holds as
    ///   text.
    /// </summary>
    int TmdbID { get; }

    /// <summary>
    ///   The TMDB collection the movie is part of, or <c>null</c> when it is
    ///   part of none.
    /// </summary>
    MetadataGuid? CollectionID { get; }

    /// <summary>
    ///   The TMDB collection ID, the same ID <see cref="CollectionID"/> holds
    ///   as text, or <c>null</c> when the movie is part of no collection.
    /// </summary>
    int? TmdbCollectionID { get; }

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
    new string OriginalLanguageCode { get; }

    string? IMovie.OriginalLanguageCode { get => OriginalLanguageCode; }

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
    /// The movies TMDB suggests to someone looking at this one, its
    /// recommendations and its similar titles alike.
    /// </summary>
    new IReadOnlyList<ITmdbMovieSuggestion> Suggestions { get; }

    /// <summary>
    /// The movies TMDB suggests this one from.
    /// </summary>
    new IReadOnlyList<ITmdbMovieSuggestion> SuggestedBy { get; }
}
