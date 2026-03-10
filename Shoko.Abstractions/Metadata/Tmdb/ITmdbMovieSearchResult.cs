using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
/// A TMDB movie search result.
/// </summary>
public interface ITmdbMovieSearchResult
{
    /// <summary>
    /// TMDB Movie ID.
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
    /// Original language the movie was shot in.
    /// </summary>
    string OriginalLanguage { get; }

    /// <summary>
    /// Preferred overview based upon description preference.
    /// </summary>
    string Overview { get; }

    /// <summary>
    /// Indicates the movie is restricted to an age group above the legal age,
    /// because it's a pornography.
    /// </summary>
    bool IsRestricted { get; }

    /// <summary>
    /// Indicates the entry is not truly a movie, including but not limited to
    /// the types:
    ///
    /// - official compilations,
    /// - best of,
    /// - filmed sport events,
    /// - music concerts,
    /// - plays or stand-up show,
    /// - fitness video,
    /// - health video,
    /// - live movie theater events (art, music),
    /// - and how-to DVDs,
    ///
    /// among others.
    /// </summary>
    bool IsVideo { get; }

    /// <summary>
    /// The date the movie first released, if it is known.
    /// </summary>
    DateOnly? ReleasedAt { get; }

    /// <summary>
    /// Relative path to the poster image.
    /// </summary>
    string? PosterPath { get; }

    /// <summary>
    /// Relative path to the backdrop image.
    /// </summary>
    string? BackdropPath { get; }

    /// <summary>
    /// User rating of the movie from TMDB users.
    /// </summary>
    decimal UserRating { get; }

    /// <summary>
    /// Number of user votes for the movie from TMDB users.
    /// </summary>
    int UserVotes { get; }

    /// <summary>
    /// Genres.
    /// </summary>
    IReadOnlyList<string> Genres { get; }
}
