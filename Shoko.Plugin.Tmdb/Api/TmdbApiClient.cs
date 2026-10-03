using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Events;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using TMDbLib.Client;
using TMDbLib.Objects.Collections;
using TMDbLib.Objects.Companies;
using TMDbLib.Objects.Exceptions;
using TMDbLib.Objects.Find;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.People;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.TvShows;

namespace Shoko.Plugin.Tmdb.Api;

/// <summary>
///   Makes every call to TMDb, through the rate limiter, a bulkhead and the
///   retries, so the rate limiter sees every request.
/// </summary>
/// <remarks>
///   A rate limit is waited out and retried ten times, a timeout three times;
///   a server error counts towards the pause and is thrown. TMDb having no
///   such entry answers <see langword="null"/>.
/// </remarks>
public sealed class TmdbApiClient : IDisposable
{
    #region Constants

    /// <summary>
    ///   Why the plugin is not configured while no API key is available.
    /// </summary>
    internal const string NoApiKeyReason = "No TMDb API key is configured.";

    /// <summary>
    ///   TMDb's image server, used when TMDb cannot be asked for its own.
    /// </summary>
    internal const string DefaultImageServerUrl = "https://image.tmdb.org/t/p/";

    /// <summary>
    ///   How many requests may be in flight at once.
    /// </summary>
    internal static readonly int MaxConcurrency = Math.Min(6, Environment.ProcessorCount);

    private const int MaxRateLimitRetries = 10;

    private const int MaxTimeoutRetries = 3;

    #endregion

    #region Fields

    private readonly ConfigurationProvider<TmdbConfiguration> _configurationProvider;

    private readonly Func<string, TMDbClient> _clientFactory;

    private readonly ILogger<TmdbApiClient> _logger;

    private readonly SemaphoreSlim _bulkhead = new(MaxConcurrency, MaxConcurrency);

    private readonly Lock _clientLock = new();

    private TMDbClient? _client;

    private string? _clientKey;

    private string? _imageServerUrl;

    #endregion

    #region Constructors

    /// <summary>
    ///   Creates the client, with TMDb's own client made on first use.
    /// </summary>
    /// <param name="configurationProvider">The plugin's configuration, for the key, the window and the changes window.</param>
    /// <param name="logger">Where the calls are logged.</param>
    /// <param name="timeProvider">The clock the pauses and the changes window are measured by; the system's when left out.</param>
    public TmdbApiClient(ConfigurationProvider<TmdbConfiguration> configurationProvider, ILogger<TmdbApiClient> logger, TimeProvider? timeProvider = null)
        : this(configurationProvider, logger, apiKey => new TMDbClient(apiKey), timeProvider)
    {
    }

    /// <summary>
    ///   Creates the client with a factory for TMDb's own client, which the
    ///   tests point at recorded answers.
    /// </summary>
    /// <param name="configurationProvider">The plugin's configuration.</param>
    /// <param name="logger">Where the calls are logged.</param>
    /// <param name="clientFactory">Makes TMDb's client for an API key.</param>
    /// <param name="timeProvider">The clock; the system's when left out.</param>
    internal TmdbApiClient(
        ConfigurationProvider<TmdbConfiguration> configurationProvider,
        ILogger<TmdbApiClient> logger,
        Func<string, TMDbClient> clientFactory,
        TimeProvider? timeProvider = null
    )
    {
        _configurationProvider = configurationProvider;
        _logger = logger;
        _clientFactory = clientFactory;
        TimeProvider = timeProvider ?? TimeProvider.System;
        RateLimiter = new(configurationProvider.Load().RateLimit, TimeProvider);
        _configurationProvider.Saved += OnConfigurationSaved;
    }

    #endregion

    #region Properties

    /// <summary>
    ///   The rate limiter every request goes through, which also holds the
    ///   plugin's pause.
    /// </summary>
    public TmdbRateLimiter RateLimiter { get; }

    /// <summary>
    ///   The clock the client measures by.
    /// </summary>
    public TimeProvider TimeProvider { get; }

    /// <summary>
    ///   Whether an API key is available, the configured one or one built in.
    /// </summary>
    public bool HasApiKey => TmdbApiKey.Resolve(_configurationProvider.Load()) is not null;

    #endregion

    #region Calls

    /// <summary>
    ///   Calls TMDb through the rate limiter, the bulkhead and the retries.
    /// </summary>
    /// <typeparam name="T">What the call answers.</typeparam>
    /// <param name="call">The call.</param>
    /// <param name="displayName">What the call is, for the logs.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What TMDb answered, or <see langword="null"/> when it has no such entry.</returns>
    /// <exception cref="MetadataProviderNotConfiguredException">No API key is available.</exception>
    /// <exception cref="RequestLimitExceededException">TMDb kept limiting the rate.</exception>
    /// <exception cref="GeneralHttpException">TMDb answered with an error.</exception>
    /// <exception cref="HttpRequestException">TMDb could not be reached.</exception>
    public async Task<T?> UseClient<T>(Func<TMDbClient, CancellationToken, Task<T?>> call, string displayName, CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(call);

        var client = GetClient();
        var started = Stopwatch.GetTimestamp();
        var rateLimited = 0;
        var timedOut = 0;
        await _bulkhead.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            while (true)
            {
                try
                {
                    var result = await RateLimiter.EnsureRateAsync(() => call(client, cancellationToken), cancellationToken).ConfigureAwait(false);
                    RateLimiter.NotifySuccess();
                    _logger.LogTrace("Completed call: {DisplayName} ({Elapsed}ms)", displayName, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                    return result;
                }
                catch (NotFoundException)
                {
                    RateLimiter.NotifySuccess();
                    _logger.LogTrace("Completed call: {DisplayName} (not found)", displayName);
                    return null;
                }
                catch (RequestLimitExceededException ex) when (++rateLimited <= MaxRateLimitRetries)
                {
                    _logger.LogTrace("Hit the remote rate limit on {DisplayName}; retrying ({Attempt}).", displayName, rateLimited);
                    RateLimiter.NotifyRateLimitExceeded(ex.RetryAfter);
                }
                catch (HttpRequestException ex) when (ex.InnerException is TaskCanceledException && !cancellationToken.IsCancellationRequested && ++timedOut <= MaxTimeoutRetries)
                {
                    _logger.LogTrace("Timed out on {DisplayName}; retrying ({Attempt}).", displayName, timedOut);
                }
                catch (GeneralHttpException ex)
                {
                    if ((int)ex.HttpStatusCode >= 500)
                        RateLimiter.NotifyServerError();
                    throw;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed call:    {DisplayName} ({Elapsed}ms)", displayName, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
        finally
        {
            _bulkhead.Release();
        }
    }

    /// <summary>
    ///   Whether a failure is one a later attempt may get past: a network
    ///   error, a timeout, TMDb's rate limit or a server error.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <returns><see langword="true"/> for a failure worth retrying later.</returns>
    public static bool IsTransient(Exception exception)
        => exception switch
        {
            AggregateException { InnerExceptions: [var inner] } => IsTransient(inner),
            HttpRequestException or RequestLimitExceededException => true,
            GeneralHttpException http => (int)http.HttpStatusCode >= 500,
            TaskCanceledException { InnerException: TimeoutException } => true,
            _ => false,
        };

    private TMDbClient GetClient()
    {
        var apiKey = TmdbApiKey.Resolve(_configurationProvider.Load())
            ?? throw new MetadataProviderNotConfiguredException(MetadataSource.TMDB, NoApiKeyReason);
        lock (_clientLock)
        {
            if (_client is not null && string.Equals(_clientKey, apiKey, StringComparison.Ordinal))
                return _client;

            // The old client may still be serving calls, so it is left to the GC.
            _client = _clientFactory(apiKey);
            _clientKey = apiKey;
            return _client;
        }
    }

    private void OnConfigurationSaved(object? sender, ConfigurationSavedEventArgs<TmdbConfiguration> eventArgs)
        => RateLimiter.Reconfigure(_configurationProvider.Load().RateLimit);

    #endregion

    #region Shows

    /// <summary>
    ///   Gets a show, with whatever is appended to it.
    /// </summary>
    /// <param name="showID">The TMDb show ID.</param>
    /// <param name="methods">What to append.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The show, or <see langword="null"/> when TMDb has none.</returns>
    public Task<TvShow?> GetShow(int showID, TvShowMethods methods, CancellationToken cancellationToken = default)
        => UseClient((client, token) => client.GetTvShowAsync(showID, methods, "en-US", cancellationToken: token), $"Get show {showID}", cancellationToken);

    /// <summary>
    ///   Gets a season of a show, with its episodes.
    /// </summary>
    /// <param name="showID">The TMDb show ID.</param>
    /// <param name="seasonNumber">The season number.</param>
    /// <param name="methods">What to append.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The season, or <see langword="null"/> when TMDb has none.</returns>
    public Task<TvSeason?> GetSeason(int showID, int seasonNumber, TvSeasonMethods methods, CancellationToken cancellationToken = default)
        => UseClient(
            (client, token) => client.GetTvSeasonAsync(showID, seasonNumber, methods, cancellationToken: token),
            $"Get season {seasonNumber} of show {showID}",
            cancellationToken
        );

    /// <summary>
    ///   Gets an episode of a show.
    /// </summary>
    /// <param name="showID">The TMDb show ID.</param>
    /// <param name="seasonNumber">The season number.</param>
    /// <param name="episodeNumber">The episode number within the season.</param>
    /// <param name="methods">What to append.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The episode, or <see langword="null"/> when TMDb has none.</returns>
    public Task<TvEpisode?> GetEpisode(int showID, int seasonNumber, int episodeNumber, TvEpisodeMethods methods, CancellationToken cancellationToken = default)
        => UseClient(
            (client, token) => client.GetTvEpisodeAsync(showID, seasonNumber, episodeNumber, methods, cancellationToken: token),
            $"Get episode {episodeNumber} of season {seasonNumber} of show {showID}",
            cancellationToken
        );

    /// <summary>
    ///   Gets an episode group collection, with its groups and episodes.
    /// </summary>
    /// <param name="collectionID">The episode group collection ID.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The collection, or <see langword="null"/> when TMDb has none.</returns>
    public Task<TvGroupCollection?> GetEpisodeGroup(string collectionID, CancellationToken cancellationToken = default)
        => UseClient((client, token) => client.GetTvEpisodeGroupsAsync(collectionID, cancellationToken: token), $"Get episode group {collectionID}", cancellationToken);

    /// <summary>
    ///   The seasons and episodes of a show TMDb recorded changes to since a
    ///   time, for a refresh that leaves the rest alone.
    /// </summary>
    /// <param name="showID">The TMDb show ID.</param>
    /// <param name="since">When the show was last refreshed, in UTC.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>
    ///   What changed, or <see langword="null"/> when the time is outside the
    ///   changes window or the window is off, so everything is fetched.
    /// </returns>
    public async Task<TmdbShowChanges?> GetShowChanges(int showID, DateTime since, CancellationToken cancellationToken = default)
    {
        if (!WithinChangesWindow(since))
            return null;

        var changes = await UseClient(
            (client, token) => client.GetTvShowChangesAsync(showID, 0, since, null, token),
            $"Get changes of show {showID}",
            cancellationToken
        ).ConfigureAwait(false);
        return changes is null ? null : TmdbShowChanges.Parse(changes);
    }

    #endregion

    #region Movies & Collections

    /// <summary>
    ///   Gets a movie, with whatever is appended to it.
    /// </summary>
    /// <param name="movieID">The TMDb movie ID.</param>
    /// <param name="methods">What to append.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The movie, or <see langword="null"/> when TMDb has none.</returns>
    public Task<Movie?> GetMovie(int movieID, MovieMethods methods, CancellationToken cancellationToken = default)
        => UseClient((client, token) => client.GetMovieAsync(movieID, "en-US", null, methods, token), $"Get movie {movieID}", cancellationToken);

    /// <summary>
    ///   Whether TMDb recorded a change to a movie since a time, or can no
    ///   longer say because the time is outside the changes window.
    /// </summary>
    /// <param name="movieID">The TMDb movie ID.</param>
    /// <param name="since">When the movie was last refreshed, in UTC.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns><see langword="true"/> when the movie changed or has to be fetched anyway.</returns>
    public async Task<bool> HasMovieChanged(int movieID, DateTime since, CancellationToken cancellationToken = default)
    {
        if (!WithinChangesWindow(since))
            return true;

        var changes = await UseClient(
            (client, token) => client.GetMovieChangesAsync(movieID, 0, since, null, token),
            $"Get changes of movie {movieID}",
            cancellationToken
        ).ConfigureAwait(false);
        return changes is null || changes.Count > 0;
    }

    /// <summary>
    ///   Gets a collection, with its parts and translations.
    /// </summary>
    /// <param name="collectionID">The TMDb collection ID.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The collection, or <see langword="null"/> when TMDb has none.</returns>
    public Task<Collection?> GetCollection(int collectionID, CancellationToken cancellationToken = default)
        => UseClient(
            (client, token) => client.GetCollectionAsync(collectionID, CollectionMethods.Translations, token),
            $"Get collection {collectionID}",
            cancellationToken
        );

    #endregion

    #region People, Companies & Networks

    /// <summary>
    ///   Gets a person, with their translations, external IDs and photos.
    /// </summary>
    /// <param name="personID">The TMDb person ID.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The person, or <see langword="null"/> when TMDb has none.</returns>
    public Task<Person?> GetPerson(int personID, CancellationToken cancellationToken = default)
        => UseClient(
            (client, token) => client.GetPersonAsync(personID, PersonMethods.Translations | PersonMethods.ExternalIds | PersonMethods.Images, token),
            $"Get person {personID}",
            cancellationToken
        );

    /// <summary>
    ///   Gets a company.
    /// </summary>
    /// <param name="companyID">The TMDb company ID.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The company, or <see langword="null"/> when TMDb has none.</returns>
    public Task<Company?> GetCompany(int companyID, CancellationToken cancellationToken = default)
        => UseClient((client, token) => client.GetCompanyAsync(companyID, CompanyMethods.Undefined, token), $"Get company {companyID}", cancellationToken);

    /// <summary>
    ///   Gets a network.
    /// </summary>
    /// <param name="networkID">The TMDb network ID.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The network, or <see langword="null"/> when TMDb has none.</returns>
    public Task<Network?> GetNetwork(int networkID, CancellationToken cancellationToken = default)
        => UseClient((client, token) => client.GetNetworkAsync(networkID, token), $"Get network {networkID}", cancellationToken);

    #endregion

    #region Images

    /// <summary>
    ///   The images of a show.
    /// </summary>
    /// <param name="showID">The TMDb show ID.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The images, or <see langword="null"/> when TMDb has no such show.</returns>
    public Task<ImagesWithId?> GetShowImages(int showID, CancellationToken cancellationToken = default)
        => UseClient((client, token) => client.GetTvShowImagesAsync(showID, cancellationToken: token), $"Get images of show {showID}", cancellationToken);

    /// <summary>
    ///   The posters of a season.
    /// </summary>
    /// <param name="showID">The TMDb show ID.</param>
    /// <param name="seasonNumber">The season number.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The posters, or <see langword="null"/> when TMDb has no such season.</returns>
    public Task<PosterImages?> GetSeasonImages(int showID, int seasonNumber, CancellationToken cancellationToken = default)
        => UseClient(
            (client, token) => client.GetTvSeasonImagesAsync(showID, seasonNumber, cancellationToken: token),
            $"Get images of season {seasonNumber} of show {showID}",
            cancellationToken
        );

    /// <summary>
    ///   The stills of an episode.
    /// </summary>
    /// <param name="showID">The TMDb show ID.</param>
    /// <param name="seasonNumber">The season number.</param>
    /// <param name="episodeNumber">The episode number within the season.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The stills, or <see langword="null"/> when TMDb has no such episode.</returns>
    public Task<StillImages?> GetEpisodeImages(int showID, int seasonNumber, int episodeNumber, CancellationToken cancellationToken = default)
        => UseClient(
            (client, token) => client.GetTvEpisodeImagesAsync(showID, seasonNumber, episodeNumber, cancellationToken: token),
            $"Get images of episode {episodeNumber} of season {seasonNumber} of show {showID}",
            cancellationToken
        );

    /// <summary>
    ///   The images of a movie.
    /// </summary>
    /// <param name="movieID">The TMDb movie ID.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The images, or <see langword="null"/> when TMDb has no such movie.</returns>
    public Task<ImagesWithId?> GetMovieImages(int movieID, CancellationToken cancellationToken = default)
        => UseClient((client, token) => client.GetMovieImagesAsync(movieID, token), $"Get images of movie {movieID}", cancellationToken);

    /// <summary>
    ///   The images of a collection.
    /// </summary>
    /// <param name="collectionID">The TMDb collection ID.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The images, or <see langword="null"/> when TMDb has no such collection.</returns>
    public Task<ImagesWithId?> GetCollectionImages(int collectionID, CancellationToken cancellationToken = default)
        => UseClient((client, token) => client.GetCollectionImagesAsync(collectionID, cancellationToken: token), $"Get images of collection {collectionID}", cancellationToken);

    /// <summary>
    ///   The photos of a person.
    /// </summary>
    /// <param name="personID">The TMDb person ID.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The photos, or <see langword="null"/> when TMDb has no such person.</returns>
    public Task<ProfileImages?> GetPersonImages(int personID, CancellationToken cancellationToken = default)
        => UseClient((client, token) => client.GetPersonImagesAsync(personID, token), $"Get images of person {personID}", cancellationToken);

    /// <summary>
    ///   The logos of a network.
    /// </summary>
    /// <param name="networkID">The TMDb network ID.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The logos, or <see langword="null"/> when TMDb has no such network.</returns>
    public Task<NetworkLogos?> GetNetworkImages(int networkID, CancellationToken cancellationToken = default)
        => UseClient((client, token) => client.GetNetworkImagesAsync(networkID, token), $"Get images of network {networkID}", cancellationToken);

    /// <summary>
    ///   The base URL of TMDb's image server, asked of TMDb once, and the
    ///   default one when TMDb cannot be asked.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The base URL, ending in a slash.</returns>
    public async Task<string> GetImageServerUrl(CancellationToken cancellationToken = default)
    {
        if (_imageServerUrl is { } known)
            return known;

        try
        {
            var configuration = await UseClient((client, token) => client.GetAPIConfiguration(token), "Get the API configuration", cancellationToken).ConfigureAwait(false);
            if (configuration?.Images?.SecureBaseUrl is { Length: > 0 } url)
                return _imageServerUrl = url.EndsWith('/') ? url : url + "/";
        }
        catch (Exception ex) when (ex is MetadataProviderNotConfiguredException || IsTransient(ex))
        {
            _logger.LogDebug(ex, "Unable to ask TMDb for its image server; using the default one.");
        }

        return DefaultImageServerUrl;
    }

    #endregion

    #region Search

    /// <summary>
    ///   One page of TMDb's show search.
    /// </summary>
    /// <param name="query">What to search for.</param>
    /// <param name="page">The page, from one.</param>
    /// <param name="includeRestricted">Whether to include adult shows.</param>
    /// <param name="year">The year the show first aired, or <c>0</c> for any.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The page.</returns>
    /// <exception cref="HttpRequestException">TMDb answered nothing.</exception>
    public async Task<SearchContainer<SearchTv>> SearchShows(string query, int page, bool includeRestricted, int year, CancellationToken cancellationToken = default)
        => await UseClient(
            (client, token) => client.SearchTvShowAsync(query, page, includeRestricted, year, token),
            $"Search{(includeRestricted ? " all" : string.Empty)} shows for \"{query}\"{(year > 0 ? $" in {year}" : string.Empty)}, page {page}",
            cancellationToken
        ).ConfigureAwait(false) ?? throw new HttpRequestException(HttpRequestError.ConnectionError, "TMDb answered no search results.");

    /// <summary>
    ///   One page of TMDb's movie search.
    /// </summary>
    /// <param name="query">What to search for.</param>
    /// <param name="page">The page, from one.</param>
    /// <param name="includeRestricted">Whether to include adult movies.</param>
    /// <param name="year">The year the movie was released, or <c>0</c> for any.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The page.</returns>
    /// <exception cref="HttpRequestException">TMDb answered nothing.</exception>
    public async Task<SearchContainer<SearchMovie>> SearchMovies(string query, int page, bool includeRestricted, int year, CancellationToken cancellationToken = default)
        => await UseClient(
            (client, token) => client.SearchMovieAsync(query, page, includeRestricted, year, cancellationToken: token),
            $"Search{(includeRestricted ? " all" : string.Empty)} movies for \"{query}\"{(year > 0 ? $" in {year}" : string.Empty)}, page {page}",
            cancellationToken
        ).ConfigureAwait(false) ?? throw new HttpRequestException(HttpRequestError.ConnectionError, "TMDb answered no search results.");

    /// <summary>
    ///   The TMDb entries an IMDb title is.
    /// </summary>
    /// <param name="imdbID">The IMDb title ID, e.g. <c>tt0000001</c>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What TMDb found, or <see langword="null"/>.</returns>
    public Task<FindContainer?> FindByImdbID(string imdbID, CancellationToken cancellationToken = default)
        => UseClient((client, token) => client.FindAsync(FindExternalSource.Imdb, imdbID, token), $"Find the TMDb entries of IMDb title {imdbID}", cancellationToken);

    #endregion

    #region Genres

    /// <summary>
    ///   TMDb's show genres.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The genres.</returns>
    public async Task<IReadOnlyList<Genre>> GetShowGenres(CancellationToken cancellationToken = default)
        => await UseClient((client, token) => client.GetTvGenresAsync(token), "Get the show genres", cancellationToken).ConfigureAwait(false) ?? [];

    /// <summary>
    ///   TMDb's movie genres.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The genres.</returns>
    public async Task<IReadOnlyList<Genre>> GetMovieGenres(CancellationToken cancellationToken = default)
        => await UseClient((client, token) => client.GetMovieGenresAsync(token), "Get the movie genres", cancellationToken).ConfigureAwait(false) ?? [];

    #endregion

    #region Helpers

    /// <summary>
    ///   Whether TMDb's changes still cover a time: the window is on, and the
    ///   time falls within it.
    /// </summary>
    /// <param name="since">The time, in UTC.</param>
    /// <returns><see langword="true"/> when the changes since then can be asked for.</returns>
    private bool WithinChangesWindow(DateTime since)
    {
        var days = _configurationProvider.Load().IncrementalChangesWindowDays;
        return days > 0 && DateTime.SpecifyKind(since, DateTimeKind.Utc) >= TimeProvider.GetUtcNow().UtcDateTime.AddDays(-days);
    }

    /// <summary>
    ///   Stops listening for configuration changes and disposes of the
    ///   rate limiter and TMDb's client.
    /// </summary>
    public void Dispose()
    {
        _configurationProvider.Saved -= OnConfigurationSaved;
        RateLimiter.Dispose();
        _bulkhead.Dispose();
        lock (_clientLock)
            _client?.Dispose();
    }

    #endregion
}
