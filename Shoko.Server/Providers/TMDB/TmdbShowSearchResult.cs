using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Server.Extensions;
using TMDbLib.Objects.Search;

#nullable enable
namespace Shoko.Server.Providers.TMDB;

/// <summary>
/// A TMDB show search result implementation.
/// </summary>
public class TmdbShowSearchResult : ITmdbShowSearchResult
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
    public DateOnly? FirstAiredAt { get; }

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
    /// Creates a new <see cref="TmdbShowSearchResult"/> from a <see cref="SearchTv"/>.
    /// </summary>
    /// <param name="show">The TMDbLib search TV result.</param>
    public TmdbShowSearchResult(SearchTv show)
    {
        ID = show.Id;
        Title = show.Name ?? string.Empty;
        OriginalTitle = show.OriginalName ?? string.Empty;
        OriginalLanguage = show.OriginalLanguage ?? string.Empty;
        Overview = show.Overview ?? string.Empty;
        FirstAiredAt = show.FirstAirDate?.ToDateOnly();
        PosterPath = show.PosterPath;
        BackdropPath = show.BackdropPath;
        UserRating = (decimal)show.VoteAverage;
        UserVotes = show.VoteCount;
        Genres = show.GetGenres();
    }
}
