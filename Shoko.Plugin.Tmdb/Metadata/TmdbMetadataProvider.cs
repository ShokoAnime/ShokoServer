using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Matching;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Plugin.Tmdb.Api;
using Shoko.Plugin.Tmdb.Mapping;
using Shoko.Plugin.Tmdb.Services;
using TMDbLib.Objects.Exceptions;

namespace Shoko.Plugin.Tmdb.Metadata;

/// <summary>
///   Supplies TMDb's shows, movies and collections, and the people, studios
///   and networks behind them, under the <c>tmdb</c> source.
/// </summary>
/// <remarks>
///   The core runs the refresh, search, image and entity jobs and calls in
///   here; the provider fetches from TMDb and writes into the core's stores,
///   and the core reads everything back from them. Without an API key it says
///   it is not configured, and while TMDb rate limits it or fails it says it
///   is paused, so the core holds its jobs back.
/// </remarks>
public sealed class TmdbMetadataProvider :
    IMetadataSeriesLinkingProvider,
    IMetadataMovieLinkingProvider,
    IMetadataCollectionProvider,
    IMetadataEntityProvider,
    IMetadataAutoLinkingProvider,
    IMetadataImageProvider,
    IPausableMetadataProvider,
    IMetadataProvider<TmdbConfiguration>,
    IDisposable
{
    #region Fields

    private readonly TmdbApiClient _apiClient;

    private readonly TmdbRefreshService _refreshService;

    private readonly TmdbEntityRefreshService _entityRefreshService;

    private readonly TmdbSearchService _searchService;

    private readonly TmdbLinkingService _linkingService;

    private readonly TmdbImageService _imageService;

    private readonly IMetadataService _metadataService;

    private readonly ILogger<TmdbMetadataProvider> _logger;

    #endregion

    #region Constructors

    /// <summary>
    ///   Creates the provider.
    /// </summary>
    /// <param name="apiClient">The TMDb client, whose key makes up the configuration and whose rate limiter makes up the pause.</param>
    /// <param name="refreshService">Fetches shows, movies and collections and writes them into the stores.</param>
    /// <param name="entityRefreshService">Fetches people, companies and networks.</param>
    /// <param name="searchService">Searches TMDb.</param>
    /// <param name="linkingService">Matches episodes.</param>
    /// <param name="imageService">Hands out the images.</param>
    /// <param name="metadataService">The core's metadata service, for the AniDB anime to link.</param>
    /// <param name="logger">The logger.</param>
    public TmdbMetadataProvider(
        TmdbApiClient apiClient,
        TmdbRefreshService refreshService,
        TmdbEntityRefreshService entityRefreshService,
        TmdbSearchService searchService,
        TmdbLinkingService linkingService,
        TmdbImageService imageService,
        IMetadataService metadataService,
        ILogger<TmdbMetadataProvider> logger
    )
    {
        _apiClient = apiClient;
        _refreshService = refreshService;
        _entityRefreshService = entityRefreshService;
        _searchService = searchService;
        _linkingService = linkingService;
        _imageService = imageService;
        _metadataService = metadataService;
        _logger = logger;
        _apiClient.RateLimiter.PauseStateChanged += OnPauseStateChanged;
    }

    #endregion

    #region Provider

    /// <inheritdoc/>
    public string Name => "TMDb";

    /// <inheritdoc/>
    public string? Description => "TMDb, The Movie Database: shows, movies and the collections they belong to, with the people, studios and networks behind them.";

    /// <inheritdoc/>
    public MetadataSource Source => MetadataSource.TMDB;

    /// <inheritdoc/>
    public string? EmbeddedIconResourceName => Plugin.IconResourceName;

    /// <summary>
    ///   Links on its own from the start, restricted entries included, as the
    ///   core's TMDb provider did.
    /// </summary>
    public bool AutoLinkByDefault => true;

    /// <inheritdoc/>
    public bool AutoLinkRestrictedByDefault => true;

    /// <summary>
    ///   Configured while an API key is available, the configured one or the
    ///   one an official build ships with.
    /// </summary>
    public bool IsConfigured => _apiClient.HasApiKey;

    /// <inheritdoc/>
    public string? NotConfiguredReason => _apiClient.HasApiKey ? null : TmdbApiClient.NoApiKeyReason;

    /// <summary>
    ///   Four of each job at once. Every request is paced by the rate limiter
    ///   whatever runs it, so this only keeps a library-wide refresh from
    ///   crowding out the rest of the queue.
    /// </summary>
    public int? MaxConcurrentJobs => 4;

    /// <summary>
    ///   The plugin keeps nothing of its own, so there is nothing to clean up
    ///   after the core purges an entry.
    /// </summary>
    /// <param name="entryID">The purged entry.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task CleanUp(MetadataGuid entryID, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    #endregion

    #region Refresh

    /// <inheritdoc/>
    /// <exception cref="MetadataProviderNotConfiguredException">No API key is configured.</exception>
    public async Task RefreshSeries(MetadataGuid seriesID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seriesID);
        ArgumentNullException.ThrowIfNull(options);
        if (TmdbIds.TryGetID(seriesID, MetadataEntityType.Series, out var showID))
            await _refreshService.RefreshShow(showID, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <exception cref="MetadataProviderNotConfiguredException">No API key is configured.</exception>
    public async Task RefreshMovie(MetadataGuid movieID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(movieID);
        ArgumentNullException.ThrowIfNull(options);
        if (TmdbIds.TryGetID(movieID, MetadataEntityType.Movie, out var tmdbMovieID))
            await _refreshService.RefreshMovie(tmdbMovieID, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <exception cref="MetadataProviderNotConfiguredException">No API key is configured.</exception>
    public async Task RefreshCollection(MetadataGuid collectionID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collectionID);
        ArgumentNullException.ThrowIfNull(options);
        if (TmdbIds.TryGetID(collectionID, MetadataEntityType.Collection, out var tmdbCollectionID))
            await _refreshService.RefreshCollection(tmdbCollectionID, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Entities

    /// <summary>
    ///   TMDb's people, companies and networks. TMDb has no characters of
    ///   its own.
    /// </summary>
    public MetadataEntityScope EntityScope { get; } =
        MetadataEntityScope.ForSource(MetadataSource.TMDB, MetadataEntityType.Creator, MetadataEntityType.Studio, MetadataEntityType.Network);

    /// <inheritdoc/>
    /// <exception cref="MetadataProviderNotConfiguredException">No API key is configured.</exception>
    public Task<bool> RefreshEntity(MetadataGuid entityID, CancellationToken cancellationToken = default)
        => _entityRefreshService.Refresh(entityID, cancellationToken);

    #endregion

    #region Site URLs

    /// <summary>
    ///   The page of a TMDb show, season, episode, movie or collection, and
    ///   of a person, company or network when asked; a group of an ordering
    ///   has none.
    /// </summary>
    /// <param name="entry">The entry, of the TMDb source.</param>
    /// <returns>The URL, or <see langword="null"/>.</returns>
    public string? GetSiteUrl(IMetadata entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return TmdbSiteUrls.ForEntry(entry);
    }

    #endregion

    #region Pausing

    /// <summary>
    ///   Paused while TMDb rate limits the plugin or answers with server
    ///   errors.
    /// </summary>
    public MetadataProviderPauseStatus PauseStatus
    {
        get
        {
            var rateLimiter = _apiClient.RateLimiter;
            if (rateLimiter.PauseReason is var reason and not TmdbPauseReason.None)
                return new()
                {
                    IsPaused = true,
                    Reason = reason is TmdbPauseReason.RateLimited
                        ? "TMDb is limiting the rate of requests, so its requests are paused for a while."
                        : "TMDb answered with server errors, so its requests are paused for a while.",
                    ResumesAt = rateLimiter.ResumesAt?.UtcDateTime,
                };

            return MetadataProviderPauseStatus.NotPaused;
        }
    }

    /// <inheritdoc/>
    public event EventHandler? PauseStatusChanged;

    private void OnPauseStateChanged(object? sender, EventArgs eventArgs)
        => PauseStatusChanged?.Invoke(this, EventArgs.Empty);

    #endregion

    #region Images

    /// <inheritdoc/>
    public Task<IReadOnlyList<ImageCandidate>?> GetImages(MetadataGuid entityID, CancellationToken cancellationToken = default)
        => _imageService.GetImages(entityID, cancellationToken);

    #endregion

    #region Search & Linking

    /// <summary>
    ///   A show, its episodes, and a movie, as the core's TMDb provider took
    ///   links for. A season is matched through its episodes.
    /// </summary>
    public IReadOnlySet<MetadataEntityType> LinkableEntityTypes { get; } =
        FrozenSet.ToFrozenSet([MetadataEntityType.Series, MetadataEntityType.Episode, MetadataEntityType.Movie]);

    /// <inheritdoc/>
    /// <exception cref="MetadataProviderUnavailableException">TMDb failed or is rate limiting the plugin.</exception>
    public Task<(IReadOnlyList<MetadataSeriesSearchResult> Page, int TotalCount)> SearchSeries(MetadataSearchOptions options, CancellationToken cancellationToken = default)
        => Upstream(() => _searchService.SearchSeries(options, cancellationToken));

    /// <inheritdoc/>
    /// <exception cref="MetadataProviderUnavailableException">TMDb failed or is rate limiting the plugin.</exception>
    public Task<(IReadOnlyList<MetadataMovieSearchResult> Page, int TotalCount)> SearchMovies(MetadataSearchOptions options, CancellationToken cancellationToken = default)
        => Upstream(() => _searchService.SearchMovies(options, cancellationToken));

    /// <inheritdoc/>
    /// <exception cref="MetadataProviderUnavailableException">TMDb failed or is rate limiting the plugin.</exception>
    public Task<MetadataSeriesSearchResult?> LookupSeries(MetadataGuid seriesID, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seriesID);
        return TmdbIds.TryGetID(seriesID, MetadataEntityType.Series, out var showID)
            ? Upstream(() => _searchService.LookupSeries(showID, cancellationToken))
            : Task.FromResult<MetadataSeriesSearchResult?>(null);
    }

    /// <inheritdoc/>
    /// <exception cref="MetadataProviderUnavailableException">TMDb failed or is rate limiting the plugin.</exception>
    public Task<MetadataMovieSearchResult?> LookupMovie(MetadataGuid movieID, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(movieID);
        return TmdbIds.TryGetID(movieID, MetadataEntityType.Movie, out var tmdbMovieID)
            ? Upstream(() => _searchService.LookupMovie(tmdbMovieID, cancellationToken))
            : Task.FromResult<MetadataMovieSearchResult?>(null);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<EpisodeMatch>> MatchEpisodes(
        IAnidbAnime anime,
        IReadOnlyList<IAnidbEpisode> anidbEpisodes,
        MetadataGuid providerSeriesID,
        MetadataGuid? providerSeasonID = null,
        IReadOnlyList<IMetadataEpisodeCrossReference>? existing = null,
        bool? considerOtherLinks = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(anime);
        ArgumentNullException.ThrowIfNull(anidbEpisodes);
        ArgumentNullException.ThrowIfNull(providerSeriesID);

        return TmdbIds.TryGetID(providerSeriesID, MetadataEntityType.Series, out var showID)
            ? Task.FromResult(_linkingService.Match(anime, anidbEpisodes, showID, providerSeasonID, existing, considerOtherLinks))
            : Task.FromResult<IReadOnlyList<EpisodeMatch>>([]);
    }

    /// <summary>
    ///   Searches TMDb for an anime and hands back every candidate judged,
    ///   without linking anything: a movie per AniDB episode standing for
    ///   one, or a show, also by the earliest prequel's titles.
    /// </summary>
    /// <remarks>
    ///   Every candidate judged comes back, the rejected ones with the
    ///   engine's reason, then the prequel's linked shows for context only,
    ///   then the entries the anime's cross-source IDs or its other links
    ///   name, which the core may take when the search took nothing better.
    /// </remarks>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The candidates, best first, or nothing without an API key or for an anime not stored.</returns>
    /// <exception cref="MetadataProviderUnavailableException">TMDb failed or is rate limiting the plugin.</exception>
    public async Task<IReadOnlyList<MetadataAutoLinkCandidate>> FindAutoLinks(int anidbAnimeID, CancellationToken cancellationToken = default)
    {
        if (anidbAnimeID <= 0 || !_apiClient.HasApiKey)
            return [];

        var anime = _metadataService.GetSeries(new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, TmdbIds.Format(anidbAnimeID))) as IAnidbAnime
            ?? _metadataService.GetShokoSeriesByAnidbID(anidbAnimeID)?.AnidbAnime;
        if (anime is null)
        {
            _logger.LogWarning("AniDB anime {AnimeID} is not available locally.", anidbAnimeID);
            return [];
        }

        return await Upstream(() => _searchService.FindAutoLinks(anime, cancellationToken)).ConfigureAwait(false);
    }

    #endregion

    #region Upstream Failures

    /// <summary>
    ///   Runs a call a person or the auto-linker waits on, and turns TMDb
    ///   being down or out of reach into a <see cref="MetadataProviderUnavailableException"/>,
    ///   so the caller can retry later.
    /// </summary>
    /// <typeparam name="T">What the call returns.</typeparam>
    /// <param name="call">The call.</param>
    /// <returns>What the call returned.</returns>
    /// <exception cref="MetadataProviderUnavailableException">TMDb answered with a server error, kept limiting the rate, or could not be reached.</exception>
    private async Task<T> Upstream<T>(Func<Task<T>> call)
    {
        try
        {
            return await call().ConfigureAwait(false);
        }
        catch (Exception ex) when (ToUnavailable(ex, RemainingPause()) is { } unavailable)
        {
            throw unavailable;
        }
    }

    private TimeSpan? RemainingPause()
        => _apiClient.RateLimiter.ResumesAt is { } resumesAt && resumesAt - _apiClient.TimeProvider.GetUtcNow() is { Ticks: > 0 } remaining ? remaining : null;

    /// <summary>
    ///   The <see cref="MetadataProviderUnavailableException"/> a failed call
    ///   stands for, when the failure is one that passes.
    /// </summary>
    /// <param name="exception">What the call threw.</param>
    /// <param name="remainingPause">What is left of the pause, if any.</param>
    /// <returns>The exception to throw instead, or <see langword="null"/> for a failure retrying would not mend.</returns>
    internal static MetadataProviderUnavailableException? ToUnavailable(Exception exception, TimeSpan? remainingPause)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            MetadataProviderUnavailableException => null,
            AggregateException { InnerExceptions: [MetadataProviderUnavailableException inner] } => inner,
            AggregateException { InnerExceptions: [var inner] } => ToUnavailable(inner, remainingPause),
            RequestLimitExceededException limited => new(MetadataSource.TMDB, "TMDb kept limiting the rate of requests.", limited.RetryAfter ?? remainingPause, exception),
            GeneralHttpException { HttpStatusCode: >= HttpStatusCode.InternalServerError } http =>
                new(MetadataSource.TMDB, $"TMDb answered with a server error ({(int)http.HttpStatusCode}).", remainingPause, exception),
            HttpRequestException => new(MetadataSource.TMDB, "TMDb could not be reached.", remainingPause, exception),
            TaskCanceledException { InnerException: TimeoutException } => new(MetadataSource.TMDB, "TMDb did not answer in time.", remainingPause, exception),
            _ => null,
        };
    }

    #endregion

    /// <summary>
    ///   Stops listening for pauses.
    /// </summary>
    public void Dispose()
        => _apiClient.RateLimiter.PauseStateChanged -= OnPauseStateChanged;
}
