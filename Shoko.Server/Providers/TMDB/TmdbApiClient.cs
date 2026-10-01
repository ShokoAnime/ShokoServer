using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Polly;
using Polly.Bulkhead;
using Polly.Retry;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using TMDbLib.Client;
using TMDbLib.Objects.Changes;
using TMDbLib.Objects.Exceptions;

namespace Shoko.Server.Providers.TMDB;

/// <summary>
///   Makes every call to TMDB, through the rate limiter, a bulkhead and the
///   retry policy, and keeps what TMDB answers the same for everybody: its
///   genres and its image server.
/// </summary>
/// <remarks>
///   Only the TMDB provider and its helpers talk to TMDB, and all of them
///   through this, so the rate limiter sees every request.
/// </remarks>
public class TmdbApiClient
{
    #region Fields

    /// <summary>
    ///   How many requests may be in flight at once.
    /// </summary>
    internal static readonly int MaxConcurrency = Math.Min(6, Environment.ProcessorCount);

    private static TmdbApiClient? _instance;

    private static readonly Lock _instanceLock = new();

    private static string? _imageServerUrl;

    private static readonly Lock _imageServerUrlLock = new();

    private readonly ILogger<TmdbApiClient> _logger;

    private readonly ISettingsProvider _settingsProvider;

    private readonly TmdbRateLimiter _rateLimiter;

    private readonly KeyedEntityLockHelper _entityLock;

    private readonly AsyncBulkheadPolicy _bulkheadPolicy;

    // Retries rate limits (10 times) and timeouts (3 times). No delay here: the pausing
    // happens in TmdbRateLimiter.EnsureRateAsync.
    private readonly AsyncRetryPolicy _retryPolicy;

    private TMDbClient? _rawClient;

    private IReadOnlyDictionary<int, string>? _movieGenres;

    private IReadOnlyDictionary<int, string>? _showGenres;

    #endregion

    #region Constructors

    /// <summary>
    ///   Sets up the policies around TMDB's client, which itself is made on
    ///   first use so the server is set up before the API key is read.
    /// </summary>
    /// <param name="logger">Where the calls are logged.</param>
    /// <param name="settingsProvider">Holds the user's API key and the changes window.</param>
    /// <param name="rateLimiter">Paces the requests and pauses them on server errors.</param>
    public TmdbApiClient(ILogger<TmdbApiClient> logger, ISettingsProvider settingsProvider, TmdbRateLimiter rateLimiter)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _rateLimiter = rateLimiter;
        _entityLock = new(logger);
        _bulkheadPolicy = Policy.BulkheadAsync(MaxConcurrency, int.MaxValue);
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<RequestLimitExceededException>()
            .Or<GeneralHttpException>()
            .Or<Exception>(IsTmdb5xx)
            .WaitAndRetryAsync(int.MaxValue, (_, _) => TimeSpan.Zero, OnTmdbRetryAsync);
        _instance ??= this;
    }

    #endregion

    #region Instance

    /// <summary>
    ///   The client, for the few places that predate dependency injection.
    /// </summary>
    internal static TmdbApiClient? Instance
    {
        get
        {
            if (_instance is not null)
                return _instance;

            lock (_instanceLock)
            {
                if (_instance is not null)
                    return _instance;

                if (!ISystemService.HasStaticServices)
                    return null;

#pragma warning disable CS0618
                return _instance = ISystemService.StaticServices.GetService<TmdbApiClient>();
#pragma warning restore CS0618
            }
        }
    }

    /// <summary>
    ///   The base URL of TMDB's image server, asked of TMDB once.
    /// </summary>
    /// <exception cref="InvalidOperationException">The client is not set up yet.</exception>
    public static string ImageServerUrl
    {
        get
        {
            if (_imageServerUrl is not null)
                return _imageServerUrl;

            lock (_imageServerUrlLock)
            {
                if (_imageServerUrl is not null)
                    return _imageServerUrl;

                var instance = Instance ?? throw new InvalidOperationException("TmdbApiClient not initialized yet.");
                try
                {
                    var config = instance.UseClient(c => c.GetAPIConfiguration(), "Get API configuration").GetAwaiter().GetResult() ??
                        throw new HttpRequestException(HttpRequestError.ConnectionError, "Failed to get API configuration");
                    return _imageServerUrl = config.Images!.SecureBaseUrl!;
                }
                catch (Exception ex)
                {
                    // Without an API key or a connection, the default image server still resolves image URLs.
                    if (FallbackImageServerUrl(ex) is { } fallback)
                        return _imageServerUrl = fallback;

                    instance._logger.LogError(ex, "Encountered an exception while trying to find the image server url to use; {ErrorMessage}", ex.Message);
                    throw;
                }
            }
        }
    }

    /// <summary>
    ///   The image server to use when TMDB could not be asked for its own.
    /// </summary>
    /// <param name="ex">Why TMDB could not be asked, unwrapped from a single-failure <see cref="AggregateException"/>.</param>
    /// <returns>
    ///   The default image server for a missing API key or an unreachable
    ///   TMDB, or <see langword="null"/> for any other failure.
    /// </returns>
    internal static string? FallbackImageServerUrl(Exception ex)
    {
        switch (ex)
        {
            case AggregateException { InnerExceptions: [var inner] }:
                return FallbackImageServerUrl(inner);
            case TmdbApiKeyUnavailableException:
            case HttpRequestException { HttpRequestError: HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError or HttpRequestError.SecureConnectionError }:
            {
                // If you can't be arsed to look it up yourself on their site, then here, waste more time than it's worth by decoding and reversing this string. No matter how you do it, it will be more effort compared to looking it up on their dev
                // site. And while you're there, go get yourself a personal API key to use. ;)
                char[] url = ['\x2f', '\x70', '\x2f', '\x74', '\x2f', '\x67', '\x72', '\x6f', '\x2e', '\x62', '\x64', '\x6d', '\x74', '\x2e', '\x65', '\x67', '\x61', '\x6d', '\x69', '\x2f', '\x2f', '\x3a', '\x73', '\x70', '\x74', '\x74', '\x68'];
                return new string(url.Reverse().ToArray());
            }
            default:
                return null;
        }
    }

    #endregion

    #region Client

    /// <summary>
    ///   Whether an API key is available, the user's or one built in.
    /// </summary>
    internal bool HasApiKey => _settingsProvider.GetSettings().TMDB.UserApiKey is { Length: > 0 } || HasBuiltInApiKey;

    /// <summary>
    ///   Whether the build carries an API key of its own.
    /// </summary>
    private static bool HasBuiltInApiKey => Constants.TMDB.ApiKey != "TMDB_API_KEY_GOES_HERE";

    // Created on first use, so the server is set up first. An empty user key
    // (cleared in the settings) is no key.
    private TMDbClient CachedClient => _rawClient ??= new(_settingsProvider.GetSettings().TMDB.UserApiKey is { Length: > 0 } userApiKey ? userApiKey : (
        Constants.TMDB.ApiKey != "TMDB_API_KEY_GOES_HERE"
#pragma warning disable CS0162 // Unreachable code detected
            ? Constants.TMDB.ApiKey
#pragma warning restore CS0162 // Unreachable code detected
            : throw new TmdbApiKeyUnavailableException()
    ));

    /// <summary>
    ///   Whether a failure is one a later attempt may get past: a network
    ///   error or TMDB's rate limit.
    /// </summary>
    /// <remarks>
    ///   A 404, a bad API key and unexpected status codes are left out, so a
    ///   caller's clean-up still runs for what TMDB no longer has.
    /// </remarks>
    /// <param name="ex">The failure.</param>
    /// <returns><see langword="true"/> for a failure worth retrying later.</returns>
    internal static bool IsTmdbTransient(Exception ex) =>
        ex is HttpRequestException or RequestLimitExceededException || IsTmdb5xx(ex);

    private static bool IsTmdb5xx(Exception ex) => ex is TMDbServerException or TMDbServiceUnavailableException;

    /// <summary>
    ///   Calls TMDB through the rate limiter, the bulkhead and the retry
    ///   policy.
    /// </summary>
    /// <typeparam name="T">The type of the result of the function.</typeparam>
    /// <param name="func">The function to execute with the TMDb client.</param>
    /// <param name="displayName">The name of the function to display in the logs.</param>
    /// <returns>A task that will complete with the result of the function, after applying the rate limiting and retry policies.</returns>
    /// <exception cref="TmdbApiKeyUnavailableException">No API key is available.</exception>
    public async Task<T?> UseClient<T>(Func<TMDbClient, Task<T>> func, string? displayName)
    {
        displayName ??= func.Method.Name;
        var now = DateTime.Now;
        var attempts = 0;
        var waitTime = TimeSpan.Zero;
        try
        {
            _logger.LogTrace("Scheduled call: {DisplayName}", displayName);
            var val = await _bulkheadPolicy.ExecuteAsync(() =>
            {
                var now1 = DateTime.Now;
                waitTime = now1 - now;
                now = now1;
                _logger.LogTrace("Executing call: {DisplayName} (Waited {Waited}ms)", displayName, waitTime.TotalMilliseconds);

                return _retryPolicy.ExecuteAsync(() =>
                {
                    ++attempts;
                    return _rateLimiter.EnsureRateAsync(() => func(CachedClient));
                });
            }).ConfigureAwait(false);

            var delta = DateTime.Now - now;
            _logger.LogTrace("Completed call: {DisplayName} (Waited {Waited}ms, Executed: {Delta}ms, {Attempts} attempts)", displayName, waitTime.TotalMilliseconds, delta.TotalMilliseconds, attempts);
            _rateLimiter.NotifySuccess();
            return val;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TmdbApiKeyUnavailableException ex)
        {
            _logger.LogWarning("Failed call:    {DisplayName} ({Reason})", displayName, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            var delta = DateTime.Now - now;
            _logger.LogError(ex, "Failed call:    {DisplayName} (Waited {Waited}ms, Executed: {Delta}ms, {Attempts} attempts)", displayName, waitTime.TotalMilliseconds, delta.TotalMilliseconds, attempts);
            throw;
        }
    }

    /// <summary>
    ///   Decides whether a failed call is tried again.
    /// </summary>
    /// <param name="ex">The failure.</param>
    /// <param name="ts">The delay before the next attempt.</param>
    /// <param name="retryCount">How many attempts were made.</param>
    /// <param name="ctx">The retry's context, which counts the attempts per kind of failure.</param>
    /// <returns>A task that completes once the retry may go ahead.</returns>
    /// <exception cref="Exception">The failure, when it is not retried.</exception>
    private Task OnTmdbRetryAsync(Exception ex, TimeSpan ts, int retryCount, Context ctx)
    {
        switch (ex)
        {
            // The rate limiter's WaitForBackoffAsync does the delay on the next acquire.
            case RequestLimitExceededException rleEx:
            {
                var rlRetryCount = ctx.TryGetValue("rateLimitRetryCount", out var rlVal) ? (int)rlVal : 0;
                if (rlRetryCount >= 10)
                    throw ex;
                ctx["rateLimitRetryCount"] = rlRetryCount + 1;
                var retryAfter = rleEx.RetryAfter ?? TimeSpan.FromSeconds(1);
                _logger.LogTrace("Hit remote rate limit. Waiting and retrying. Retry count: {RetryCount}, Retry after: {RetryAfter}", retryCount, retryAfter);
                _rateLimiter.NotifyRateLimitExceeded(retryAfter);
                break;
            }
            case HttpRequestException hrEx when hrEx.InnerException is TaskCanceledException:
            {
                var timeoutRetryCount = ctx.TryGetValue("timeoutRetryCount", out var timeoutRetryCountValue) ? (int)timeoutRetryCountValue : 0;
                if (timeoutRetryCount >= 3)
                    throw ex;
                ctx["timeoutRetryCount"] = timeoutRetryCount + 1;
                break;
            }
            // TMDbLib master maps recognised 5xx responses to these types instead of GeneralHttpException.
            case TMDbServerException or TMDbServiceUnavailableException:
                _logger.LogWarning(ex, "Got a server-side error from TMDb: {Message}", ex.Message);
                _rateLimiter.Notify5xxError();
                throw ex;
            case GeneralHttpException ghEx:
                _logger.LogWarning(ghEx, "Got a general HTTP exception while processing TMDb request: {StatusCode}", (int)ghEx.HttpStatusCode);
                if ((int)ghEx.HttpStatusCode >= 500)
                    _rateLimiter.Notify5xxError();
                throw ex;
            default:
                throw ex;
        }
        return Task.CompletedTask;
    }

    #endregion

    #region Genres

    /// <summary>
    ///   TMDB's movie genres, asked for once.
    /// </summary>
    /// <returns>The genres' names by their IDs.</returns>
    public async Task<IReadOnlyDictionary<int, string>> GetMovieGenres()
    {
        if (_movieGenres is not null)
            return _movieGenres;

        using (await _entityLock.GetLockForEntityAsync(MetadataEntityType.Movie, 0, "genre", "Load").ConfigureAwait(false))
        {
            if (_movieGenres is not null)
                return _movieGenres;

            var genres = await UseClient(c => c.GetMovieGenresAsync(), "Get Movie Genres").ConfigureAwait(false);
            if (genres is null)
                return new Dictionary<int, string>();

            return _movieGenres = genres.ToDictionary(x => x.Id, x => x.Name!);
        }
    }

    /// <summary>
    ///   TMDB's show genres, asked for once.
    /// </summary>
    /// <returns>The genres' names by their IDs.</returns>
    public async Task<IReadOnlyDictionary<int, string>> GetShowGenres()
    {
        if (_showGenres is not null)
            return _showGenres;

        using (await _entityLock.GetLockForEntityAsync(MetadataEntityType.Series, 0, "genre", "Load").ConfigureAwait(false))
        {
            if (_showGenres is not null)
                return _showGenres;

            var genres = await UseClient(c => c.GetTvGenresAsync(), "Get TV Show Genres").ConfigureAwait(false);
            if (genres is null)
                return new Dictionary<int, string>();

            return _showGenres = genres.ToDictionary(x => x.Id, x => x.Name!);
        }
    }

    #endregion

    #region Changes

    /// <summary>
    ///   Whether TMDB recorded any change to a movie since a time, or can no
    ///   longer say because the time is outside its changes window.
    /// </summary>
    /// <param name="movieId">The TMDB movie ID.</param>
    /// <param name="since">The time, in local time.</param>
    /// <returns><see langword="true"/> when the movie changed or a full refresh is needed.</returns>
    internal async Task<bool> HasMovieChangedSinceAsync(int movieId, DateTime since)
    {
        var changesWindowDays = _settingsProvider.GetSettings().TMDB.IncrementalChangesWindowDays;
        if (changesWindowDays is 0)
            return true;

        // The Changes API has no history past its window, so anything older counts as changed.
        var sinceUtc = since.ToUniversalTime();
        if (sinceUtc < DateTime.UtcNow.AddDays(-changesWindowDays))
            return true;
        var changes = await UseClient(c => c.GetMovieChangesAsync(movieId, 0, sinceUtc, null), $"Get changes for movie {movieId}").ConfigureAwait(false);
        return changes is null || changes.Count > 0;
    }

    /// <summary>
    ///   The seasons and episodes of a show TMDB recorded changes to since a
    ///   time, for a refresh that leaves the rest alone.
    /// </summary>
    /// <param name="showId">The TMDB show ID.</param>
    /// <param name="since">The time, in local time.</param>
    /// <returns>
    ///   <para><see langword="null"/> if the Changes API window has been exceeded or the API
    ///   call failed; the caller should then refresh all seasons and episodes.</para>
    ///   <para>Empty sets if no season or episode changes were reported; the caller can skip the update.</para>
    ///   <para>Populated sets containing the season numbers and (season, episode) pairs that changed.</para>
    /// </returns>
    internal async Task<TmdbShowChangedItems?> GetShowChangedItemsAsync(int showId, DateTime since)
    {
        var changesWindowDays = _settingsProvider.GetSettings().TMDB.IncrementalChangesWindowDays;
        if (changesWindowDays is 0)
            return null;

        // The Changes API has no history past its window, so the caller refreshes everything.
        var sinceUtc = since.ToUniversalTime();
        if (sinceUtc < DateTime.UtcNow.AddDays(-changesWindowDays))
            return null;
        var changes = await UseClient(c => c.GetTvShowChangesAsync(showId, 0, sinceUtc, null), $"Get changes for show {showId}").ConfigureAwait(false);
        if (changes is null)
            return null;
        return ParseShowChanges(changes);
    }

    /// <summary>
    ///   Reads a TMDB Changes API response into the season numbers and the
    ///   (season, episode) pairs that changed.
    /// </summary>
    /// <remarks>
    ///   Only the <c>"episode"</c> and <c>"season"</c> keys are read. An episode
    ///   change also marks its season, so the season is refreshed with it.
    /// </remarks>
    /// <param name="changes">The changes TMDB reported.</param>
    /// <returns>What changed.</returns>
    internal static TmdbShowChangedItems ParseShowChanges(IList<Change> changes)
    {
        var seasonNumbers = new HashSet<int>();
        var episodes = new HashSet<(int Season, int Episode)>();
        foreach (var change in changes)
        {
            if (change.Key is not ("episode" or "season"))
                continue;
            foreach (var item in change.Items ?? [])
                ApplyChangeItem(change.Key, item, seasonNumbers, episodes);
        }
        return new TmdbShowChangedItems(seasonNumbers, episodes);
    }

    /// <summary>
    ///   Adds what one change item names to the changed seasons and episodes.
    /// </summary>
    /// <param name="key">The change's key, <c>"episode"</c> or <c>"season"</c>.</param>
    /// <param name="item">The change item.</param>
    /// <param name="seasonNumbers">The changed season numbers.</param>
    /// <param name="episodes">The changed episodes.</param>
    private static void ApplyChangeItem(string key, ChangeItemBase item, HashSet<int> seasonNumbers, HashSet<(int Season, int Episode)> episodes)
    {
        var value = item switch
        {
            ChangeItemAdded added => added.Value as JObject,
            ChangeItemUpdated updated => updated.Value as JObject,
            ChangeItemDestroyed destroyed => destroyed.Value as JObject,
            ChangeItemDeleted deleted => deleted.OriginalValue as JObject, // deleted items carry the previous value
            _ => null,
        };
        if (value is null)
            return;

        var seasonNumber = value["season_number"]?.Value<int>();
        if (!seasonNumber.HasValue)
            return;

        seasonNumbers.Add(seasonNumber.Value);

        // Lets the refresh skip the season's episodes that did not change themselves.
        if (key != "episode")
            return;

        var episodeNumber = value["episode_number"]?.Value<int>();
        if (episodeNumber.HasValue)
            episodes.Add((seasonNumber.Value, episodeNumber.Value));
    }

    #endregion
}

/// <summary>
///   The seasons and episodes of a show TMDB recorded changes to.
/// </summary>
/// <param name="SeasonNumbers">The numbers of the seasons that changed.</param>
/// <param name="Episodes">The season and episode numbers of the episodes that changed.</param>
internal readonly record struct TmdbShowChangedItems(HashSet<int> SeasonNumbers, HashSet<(int Season, int Episode)> Episodes);
