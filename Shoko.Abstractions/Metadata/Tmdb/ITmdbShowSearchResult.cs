using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
/// A TMDB show search result.
/// </summary>
public interface ITmdbShowSearchResult
{
    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    int ID { get; }

    /// <summary>
    /// English title.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Title in the original language.
    /// </summary>
    string OriginalTitle { get; }

    /// <summary>
    /// Original language the show was shot in.
    /// </summary>
    string OriginalLanguage { get; }

    /// <summary>
    /// Preferred overview based upon description preference.
    /// </summary>
    string Overview { get; }

    /// <summary>
    /// The date the first episode aired at, if it is known.
    /// </summary>
    DateOnly? FirstAiredAt { get; }

    /// <summary>
    /// Relative path to the poster image.
    /// </summary>
    string? PosterPath { get; }

    /// <summary>
    /// Relative path to the backdrop image.
    /// </summary>
    string? BackdropPath { get; }

    /// <summary>
    /// User rating of the show from TMDB users.
    /// </summary>
    decimal UserRating { get; }

    /// <summary>
    /// Number of user votes for the show from TMDB users.
    /// </summary>
    int UserVotes { get; }

    /// <summary>
    /// Genres.
    /// </summary>
    IReadOnlyList<string> Genres { get; }
}
