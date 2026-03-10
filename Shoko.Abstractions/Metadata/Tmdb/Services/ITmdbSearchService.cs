using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Anidb;

namespace Shoko.Abstractions.Metadata.Tmdb.Services;

/// <summary>
/// TMDB search service for searching movies and shows.
/// </summary>
public interface ITmdbSearchService
{
    /// <summary>
    /// Searches for movies on TMDB.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="includeRestricted">Whether to include restricted (adult) content.</param>
    /// <param name="year">Optional year filter.</param>
    /// <param name="page">The page number (1-indexed).</param>
    /// <param name="pageSize">The number of results per page.</param>
    /// <returns>A tuple containing the page of results and the total count.</returns>
    Task<(IReadOnlyList<ITmdbMovieSearchResult> Page, int TotalCount)> SearchMovies(string query, bool includeRestricted = false, int year = 0, int page = 1, int pageSize = 6);

    /// <summary>
    /// Searches for TV shows on TMDB.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="includeRestricted">Whether to include restricted (adult) content.</param>
    /// <param name="year">Optional year filter for first air date.</param>
    /// <param name="page">The page number (1-indexed).</param>
    /// <param name="pageSize">The number of results per page.</param>
    /// <returns>A tuple containing the page of results and the total count.</returns>
    Task<(IReadOnlyList<ITmdbShowSearchResult> Page, int TotalCount)> SearchShows(string query, bool includeRestricted = false, int year = 0, int page = 1, int pageSize = 6);

    /// <summary>
    /// Performs an automatic search for TMDB matches for an AniDB anime.
    /// </summary>
    /// <param name="anime">The AniDB anime to search for.</param>
    /// <returns>A list of auto-match results.</returns>
    Task<IReadOnlyList<ITmdbAutoSearchResult>> SearchForAutoMatch(IAnidbAnime anime);
}
