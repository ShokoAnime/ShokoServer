using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Server.Extensions;
using TMDbLib.Objects.Search;

#nullable enable
namespace Shoko.Server.Providers.TMDB;

/// <summary>
/// A TMDB movie search result implementation.
/// </summary>
public class TmdbMovieSearchResult : ITmdbMovieSearchResult
{
    /// <inheritdoc/>
    public int ID { get; }

    /// <inheritdoc/>
    public string Title { get; }

    /// <inheritdoc/>
    public string OriginalTitle { get; }

    /// <inheritdoc/>
    public string OriginalLanguage { get; }

    /// <inheritdoc/>
    public string Overview { get; }

    /// <inheritdoc/>
    public bool IsRestricted { get; }

    /// <inheritdoc/>
    public bool IsVideo { get; }

    /// <inheritdoc/>
    public DateOnly? ReleasedAt { get; }

    /// <inheritdoc/>
    public string? PosterPath { get; }

    /// <inheritdoc/>
    public string? BackdropPath { get; }

    /// <inheritdoc/>
    public decimal UserRating { get; }

    /// <inheritdoc/>
    public int UserVotes { get; }

    /// <inheritdoc/>
    public IReadOnlyList<string> Genres { get; }

    /// <summary>
    /// Creates a new <see cref="TmdbMovieSearchResult"/> from a <see cref="SearchMovie"/>.
    /// </summary>
    /// <param name="movie">The TMDbLib search movie result.</param>
    public TmdbMovieSearchResult(SearchMovie movie)
    {
        ID = movie.Id;
        Title = movie.Title ?? string.Empty;
        OriginalTitle = movie.OriginalTitle ?? string.Empty;
        OriginalLanguage = movie.OriginalLanguage ?? string.Empty;
        Overview = movie.Overview ?? string.Empty;
        IsRestricted = movie.Adult;
        IsVideo = movie.Video;
        ReleasedAt = movie.ReleaseDate?.ToDateOnly();
        PosterPath = movie.PosterPath;
        BackdropPath = movie.BackdropPath;
        UserRating = (decimal)movie.VoteAverage;
        UserVotes = movie.VoteCount;
        Genres = movie.GetGenres();
    }
}
