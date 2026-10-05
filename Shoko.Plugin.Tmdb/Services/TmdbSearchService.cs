using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Plugin.Tmdb.Api;
using Shoko.Plugin.Tmdb.Mapping;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.TvShows;

namespace Shoko.Plugin.Tmdb.Services;

/// <summary>
///   Searches TMDb for the shows and movies a person or the auto-linker
///   might link an anime to.
/// </summary>
/// <remarks>
///   A search hands back what TMDb said, its hits of the animation genre
///   first, with the genres named through the tag store. The auto-linker's
///   half is in the other part of this class.
/// </remarks>
public sealed partial class TmdbSearchService
{
    #region Fields

    private readonly TmdbApiClient _apiClient;

    private readonly TmdbStores _stores;

    private readonly TmdbTagService _tags;

    private readonly IMetadataMatchingEngine _matchingEngine;

    private readonly IMetadataService _metadataService;

    private readonly IMetadataLinkingService _linkingService;

    private readonly ConfigurationProvider<TmdbConfiguration> _configurationProvider;

    private readonly ILogger<TmdbSearchService> _logger;

    #endregion

    #region Constructors

    /// <summary>
    ///   Creates the search service.
    /// </summary>
    /// <param name="apiClient">The TMDb client.</param>
    /// <param name="stores">The core's _stores.</param>
    /// <param name="tags">Names the genres of TMDb's search hits.</param>
    /// <param name="matchingEngine">The core's matching engine, which judges the auto-search's candidates.</param>
    /// <param name="metadataService">The core's metadata service, for the links of an anime's prequel.</param>
    /// <param name="linkingService">The core's linking service, for the hints the anime's other links give.</param>
    /// <param name="configurationProvider">The plugin's configuration.</param>
    /// <param name="logger">The _logger.</param>
    public TmdbSearchService(
        TmdbApiClient apiClient,
        TmdbStores stores,
        TmdbTagService tags,
        IMetadataMatchingEngine matchingEngine,
        IMetadataService metadataService,
        IMetadataLinkingService linkingService,
        ConfigurationProvider<TmdbConfiguration> configurationProvider,
        ILogger<TmdbSearchService> logger
    )
    {
        _apiClient = apiClient;
        _stores = stores;
        _tags = tags;
        _matchingEngine = matchingEngine;
        _metadataService = metadataService;
        _linkingService = linkingService;
        _configurationProvider = configurationProvider;
        _logger = logger;
    }

    #endregion

    #region Constants

    /// <summary>
    ///   TMDb's ID for the animation genre, the same for shows and movies.
    /// </summary>
    internal const int AnimationGenreID = 16;

    // "cn" is TMDb's code for Cantonese.
    private static readonly HashSet<string> RestrictedLanguages = new(StringComparer.OrdinalIgnoreCase) { "ja", "zh", "cn", "ko" };

    #endregion

    #region Searching

    /// <summary>
    ///   Searches TMDb's shows.
    /// </summary>
    /// <param name="options">What to search for.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The page asked for, and how many hits there are in total.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public async Task<(IReadOnlyList<MetadataSeriesSearchResult> Page, int TotalCount)> SearchSeries(MetadataSearchOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var (page, total) = await SearchShowsRaw(options.Query, options.IncludeRestricted, options.Year ?? 0, options.Page, options.PageSize, cancellationToken).ConfigureAwait(false);
        var imageServer = await _apiClient.GetImageServerUrl(cancellationToken).ConfigureAwait(false);
        var genreName = await _tags.GetGenreNames(page.SelectMany(show => show.GenreIds ?? []), cancellationToken).ConfigureAwait(false);
        return ([.. page.Select(show => TmdbSearchResults.FromSearch(show, imageServer, genreName))], total);
    }

    /// <summary>
    ///   Searches TMDb's movies.
    /// </summary>
    /// <param name="options">What to search for.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The page asked for, and how many hits there are in total.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public async Task<(IReadOnlyList<MetadataMovieSearchResult> Page, int TotalCount)> SearchMovies(MetadataSearchOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var (page, total) = await SearchMoviesRaw(options.Query, options.IncludeRestricted, options.Year ?? 0, options.Page, options.PageSize, cancellationToken).ConfigureAwait(false);
        var imageServer = await _apiClient.GetImageServerUrl(cancellationToken).ConfigureAwait(false);
        var genreName = await _tags.GetGenreNames(page.SelectMany(movie => movie.GenreIds ?? []), cancellationToken).ConfigureAwait(false);
        return ([.. page.Select(movie => TmdbSearchResults.FromSearch(movie, imageServer, genreName))], total);
    }

    /// <summary>
    ///   Looks one show up by its ID: the stored one when there is one, else
    ///   TMDb's.
    /// </summary>
    /// <param name="showID">The TMDb show ID.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The show, or <see langword="null"/> when TMDb has none.</returns>
    public async Task<MetadataSeriesSearchResult?> LookupSeries(int showID, CancellationToken cancellationToken = default)
    {
        var imageServer = await _apiClient.GetImageServerUrl(cancellationToken).ConfigureAwait(false);
        if (_stores.Series.GetSeries(TmdbIds.Series(showID)) is { } stored)
            return TmdbSearchResults.FromStored(stored, imageServer);

        return await _apiClient.GetShow(showID, TvShowMethods.Translations, cancellationToken).ConfigureAwait(false) is { } remote
            ? TmdbSearchResults.FromShow(remote, imageServer)
            : null;
    }

    /// <summary>
    ///   Looks one movie up by its ID: the stored one when there is one, else
    ///   TMDb's.
    /// </summary>
    /// <param name="movieID">The TMDb movie ID.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The movie, or <see langword="null"/> when TMDb has none.</returns>
    public async Task<MetadataMovieSearchResult?> LookupMovie(int movieID, CancellationToken cancellationToken = default)
    {
        var imageServer = await _apiClient.GetImageServerUrl(cancellationToken).ConfigureAwait(false);
        if (_stores.Movies.GetMovie(TmdbIds.Movie(movieID)) is { } stored)
            return TmdbSearchResults.FromStored(stored, imageServer);

        return await _apiClient.GetMovie(movieID, MovieMethods.Translations | MovieMethods.ReleaseDates, cancellationToken).ConfigureAwait(false) is { } remote
            ? TmdbSearchResults.FromMovie(remote, imageServer)
            : null;
    }

    /// <summary>
    ///   One page of TMDb's show search, cut to the page size asked for from
    ///   TMDb's own pages, the hits of the animation genre first.
    /// </summary>
    /// <param name="query">What to search for.</param>
    /// <param name="includeRestricted">Whether to include adult shows.</param>
    /// <param name="year">The year the show first aired, or <c>0</c> for any.</param>
    /// <param name="page">The page, from one.</param>
    /// <param name="pageSize">How many hits a page holds.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The page, and how many hits there are in total.</returns>
    internal async Task<(List<SearchTv> Page, int TotalCount)> SearchShowsRaw(
        string query,
        bool includeRestricted = false,
        int year = 0,
        int page = 1,
        int pageSize = 6,
        CancellationToken cancellationToken = default
    )
    {
        var (results, total) = await Page(
            pageNumber => _apiClient.SearchShows(query, pageNumber, includeRestricted, year, cancellationToken),
            container => (container.Results ?? [], container.TotalResults, container.TotalPages),
            page,
            pageSize
        ).ConfigureAwait(false);
        return ([.. results.OrderByDescending(show => show.GenreIds?.Contains(AnimationGenreID) ?? false)], total);
    }

    /// <summary>
    ///   One page of TMDb's movie search, as <see cref="SearchShowsRaw"/>.
    /// </summary>
    /// <param name="query">What to search for.</param>
    /// <param name="includeRestricted">Whether to include adult movies.</param>
    /// <param name="year">The year the movie was released, or <c>0</c> for any.</param>
    /// <param name="page">The page, from one.</param>
    /// <param name="pageSize">How many hits a page holds.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The page, and how many hits there are in total.</returns>
    internal async Task<(List<SearchMovie> Page, int TotalCount)> SearchMoviesRaw(
        string query,
        bool includeRestricted = false,
        int year = 0,
        int page = 1,
        int pageSize = 6,
        CancellationToken cancellationToken = default
    )
    {
        var (results, total) = await Page(
            pageNumber => _apiClient.SearchMovies(query, pageNumber, includeRestricted, year, cancellationToken),
            container => (container.Results ?? [], container.TotalResults, container.TotalPages),
            page,
            pageSize
        ).ConfigureAwait(false);
        return ([.. results.OrderByDescending(movie => movie.GenreIds?.Contains(AnimationGenreID) ?? false)], total);
    }

    /// <summary>
    ///   Cuts a page of a size out of TMDb's own pages, fetching the ones it
    ///   spans.
    /// </summary>
    /// <typeparam name="TContainer">TMDb's page.</typeparam>
    /// <typeparam name="THit">One hit.</typeparam>
    /// <param name="fetch">Fetches one of TMDb's pages.</param>
    /// <param name="read">Reads a page's hits and totals.</param>
    /// <param name="page">The page wanted, from one.</param>
    /// <param name="pageSize">How many hits the page wanted holds.</param>
    /// <returns>The hits, and how many there are in total.</returns>
    private static async Task<(List<THit> Page, int TotalCount)> Page<TContainer, THit>(
        Func<int, Task<TContainer>> fetch,
        Func<TContainer, (List<THit> Results, int Total, int TotalPages)> read,
        int page,
        int pageSize
    )
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);
        var (firstResults, total, lastPage) = read(await fetch(1).ConfigureAwait(false));
        if (total is 0 || firstResults.Count is 0)
            return ([], total);

        var actualPageSize = firstResults.Count;
        var startIndex = (page - 1) * pageSize;
        var startPage = (startIndex / actualPageSize) + 1;
        var endIndex = Math.Min(startIndex + pageSize, total);
        var endPage = total == endIndex ? lastPage : Math.Min((endIndex / actualPageSize) + (endIndex % actualPageSize > 0 ? 1 : 0), lastPage);
        var results = new List<THit>();
        for (var pageNumber = startPage; pageNumber <= endPage; pageNumber++)
            results.AddRange(pageNumber is 1 ? firstResults : read(await fetch(pageNumber).ConfigureAwait(false)).Results);

        var skip = startIndex - ((startPage - 1) * actualPageSize);
        return ([.. results.Skip(skip).Take(pageSize)], total);
    }

    #endregion
}
