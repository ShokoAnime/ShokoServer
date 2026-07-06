using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Polly;
using Polly.Bulkhead;
using Polly.Retry;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Metadata.Tmdb.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Repositories.Direct.TMDB;
using Shoko.Server.Repositories.Direct.TMDB.Optional;
using Shoko.Server.Repositories.Direct.TMDB.Text;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using TMDbLib.Client;
using TMDbLib.Objects.Changes;
using TMDbLib.Objects.Collections;
using TMDbLib.Objects.Exceptions;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.People;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.TvShows;

using MovieCredits = TMDbLib.Objects.Movies.Credits;
using TitleLanguage = Shoko.Abstractions.Metadata.Enums.TitleLanguage;

#pragma warning disable CS0618
// Suggestions we don't need in this file.
#pragma warning disable CA1822
#pragma warning disable CA1826

namespace Shoko.Server.Providers.TMDB;

public class TmdbMetadataService : ITmdbMetadataService
{
    private static readonly int _maxConcurrency = Math.Min(6, Environment.ProcessorCount);

    private static TmdbMetadataService? _instance = null;

    private static readonly object _instanceLockObj = new();

    internal static TmdbMetadataService? Instance
    {
        get
        {
            if (_instance is not null)
                return _instance;

            lock (_instanceLockObj)
            {
                if (_instance is not null)
                    return _instance;

                if (!ISystemService.HasStaticServices)
                    return null;

                return _instance = ISystemService.StaticServices.GetService<TmdbMetadataService>();
            }
        }
    }

    private static string? _imageServerUrl = null;

    private static readonly object _imageServerUrlLockObj = new();

    public static string ImageServerUrl
    {
        get
        {
            // Return cached version if possible.
            if (_imageServerUrl is not null)
                return _imageServerUrl;

            // Lock before getting the config, in case multiple threads are trying to get the image server url at the same time.
            lock (_imageServerUrlLockObj)
            {
                // Try one more time, in case it has been initialized while we were waiting.
                if (_imageServerUrl is not null)
                    return _imageServerUrl;

                // In case the server url is attempted to be accessed before the lazily initialized instance has been created, create it now if the service container is available, otherwise abort.
                var instance = Instance;
                if (instance is null)
                    throw new InvalidOperationException("TmdbMetadataService not initialized yet.");

                try
                {
                    var config = instance.UseClient(c => c.GetAPIConfiguration(), "Get API configuration").Result ??
                        throw new HttpRequestException(HttpRequestError.ConnectionError, "Failed to get API configuration");
                    return _imageServerUrl = config.Images!.SecureBaseUrl!;
                }
                catch (Exception ex)
                {
                    // If the API key is unavailable or if we can't establish a connection to the api server, then use the default image server url if we ever need to resolve the image URLs for whatever reason.
                    if (ex is TmdbApiKeyUnavailableException || (ex is HttpRequestException httpEx && httpEx.HttpRequestError is HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError or HttpRequestError.SecureConnectionError))
                    {
                        // If you can't be arsed to look it up yourself on their site, then here, waste more time than it's worth by decoding and reversing this string. No matter how you do it, it will be more effort compared to looking it up on their dev
                        // site. And while you're there, go get yourself a personal API key to use. ;)
                        char[] url = ['\x2f', '\x70', '\x2f', '\x74', '\x2f', '\x67', '\x72', '\x6f', '\x2e', '\x64', '\x62', '\x6d', '\x74', '\x2e', '\x65', '\x67', '\x61', '\x6d', '\x69', '\x2f', '\x2f', '\x3a', '\x73', '\x70', '\x74', '\x74', '\x68'];
                        return _imageServerUrl = new string(url.Reverse().ToArray());
                    }
                    instance._logger.LogError(ex, "Encountered an exception while trying to find the image server url to use; {ErrorMessage}", ex.Message);
                    throw;
                }
            }
        }
    }

    private readonly ILogger<TmdbMetadataService> _logger;

    private readonly IQueueScheduler _scheduler;

    private readonly ISettingsProvider _settingsProvider;

    private readonly TmdbImageService _imageService;

    private readonly TmdbLinkingService _linkingService;

    private readonly AnimeSeriesRepository _animeSeries;

    private readonly ShokoImage_EntityRepository _shokoImageXrefRepository;

    private readonly TMDB_AlternateOrderingRepository _tmdbAlternateOrdering;

    private readonly TMDB_AlternateOrdering_EpisodeRepository _tmdbAlternateOrderingEpisodes;

    private readonly TMDB_AlternateOrdering_SeasonRepository _tmdbAlternateOrderingSeasons;

    private readonly TMDB_CollectionRepository _tmdbCollections;

    private readonly TMDB_CompanyRepository _tmdbCompany;

    private readonly TMDB_EpisodeRepository _tmdbEpisodes;

    private readonly TMDB_Episode_CastRepository _tmdbEpisodeCast;

    private readonly TMDB_Episode_CrewRepository _tmdbEpisodeCrew;

    private readonly TMDB_MovieRepository _tmdbMovies;

    private readonly TMDB_Movie_CastRepository _tmdbMovieCast;

    private readonly TMDB_Movie_CrewRepository _tmdbMovieCrew;

    private readonly TMDB_NetworkRepository _tmdbNetwork;

    private readonly TMDB_OverviewRepository _tmdbOverview;

    private readonly TMDB_PersonRepository _tmdbPeople;

    private readonly TMDB_SeasonRepository _tmdbSeasons;

    private readonly TMDB_ShowRepository _tmdbShows;

    private readonly TMDB_TitleRepository _tmdbTitle;

    private readonly CrossRef_AniDB_TMDB_MovieRepository _xrefAnidbTmdbMovies;

    private readonly CrossRef_AniDB_TMDB_ShowRepository _xrefAnidbTmdbShows;

    private readonly TMDB_Collection_MovieRepository _xrefTmdbCollectionMovies;

    private readonly TMDB_Company_EntityRepository _xrefTmdbCompanyEntity;

    private readonly TMDB_Show_NetworkRepository _xrefTmdbShowNetwork;

    private TMDbClient? _rawClient = null;

    // We lazy-init it on first use, this will give us time to set up the server before we attempt to init the tmdb client.
    private TMDbClient CachedClient => _rawClient ??= new(_settingsProvider.GetSettings().TMDB.UserApiKey ?? (
        Constants.TMDB.ApiKey != "TMDB_API_KEY_GOES_HERE"
            ? Constants.TMDB.ApiKey
            : throw new TmdbApiKeyUnavailableException()
    ));

    // This policy will ensure only 10 requests can be in-flight at the same time.
    private readonly AsyncBulkheadPolicy _bulkheadPolicy;

    private readonly TmdbRateLimiter _rateLimiter;

    public TmdbRateLimitPauseStatus GetPauseStatus()
    {
        var (isPaused, remaining) = _rateLimiter.GetPauseSnapshot();
        return new() { IsPaused = isPaused, RemainingPauseTime = remaining };
    }

    // Retries only on RequestLimitExceededException (cap: 10) and HttpRequestException timeouts (cap: 3).
    // GeneralHttpException and all other types re-throw immediately in OnTmdbRetryAsync.
    // Zero delay is intentional — actual rate-limit pausing happens inside TmdbRateLimiter.EnsureRateAsync.
    private readonly AsyncRetryPolicy _retryPolicy;

    private readonly KeyedEntityLockHelper _entityLock;

    /// <summary>
    /// Execute the given function with the TMDb client, applying rate limiting and retry policies.
    /// </summary>
    private Task OnTmdbRetryAsync(Exception ex, TimeSpan ts, int retryCount, Context ctx)
    {
        switch (ex)
        {
            // If we got a _remote_ rate limit exception, notify the rate limiter.
            // The rate limiter's WaitForBackoffAsync handles the actual delay on the next acquire.
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
            // If we timed out, wait and try again (up to 3 times).
            case HttpRequestException hrEx when hrEx.InnerException is TaskCanceledException:
            {
                var timeoutRetryCount = ctx.TryGetValue("timeoutRetryCount", out var timeoutRetryCountValue) ? (int)timeoutRetryCountValue : 0;
                if (timeoutRetryCount >= 3)
                    throw ex;
                ctx["timeoutRetryCount"] = timeoutRetryCount + 1;
                break;
            }
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

    // Network exhaustion and rate-limit exhaustion are transient — the person exists on TMDB but
    // couldn't be fetched this run. NotFoundException (404), bad API key, and unexpected HTTP
    // status codes are excluded so the caller's cleanup path still fires for genuine orphans.
    internal static bool IsTmdbTransient(Exception ex) =>
        ex is HttpRequestException or RequestLimitExceededException;

    /// <typeparam name="T">The type of the result of the function.</typeparam>
    /// <param name="func">The function to execute with the TMDb client.</param>
    /// <param name="displayName">The name of the function to display in the logs.</param>
    /// <returns>A task that will complete with the result of the function, after applying the rate limiting and retry policies.</returns>
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
        catch (Exception ex)
        {
            var delta = DateTime.Now - now;
            _logger.LogError(ex, "Failed call:    {DisplayName} (Waited {Waited}ms, Executed: {Delta}ms, {Attempts} attempts)", displayName, waitTime.TotalMilliseconds, delta.TotalMilliseconds, attempts);
            throw;
        }
    }

    public TmdbMetadataService(
        ILogger<TmdbMetadataService> logger,
        IQueueScheduler scheduler,
        ISettingsProvider settingsProvider,
        TmdbRateLimiter rateLimiter,
        TmdbImageService imageService,
        TmdbLinkingService linkingService,
        AnimeSeriesRepository animeSeries,
        ShokoImage_EntityRepository shokoImageXrefRepository,
        TMDB_AlternateOrderingRepository tmdbAlternateOrdering,
        TMDB_AlternateOrdering_EpisodeRepository tmdbAlternateOrderingEpisodes,
        TMDB_AlternateOrdering_SeasonRepository tmdbAlternateOrderingSeasons,
        TMDB_CollectionRepository tmdbCollections,
        TMDB_CompanyRepository tmdbCompany,
        TMDB_EpisodeRepository tmdbEpisodes,
        TMDB_Episode_CastRepository tmdbEpisodeCast,
        TMDB_Episode_CrewRepository tmdbEpisodeCrew,
        TMDB_MovieRepository tmdbMovies,
        TMDB_Movie_CastRepository tmdbMovieCast,
        TMDB_Movie_CrewRepository tmdbMovieCrew,
        TMDB_NetworkRepository tmdbNetwork,
        TMDB_OverviewRepository tmdbOverview,
        TMDB_PersonRepository tmdbPeople,
        TMDB_SeasonRepository tmdbSeasons,
        TMDB_ShowRepository tmdbShows,
        TMDB_TitleRepository tmdbTitle,
        CrossRef_AniDB_TMDB_MovieRepository xrefAnidbTmdbMovies,
        CrossRef_AniDB_TMDB_ShowRepository xrefAnidbTmdbShows,
        TMDB_Collection_MovieRepository xrefTmdbCollectionMovies,
        TMDB_Company_EntityRepository xrefTmdbCompanyEntity,
        TMDB_Show_NetworkRepository xrefTmdbShowNetwork
    )
    {
        _logger = logger;
        _scheduler = scheduler;
        _settingsProvider = settingsProvider;
        _imageService = imageService;
        _linkingService = linkingService;
        _animeSeries = animeSeries;
        _shokoImageXrefRepository = shokoImageXrefRepository;
        _tmdbAlternateOrdering = tmdbAlternateOrdering;
        _tmdbAlternateOrderingEpisodes = tmdbAlternateOrderingEpisodes;
        _tmdbAlternateOrderingSeasons = tmdbAlternateOrderingSeasons;
        _tmdbCollections = tmdbCollections;
        _tmdbCompany = tmdbCompany;
        _tmdbEpisodes = tmdbEpisodes;
        _tmdbEpisodeCast = tmdbEpisodeCast;
        _tmdbEpisodeCrew = tmdbEpisodeCrew;
        _tmdbMovies = tmdbMovies;
        _tmdbMovieCast = tmdbMovieCast;
        _tmdbMovieCrew = tmdbMovieCrew;
        _tmdbNetwork = tmdbNetwork;
        _tmdbOverview = tmdbOverview;
        _tmdbPeople = tmdbPeople;
        _tmdbSeasons = tmdbSeasons;
        _tmdbShows = tmdbShows;
        _tmdbTitle = tmdbTitle;
        _xrefAnidbTmdbMovies = xrefAnidbTmdbMovies;
        _xrefAnidbTmdbShows = xrefAnidbTmdbShows;
        _xrefTmdbCollectionMovies = xrefTmdbCollectionMovies;
        _xrefTmdbCompanyEntity = xrefTmdbCompanyEntity;
        _xrefTmdbShowNetwork = xrefTmdbShowNetwork;
        _entityLock = new(logger);
        _rateLimiter = rateLimiter;
        _instance ??= this;
        _bulkheadPolicy = Policy.BulkheadAsync(_maxConcurrency, int.MaxValue);
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<RequestLimitExceededException>()
            .Or<GeneralHttpException>()
            .WaitAndRetryAsync(int.MaxValue, (_, _) => TimeSpan.Zero, OnTmdbRetryAsync);
    }

    public async Task ScheduleSearchForMatch(int anidbId, bool force)
    {
        await _scheduler.StartJob<SearchTmdbJob>(c =>
        {
            c.AnimeID = anidbId;
            c.ForceRefresh = force;
        });
    }

    public async Task ScanForMatches()
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.TMDB.AutoLink)
            return;

        var allSeries = _animeSeries.GetAll();
        foreach (var ser in allSeries)
        {
            if (ser.IsTmdbAutoMatchingDisabled)
                continue;

            if (ser.AniDB_Anime is not { } anime)
                continue;

            if (anime.IsRestricted && !settings.TMDB.AutoLinkRestricted)
                continue;

            if (anime.TmdbMovieCrossReferences is { Count: > 0 })
                continue;

            if (anime.TmdbShowCrossReferences is { Count: > 0 })
                continue;

            _logger.LogTrace("Found anime without TMDB association: {MainTitle}", anime.MainTitle);

            await _scheduler.StartJob<SearchTmdbJob>(c => c.AnimeID = ser.AniDB_ID);
        }
    }

    #region Movies

    #region Genres (Movies)

    private IReadOnlyDictionary<int, string>? _movieGenres = null;

    public async Task<IReadOnlyDictionary<int, string>> GetMovieGenres()
    {
        if (_movieGenres is not null)
            return _movieGenres;

        using (await GetLockForEntity(DataEntityType.Movie, 0, "genre", "Load").ConfigureAwait(false))
        {
            if (_movieGenres is not null)
                return _movieGenres;

            var genres = await UseClient(c => c.GetMovieGenresAsync(), "Get Movie Genres").ConfigureAwait(false);
            if (genres is null)
                return new Dictionary<int, string>();

            _movieGenres = genres.ToDictionary(x => x.Id, x => x.Name!);
            return _movieGenres;
        }
    }

    #endregion

    #region Update (Movies)

    public bool IsMovieUpdating(int movieId)
        => IsEntityLocked(DataEntityType.Movie, movieId, "metadata");

    public bool WaitForMovieUpdate(int movieId)
        => WaitIfEntityLocked(DataEntityType.Movie, movieId, "metadata");

    public Task<bool> WaitForMovieUpdateAsync(int movieId)
        => WaitIfEntityLockedAsync(DataEntityType.Movie, movieId, "metadata");

    public bool IsMovieCollectionUpdating(int collectionId)
        => IsEntityLocked(DataEntityType.Collection, collectionId, "metadata");

    public bool WaitForMovieCollectionUpdate(int collectionId)
        => WaitIfEntityLocked(DataEntityType.Collection, collectionId, "metadata");

    public Task<bool> WaitForMovieCollectionUpdateAsync(int collectionId)
        => WaitIfEntityLockedAsync(DataEntityType.Collection, collectionId, "metadata");

    public async Task UpdateAllMovies(bool force, bool saveImages)
    {
        var allXRefs = _xrefAnidbTmdbMovies.GetAll();
        _logger.LogInformation("Scheduling {Count} movies to be updated.", allXRefs.Count);
        foreach (var xref in allXRefs)
        {
            if (xref.AnimeSeries is null)
                continue;

            await _scheduler.StartJob<UpdateTmdbMovieJob>(
                c =>
                {
                    c.TmdbMovieID = xref.TmdbMovieID;
                    c.ForceRefresh = force;
                    c.DownloadImages = saveImages;
                }
            ).ConfigureAwait(false);
        }
    }

    public async Task ScheduleUpdateOfMovie(TmdbMovieUpdateOptions options)
    {
        // Schedule the movie info to be downloaded or updated.
        await _scheduler.RunAfterCurrent<UpdateTmdbMovieJob>(c =>
        {
            c.TmdbMovieID = options.MovieId;
            c.ForceRefresh = options.ForceRefresh;
            c.DownloadImages = options.DownloadImages;
            c.DownloadCrewAndCast = options.DownloadCrewAndCast;
            c.DownloadCollections = options.DownloadCollections;
        }).ConfigureAwait(false);
    }

    public async Task<bool> UpdateMovie(TmdbMovieUpdateOptions options)
    {
        var movieId = options.MovieId;
        var forceRefresh = options.ForceRefresh;
        var downloadImages = options.DownloadImages;
        var settings = _settingsProvider.GetSettings();
        var downloadCrewAndCast = options.DownloadCrewAndCast ?? settings.TMDB.AutoDownloadCrewAndCast;
        var downloadCollections = options.DownloadCollections ?? settings.TMDB.AutoDownloadCollections;

        using (await GetLockForEntity(DataEntityType.Movie, movieId, "metadata", "Update").ConfigureAwait(false))
        {
            // Abort if we're within a certain time frame as to not try and get us rate-limited.
            var tmdbMovie = _tmdbMovies.GetByTmdbMovieID(movieId) ?? new(movieId);
            var newlyAdded = tmdbMovie.TMDB_MovieID == 0;
            if (!forceRefresh && tmdbMovie.CreatedAt != tmdbMovie.LastUpdatedAt && tmdbMovie.LastUpdatedAt > DateTime.Now.AddHours(-1))
            {
                _logger.LogInformation("Skipping update of movie {MovieID} as it was last updated {LastUpdatedAt}", movieId, tmdbMovie.LastUpdatedAt);
                return false;
            }

            if (!forceRefresh && !newlyAdded)
            {
                bool hasChanged;
                try
                {
                    hasChanged = await HasMovieChangedSinceAsync(movieId, tmdbMovie.LastUpdatedAt).ConfigureAwait(false);
                }
                catch (Exception ex) when (IsTmdbTransient(ex))
                {
                    _logger.LogWarning(ex, "TMDB: Transient error checking changes for movie {MovieId}; proceeding with full refresh.", movieId);
                    hasChanged = true;
                }
                if (!hasChanged)
                {
                    _logger.LogInformation("Skipping update of movie {MovieID} as no changes were detected on TMDB since {LastUpdatedAt}.", movieId, tmdbMovie.LastUpdatedAt);
                    return false;
                }
            }

            // Abort if we couldn't find the movie by id.
            var methods = MovieMethods.Translations | MovieMethods.ReleaseDates | MovieMethods.ExternalIds | MovieMethods.Keywords;
            if (downloadCrewAndCast)
                methods |= MovieMethods.Credits;
            var movie = await UseClient(c => c.GetMovieAsync(movieId, "en-US", null, methods), $"Get movie {movieId}").ConfigureAwait(false);
            if (movie is null)
                return false;

            var preferredTitleLanguages = settings.TMDB.DownloadAllTitles ? null : Languages.PreferredNamingLanguages.Select(a => a.Language).ToHashSet();
            var preferredOverviewLanguages = settings.TMDB.DownloadAllOverviews ? null : Languages.PreferredDescriptionNamingLanguages.Select(a => a.Language).ToHashSet();
            var contentRatingLanguages = settings.TMDB.DownloadAllContentRatings
                ? null
                : Languages.PreferredNamingLanguages.Select(a => a.Language)
                    .Concat(Languages.PreferredEpisodeNamingLanguages.Select(a => a.Language))
                    .Except([TitleLanguage.Main, TitleLanguage.Unknown, TitleLanguage.None])
                    .ToHashSet();
            var updated = tmdbMovie.Populate(movie, contentRatingLanguages);
            var (titlesUpdated, overviewsUpdated) = UpdateTitlesAndOverviewsWithTuple(tmdbMovie, movie.Translations, preferredTitleLanguages, preferredOverviewLanguages);
            updated = titlesUpdated || overviewsUpdated || updated;
            updated = UpdateMovieExternalIDs(tmdbMovie, movie.ExternalIds!) || updated;
            updated = await UpdateCompanies(tmdbMovie, movie.ProductionCompanies!) || updated;
            if (downloadCrewAndCast)
                updated = await UpdateMovieCastAndCrew(tmdbMovie, movie.Credits!, forceRefresh, downloadImages) || updated;
            if (updated)
            {
                tmdbMovie.LastUpdatedAt = DateTime.Now;
                _tmdbMovies.Save(tmdbMovie);
            }

            if (downloadCollections)
                await UpdateMovieCollections(movie);

            foreach (var xref in _xrefAnidbTmdbMovies.GetByTmdbMovieID(movieId))
            {
                if ((titlesUpdated || overviewsUpdated) && xref.AnimeSeries is { } series)
                {
                    if (titlesUpdated)
                    {
                        series.ResetPreferredTitle();
                        series.ResetAnimeTitles();
                    }

                    if (overviewsUpdated)
                        series.ResetPreferredOverview();
                }
            }

            if (downloadImages)
                await DownloadMovieImages(movieId, tmdbMovie.OriginalLanguage);

            if (newlyAdded || updated)
                ShokoEventHandler.Instance.OnMovieUpdated(tmdbMovie, newlyAdded ? UpdateReason.Added : UpdateReason.Updated);

            return updated;
        }
    }

    private async Task<bool> UpdateMovieCastAndCrew(TMDB_Movie tmdbMovie, MovieCredits credits, bool forceRefresh, bool downloadImages)
    {
        var peopleToKeep = new HashSet<int>();

        var counter = 0;
        var castToAdd = 0;
        var castToKeep = new HashSet<string>();
        var castToSave = new List<TMDB_Movie_Cast>();
        var existingCastDict = _tmdbMovieCast.GetByTmdbMovieID(tmdbMovie.Id)
            .ToDictionary(cast => cast.TmdbCreditID);
        foreach (var cast in credits.Cast!)
        {
            var ordering = counter++;
            peopleToKeep.Add(cast.Id);
            castToKeep.Add(cast.CreditId!);

            var roleUpdated = false;
            if (!existingCastDict.TryGetValue(cast.CreditId!, out var role))
            {
                role = new()
                {
                    TmdbMovieID = tmdbMovie.Id,
                    TmdbPersonID = cast.Id,
                    TmdbCreditID = cast.CreditId!,
                };
                castToAdd++;
                roleUpdated = true;
            }

            var characterName = cast.Character!.Replace(" (voice)", "");
            if (role.CharacterName != characterName)
            {
                role.CharacterName = characterName;
                roleUpdated = true;
            }

            if (role.Ordering != ordering)
            {
                role.Ordering = ordering;
                roleUpdated = true;
            }

            if (roleUpdated)
            {
                castToSave.Add(role);
            }
        }

        var crewToAdd = 0;
        var crewToKeep = new HashSet<string>();
        var crewToSave = new List<TMDB_Movie_Crew>();
        var existingCrewDict = _tmdbMovieCrew.GetByTmdbMovieID(tmdbMovie.Id)
            .ToDictionary(crew => crew.TmdbCreditID);
        foreach (var crew in credits.Crew!)
        {
            peopleToKeep.Add(crew.Id);
            crewToKeep.Add(crew.CreditId!);

            var roleUpdated = false;
            if (!existingCrewDict.TryGetValue(crew.CreditId!, out var role))
            {
                role = new()
                {
                    TmdbMovieID = tmdbMovie.Id,
                    TmdbPersonID = crew.Id,
                    TmdbCreditID = crew.CreditId!,
                };
                crewToAdd++;
                roleUpdated = true;
            }

            if (role.Department != crew.Department)
            {
                role.Department = crew.Department!;
                roleUpdated = true;
            }

            if (role.Job != crew.Job)
            {
                role.Job = crew.Job!;
                roleUpdated = true;
            }

            if (roleUpdated)
            {
                crewToSave.Add(role);
            }
        }

        var castToRemove = existingCastDict.Values
            .ExceptBy(castToKeep, cast => cast.TmdbCreditID)
            .ToList();
        var crewToRemove = existingCrewDict.Values
            .ExceptBy(crewToKeep, crew => crew.TmdbCreditID)
            .ToList();

        _tmdbMovieCast.Save(castToSave);
        _tmdbMovieCrew.Save(crewToSave);
        _tmdbMovieCast.Delete(castToRemove);
        _tmdbMovieCrew.Delete(crewToRemove);

        _logger.LogDebug(
            "Added/updated/removed/skipped {aa}/{au}/{ar}/{as} cast and {ra}/{ru}/{rr}/{rs} crew  for movie {MovieTitle} (Movie={MovieId})",
            castToAdd,
            castToSave.Count - castToAdd,
            castToRemove.Count,
            existingCastDict.Count - (castToSave.Count - castToAdd),
            crewToAdd,
            crewToSave.Count - crewToAdd,
            crewToRemove.Count,
            existingCrewDict.Count - (crewToSave.Count - crewToAdd),
            tmdbMovie.EnglishTitle,
            tmdbMovie.Id
            );

        // Only add/remove staff if we're not doing a quick refresh.
        var peopleAdded = 0;
        var peopleUpdated = 0;
        var peoplePurged = 0;
        var peopleToPurge = existingCastDict.Values.Select(cast => cast.TmdbPersonID)
            .Concat(existingCrewDict.Values.Select(crew => crew.TmdbPersonID))
            .Except(peopleToKeep)
            .ToHashSet();
        var transientlyFailedMoviePeople = new ConcurrentBag<int>();
        await ProcessWithConcurrencyAsync(_maxConcurrency, peopleToKeep, async personId =>
        {
            try
            {
                var (added, updated) = await UpdatePerson(personId, forceRefresh, downloadImages, currentMovieId: tmdbMovie.Id);
                if (added)
                    Interlocked.Increment(ref peopleAdded);
                else if (updated)
                    Interlocked.Increment(ref peopleUpdated);
            }
            catch (Exception ex) when (IsTmdbTransient(ex))
            {
                // Transient failure — preserve cast/crew rows and retry later.
                transientlyFailedMoviePeople.Add(personId);
            }
            catch (Exception ex)
            {
                // Non-transient failure — log and let CleanupOrphanedCastCrew handle it.
                _logger.LogWarning(ex, "TMDB: Unexpected error updating person {PersonId} for movie {MovieId}", personId, tmdbMovie.Id);
            }
        }, onDropped: transientlyFailedMoviePeople.Add);
        // Schedule retries for transiently-failed people; their cast/crew rows are preserved.
        var transientlyFailedMovieSet = transientlyFailedMoviePeople.ToHashSet();
        if (transientlyFailedMovieSet.Count > 0)
            await Task.WhenAll(transientlyFailedMovieSet.Select(personId =>
                _scheduler.Enqueue<UpdateTmdbPersonJob>(j => { j.TmdbPersonID = personId; j.DownloadImages = downloadImages; j.TmdbMovieID = tmdbMovie.Id; })));
        // Remove cast/crew for people that permanently failed — transient failures are excluded.
        CleanupOrphanedCastCrew(peopleToKeep, transientlyFailedMovieSet, missingPersonIds =>
        {
            var orphanedCast = _tmdbMovieCast.GetByTmdbMovieID(tmdbMovie.Id)
                .Where(c => missingPersonIds.Contains(c.TmdbPersonID)).ToList();
            var orphanedCrew = _tmdbMovieCrew.GetByTmdbMovieID(tmdbMovie.Id)
                .Where(c => missingPersonIds.Contains(c.TmdbPersonID)).ToList();
            if (orphanedCast.Count > 0 || orphanedCrew.Count > 0)
            {
                _logger.LogWarning("TMDB: Removed {CastCount} cast and {CrewCount} crew entries for {PersonCount} people that failed to fetch. (Movie={MovieId})",
                    orphanedCast.Count, orphanedCrew.Count, missingPersonIds.Count, tmdbMovie.Id);
                _tmdbMovieCast.Delete(orphanedCast);
                _tmdbMovieCrew.Delete(orphanedCrew);
            }
        });
        try
        {
            await ProcessWithConcurrencyAsync(_maxConcurrency, peopleToPurge, async personId =>
            {
                if (await PurgePerson(personId))
                    Interlocked.Increment(ref peoplePurged);
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TMDB: Failed to purge one or more people during movie cast/crew update (Movie={MovieId})", tmdbMovie.Id);
        }

        _logger.LogDebug("Added/removed {a}/{u}/{r}/{s} staff for movie {MovieTitle} (Movie={MovieId})",
            peopleAdded,
            peopleUpdated,
            peoplePurged,
            peopleToKeep.Count + peopleToPurge.Count - peopleAdded - peopleUpdated - peoplePurged,
            tmdbMovie.EnglishTitle,
            tmdbMovie.Id
        );
        return castToSave.Count > 0 ||
            castToRemove.Count > 0 ||
            crewToSave.Count > 0 ||
            crewToRemove.Count > 0 ||
            peopleAdded > 0 ||
            peopleUpdated > 0 ||
            peoplePurged > 0;
    }

    private async Task UpdateMovieCollections(Movie movie)
    {
        if (movie.BelongsToCollection?.Id is not { } collectionId)
        {
            await CleanupMovieCollection(movie.Id);
            return;
        }

        var collection = await UseClient(c => c.GetCollectionAsync(collectionId, CollectionMethods.Images | CollectionMethods.Translations), $"Get movie collection {collectionId} for movie {movie.Id} \"{movie.Title}\"").ConfigureAwait(false);
        if (collection is null)
        {
            await PurgeMovieCollection(collectionId);
            return;
        }

        using (await GetLockForEntity(DataEntityType.Collection, collection.Id, "metadata", "Update").ConfigureAwait(false))
        {
            var settings = _settingsProvider.GetSettings();
            var preferredTitleLanguages = settings.TMDB.DownloadAllTitles ? null : Languages.PreferredNamingLanguages.Select(a => a.Language).ToHashSet();
            var preferredOverviewLanguages = settings.TMDB.DownloadAllOverviews ? null : Languages.PreferredDescriptionNamingLanguages.Select(a => a.Language).ToHashSet();

            var tmdbCollection = _tmdbCollections.GetByTmdbCollectionID(collectionId) ?? new(collectionId);
            var updated = tmdbCollection.Populate(collection);
            updated = UpdateTitlesAndOverviews(tmdbCollection, collection.Translations, preferredTitleLanguages, preferredOverviewLanguages) || updated;

            var xrefsToAdd = 0;
            var xrefsToSave = new List<TMDB_Collection_Movie>();
            var movieXRefs = _xrefTmdbCollectionMovies.GetByTmdbCollectionID(collectionId);
            var xrefsToRemove = movieXRefs.Where(xref => !collection.Parts!.Any(part => xref.TmdbMovieID == part.Id)).ToList();
            var movieXref = movieXRefs.FirstOrDefault(xref => xref.TmdbMovieID == movie.Id);
            var index = collection.Parts!.FindIndex(part => part.Id == movie.Id);
            if (index == -1)
                index = collection.Parts.Count;
            if (movieXref is null)
            {
                xrefsToAdd++;
                xrefsToSave.Add(new(collectionId, movie.Id, index + 1));
            }
            else if (movieXref.Ordering != index + 1)
            {
                movieXref.Ordering = index + 1;
                xrefsToSave.Add(movieXref);
            }

            _logger.LogDebug(
                "Added/updated/removed/skipped {ta}/{tu}/{tr}/{ts} movie cross-references for movie collection {CollectionTitle} (Id={CollectionId})",
                xrefsToAdd,
                xrefsToSave.Count - xrefsToAdd,
                xrefsToRemove.Count,
                movieXRefs.Count + xrefsToAdd - xrefsToRemove.Count - xrefsToSave.Count,
                tmdbCollection.EnglishTitle,
                tmdbCollection.Id);
            _xrefTmdbCollectionMovies.Save(xrefsToSave);
            _xrefTmdbCollectionMovies.Delete(xrefsToRemove);

            if (updated || xrefsToSave.Count > 0 || xrefsToRemove.Count > 0)
            {
                tmdbCollection.LastUpdatedAt = DateTime.Now;
                _tmdbCollections.Save(tmdbCollection);
            }
        }

    }

    public async Task ScheduleDownloadAllMovieImages(int movieId, bool forceDownload = false)
    {
        // Schedule the movie info to be downloaded or updated.
        await _scheduler.StartJob<DownloadTmdbMovieImagesJob>(c =>
        {
            c.TmdbMovieID = movieId;
            c.ForceDownload = forceDownload;
        }).ConfigureAwait(false);
    }

    public async Task DownloadAllMovieImages(int movieId, bool forceDownload = false)
    {
        using (await GetLockForEntity(DataEntityType.Movie, movieId, "images", "Update").ConfigureAwait(false))
        {
            var tmdbMovie = _tmdbMovies.GetByTmdbMovieID(movieId);
            if (tmdbMovie is null)
                return;

            await DownloadMovieImages(movieId, tmdbMovie.OriginalLanguage, forceDownload);
        }
    }

    private async Task DownloadMovieImages(int movieId, TitleLanguage? mainLanguage = null, bool forceDownload = false)
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.TMDB.AutoDownloadPosters && !settings.TMDB.AutoDownloadLogos && !settings.TMDB.AutoDownloadBackdrops)
            return;

        if (_tmdbMovies.GetByTmdbMovieID(movieId) is not { } movie)
            return;

        _logger.LogTrace("Downloading images for movie {MovieName} (Movie={MovieID})", movie.OriginalTitle, movie.Id);

        var images = await UseClient(c => c.GetMovieImagesAsync(movieId), $"Get images for movie {movieId}").ConfigureAwait(false);
        if (images is null)
            return;

        var languages = GetLanguages(mainLanguage);
        if (settings.TMDB.AutoDownloadPosters)
            await _imageService.DownloadImagesByType(movie.PosterPath, images.Posters!, ImageEntityType.Primary, movie, settings.TMDB.MaxAutoPosters, languages, forceDownload);
        if (settings.TMDB.AutoDownloadLogos)
            await _imageService.DownloadImagesByType(null, images.Logos!, ImageEntityType.Logo, movie, settings.TMDB.MaxAutoLogos, languages, forceDownload);
        if (settings.TMDB.AutoDownloadBackdrops)
            await _imageService.DownloadImagesByType(movie.BackdropPath, images.Backdrops!, ImageEntityType.Backdrop, movie, settings.TMDB.MaxAutoBackdrops, languages, forceDownload);
    }

    #endregion

    #region Purge (Movies)

    public async Task PurgeAllUnusedMovies(DateTime? olderThan = null)
    {
        var toKeep = _xrefAnidbTmdbMovies.GetAll().Select(xref => xref.TmdbMovieID).ToHashSet();
        // Seeded with toKeep so AllCount in the log includes xref-linked IDs that have no TMDB_Movie row yet.
        var allMovies = new HashSet<int>(toKeep);
        allMovies.UnionWith(_tmdbMovies.GetAll().Select(m => m.TmdbMovieID));
        allMovies.UnionWith(_shokoImageXrefRepository.GetByEntity(DataSource.TMDB, DataEntityType.Movie).Select(xref => int.Parse(xref.EntityID)));
        allMovies.UnionWith(_xrefTmdbCompanyEntity.GetAll().Where(x => x.TmdbEntityType is DataEntityType.Movie).Select(x => x.TmdbEntityID));
        allMovies.UnionWith(_tmdbMovieCast.GetAll().Select(x => x.TmdbMovieID));
        allMovies.UnionWith(_tmdbMovieCrew.GetAll().Select(x => x.TmdbMovieID));
        allMovies.UnionWith(_xrefTmdbCollectionMovies.GetAll().Select(x => x.TmdbMovieID));
        var toBePurged = allMovies.Except(toKeep).ToHashSet();

        if (olderThan.HasValue)
            toBePurged = toBePurged
                .Where(id => _tmdbMovies.GetByTmdbMovieID(id) is not { LastUpdatedAt: var t } || t < olderThan.Value)
                .ToHashSet();

        _logger.LogInformation("Scheduling {Count} out of {AllCount} movies to be purged.", toBePurged.Count, allMovies.Count);
        foreach (var movieID in toBePurged)
            await _scheduler.StartJob<PurgeTmdbMovieJob>(c => c.TmdbMovieID = movieID);
    }

    public async Task SchedulePurgeOfMovie(int movieId)
    {
        await _scheduler.StartJob<PurgeTmdbMovieJob>(c => c.TmdbMovieID = movieId).ConfigureAwait(false);
    }

    /// <summary>
    /// Purge a TMDB movie from the local database.
    /// </summary>
    /// <param name="movieId">TMDB Movie ID.</param>
    public async Task PurgeMovie(int movieId)
    {
        using (await GetLockForEntity(DataEntityType.Movie, movieId, "metadata", "Purge").ConfigureAwait(false))
        {
            await _linkingService.RemoveAllMovieLinksForMovie(movieId);

            var movie = _tmdbMovies.GetByTmdbMovieID(movieId);
            if (movie is not null)
            {
                _logger.LogTrace("Removing movie {MovieName} (Movie={MovieID})", movie.OriginalTitle, movie.Id);
                _tmdbMovies.Delete(movie);
            }

            _imageService.PurgeImages(movie ?? new() { TmdbMovieID = movieId });

            PurgeMovieCompanies(movieId);

            await PurgeMovieCastAndCrew(movieId);

            await CleanupMovieCollection(movieId);

            PurgeTitlesAndOverviews(DataEntityType.Movie, movieId);
        }
    }

    private void PurgeMovieCompanies(int movieId)
    {
        var xrefsToRemove = _xrefTmdbCompanyEntity.GetByTmdbEntityTypeAndID(DataEntityType.Movie, movieId);
        foreach (var xref in xrefsToRemove)
        {
            // Delete xref or purge company.
            var xrefs = _xrefTmdbCompanyEntity.GetByTmdbCompanyID(xref.TmdbCompanyID);
            if (xrefs.Count > 1)
                _xrefTmdbCompanyEntity.Delete(xref);
            else
                PurgeCompany(xref.TmdbCompanyID);
        }
    }

    private async Task PurgeMovieCastAndCrew(int movieId)
    {
        var castMembers = _tmdbMovieCast.GetByTmdbMovieID(movieId);
        var crewMembers = _tmdbMovieCrew.GetByTmdbMovieID(movieId);

        _tmdbMovieCast.Delete(castMembers);
        _tmdbMovieCrew.Delete(crewMembers);

        var allPeopleSet = castMembers.Select(c => c.TmdbPersonID)
            .Concat(crewMembers.Select(c => c.TmdbPersonID))
            .Distinct()
            .ToHashSet();
        foreach (var personId in allPeopleSet)
            await PurgePerson(personId);
    }

    private async Task CleanupMovieCollection(int movieId)
    {
        var xref = _xrefTmdbCollectionMovies.GetByTmdbMovieID(movieId);
        if (xref is null)
            return;

        var allXRefs = _xrefTmdbCollectionMovies.GetByTmdbCollectionID(xref.TmdbCollectionID);
        if (allXRefs.Count > 1)
            _xrefTmdbCollectionMovies.Delete(xref);
        else
            await PurgeMovieCollection(xref.TmdbCollectionID);
    }

    private async Task PurgeMovieCollection(int collectionId)
    {
        using (await GetLockForEntity(DataEntityType.Collection, collectionId, "metadata", "Update").ConfigureAwait(false))
        {
            var collection = _tmdbCollections.GetByTmdbCollectionID(collectionId);
            var collectionXRefs = _xrefTmdbCollectionMovies.GetByTmdbCollectionID(collectionId);
            if (collectionXRefs.Count > 0)
            {
                _logger.LogTrace(
                    "Removing {Count} cross-references for movie collection {CollectionName} (Collection={CollectionID})",
                    collectionXRefs.Count, collection?.EnglishTitle ?? string.Empty,
                    collectionId
                );
                _xrefTmdbCollectionMovies.Delete(collectionXRefs);
            }

            _imageService.PurgeImages(collection ?? new() { TmdbCollectionID = collectionId });

            PurgeTitlesAndOverviews(DataEntityType.Collection, collectionId);

            if (collection is not null)
            {
                _logger.LogTrace(
                    "Removing movie collection {CollectionName} (Collection={CollectionID})",
                    collection.EnglishTitle,
                    collectionId
                );
                _tmdbCollections.Delete(collection);
            }
        }
    }

    public async Task PurgeAllMovieCollections()
    {
        var collections = _tmdbCollections.GetAll();
        var collectionXRefs = _xrefTmdbCollectionMovies.GetAll();
        var collectionIDs = new HashSet<int>([
            ..collections.Select(x => x.TmdbCollectionID),
            ..collectionXRefs.Select(x => x.TmdbCollectionID),
        ]);

        _logger.LogInformation("Removing {Count} movie collections to be purged.", collectionIDs.Count);

        foreach (var collectionID in collectionIDs)
            await PurgeMovieCollection(collectionID);
    }

    #endregion

    #endregion

    #region Shows

    #region Genres (Shows)

    private IReadOnlyDictionary<int, string>? _tvShowGenres = null;

    public async Task<IReadOnlyDictionary<int, string>> GetShowGenres()
    {
        if (_tvShowGenres is not null)
            return _tvShowGenres;

        using (await GetLockForEntity(DataEntityType.Show, 0, "genre", "Load").ConfigureAwait(false))
        {
            if (_tvShowGenres is not null)
                return _tvShowGenres;

            var genres = await UseClient(c => c.GetTvGenresAsync(), "Get TV Show Genres").ConfigureAwait(false);
            if (genres is null)
                return new Dictionary<int, string>();

            _tvShowGenres = genres.ToDictionary(x => x.Id, x => x.Name!);
            return _tvShowGenres;
        }
    }

    #endregion

    #region Update (Shows)

    public async Task UpdateAllShows(bool force = false, bool downloadImages = false)
    {
        var allXRefs = _xrefAnidbTmdbShows.GetAll();
        _logger.LogInformation("Scheduling {Count} shows to be updated.", allXRefs.Count);
        foreach (var xref in allXRefs)
        {
            if (xref.TmdbShowID is 0)
                continue;

            if (xref.AnimeSeries is null)
                continue;

            await _scheduler.StartJob<UpdateTmdbShowJob>(
                c =>
                {
                    c.TmdbShowID = xref.TmdbShowID;
                    c.ForceRefresh = force;
                    c.DownloadImages = downloadImages;
                }
            ).ConfigureAwait(false);
        }
    }

    public bool IsShowUpdating(int showId)
        => IsEntityLocked(DataEntityType.Show, showId, "metadata");

    public bool WaitForShowUpdate(int showId)
        => WaitIfEntityLocked(DataEntityType.Show, showId, "metadata");

    public Task<bool> WaitForShowUpdateAsync(int showId)
        => WaitIfEntityLockedAsync(DataEntityType.Show, showId, "metadata");

    public async Task ScheduleUpdateOfShow(TmdbShowUpdateOptions options)
    {
        if (options.ShowId is 0)
            return;

        // Schedule the show info to be downloaded or updated.
        await _scheduler.RunAfterCurrent<UpdateTmdbShowJob>(c =>
        {
            c.TmdbShowID = options.ShowId;
            c.ForceRefresh = options.ForceRefresh;
            c.DownloadImages = options.DownloadImages;
            c.DownloadCrewAndCast = options.DownloadCrewAndCast;
            c.DownloadAlternateOrdering = options.DownloadAlternateOrdering;
            c.DownloadNetworks = options.DownloadNetworks;
        }).ConfigureAwait(false);
    }

    public async Task<bool> UpdateShow(TmdbShowUpdateOptions options)
    {
        var showId = options.ShowId;
        var forceRefresh = options.ForceRefresh;
        var downloadImages = options.DownloadImages;
        var quickRefresh = options.QuickRefresh;
        var settings = _settingsProvider.GetSettings();
        var downloadCrewAndCast = options.DownloadCrewAndCast ?? settings.TMDB.AutoDownloadCrewAndCast;
        var downloadAlternateOrdering = options.DownloadAlternateOrdering ?? settings.TMDB.AutoDownloadAlternateOrdering;
        var downloadNetworks = options.DownloadNetworks ?? settings.TMDB.AutoDownloadNetworks;

        if (showId is 0)
            return false;

        using (await GetLockForEntity(DataEntityType.Show, showId, "metadata", "Update").ConfigureAwait(false))
        {
            // Abort if we're within a certain time frame as to not try and get us rate-limited.
            var tmdbShow = _tmdbShows.GetByTmdbShowID(showId) ?? new(showId);
            var newlyAdded = tmdbShow.CreatedAt == tmdbShow.LastUpdatedAt;
            var xrefs = _xrefAnidbTmdbShows.GetByTmdbShowID(showId);
            if (!forceRefresh && tmdbShow.CreatedAt != tmdbShow.LastUpdatedAt && tmdbShow.LastUpdatedAt > DateTime.Now.AddHours(-1))
            {
                _logger.LogInformation("Skipping update of show {ShowID} as it was last updated {LastUpdatedAt}", showId, tmdbShow.LastUpdatedAt);

                // Do the auto-matching if we're not doing a quick refresh.
                if (!quickRefresh)
                    foreach (var xref in xrefs)
                        _linkingService.MatchAnidbToTmdbEpisodes(xref.AnidbAnimeID, xref.TmdbShowID, null, true, true);

                return false;
            }

            var methods = TvShowMethods.ContentRatings | TvShowMethods.Translations | TvShowMethods.ExternalIds | TvShowMethods.Keywords;
            if (downloadAlternateOrdering && !quickRefresh)
                methods |= TvShowMethods.EpisodeGroups;
            var show = await UseClient(c => c.GetTvShowAsync(showId, methods, "en-US"), $"Get Show {showId}").ConfigureAwait(false);
            if (show is null)
                return false;

            // Null result (14-day window exceeded or API failure) triggers a full refresh in UpdateShowSeasonsAndEpisodes.
            // Not applied on forced refreshes or newly-added shows — those always do a full fetch.
            TmdbShowChangedItems? changedItems = null;
            if (!newlyAdded && !forceRefresh && !quickRefresh)
            {
                try
                {
                    changedItems = await GetShowChangedItemsAsync(showId, tmdbShow.LastUpdatedAt).ConfigureAwait(false);
                }
                catch (Exception ex) when (IsTmdbTransient(ex))
                {
                    _logger.LogWarning(ex, "TMDB: Transient error checking changes for show {ShowId}; proceeding with full refresh.", showId);
                    changedItems = null;
                }
            }

            var preferredTitleLanguages = settings.TMDB.DownloadAllTitles ? null : Languages.PreferredNamingLanguages.Select(a => a.Language).ToHashSet();
            var preferredOverviewLanguages = settings.TMDB.DownloadAllOverviews ? null : Languages.PreferredDescriptionNamingLanguages.Select(a => a.Language).ToHashSet();
            var contentRatingLanguages = settings.TMDB.DownloadAllContentRatings
                ? null
                : Languages.PreferredNamingLanguages.Select(a => a.Language)
                    .Concat(Languages.PreferredEpisodeNamingLanguages.Select(a => a.Language))
                    .Except([TitleLanguage.Main, TitleLanguage.Unknown, TitleLanguage.None])
                    .ToHashSet();
            var shouldFireEvents = !quickRefresh || xrefs.Count > 0;
            var updated = tmdbShow.Populate(show, contentRatingLanguages);
            var (titlesUpdated, overviewsUpdated) = UpdateTitlesAndOverviewsWithTuple(tmdbShow, show.Translations, preferredTitleLanguages, preferredOverviewLanguages);
            updated = titlesUpdated || overviewsUpdated || updated;
            updated = UpdateShowExternalIDs(tmdbShow, show.ExternalIds!) || updated;
            updated = await UpdateCompanies(tmdbShow, show.ProductionCompanies!) || updated;
            var (episodesOrSeasonsUpdated, updatedSeasons, updatedEpisodes, episodeCount, hiddenEpisodeCount) = await UpdateShowSeasonsAndEpisodes(show, downloadCrewAndCast, forceRefresh, downloadImages, quickRefresh, shouldFireEvents, changedItems);
            updated = episodesOrSeasonsUpdated || updated;
            if (tmdbShow.EpisodeCount != episodeCount)
            {
                tmdbShow.EpisodeCount = episodeCount;
                updated = true;
            }
            if (tmdbShow.HiddenEpisodeCount != hiddenEpisodeCount)
            {
                tmdbShow.HiddenEpisodeCount = hiddenEpisodeCount;
                updated = true;
            }
            if (downloadAlternateOrdering && !quickRefresh)
                updated = await UpdateShowAlternateOrdering(tmdbShow, show) || updated;
            if (downloadNetworks && !quickRefresh)
                updated = await UpdateShowNetworks(tmdbShow, show) || updated;
            if (newlyAdded || updated)
            {
                if (shouldFireEvents)
                    tmdbShow.LastUpdatedAt = DateTime.Now;
                _tmdbShows.Save(tmdbShow);
            }

            foreach (var xref in xrefs)
            {
                // Don't do the auto-matching if we're just doing a quick refresh.
                if (!quickRefresh)
                    _linkingService.MatchAnidbToTmdbEpisodes(xref.AnidbAnimeID, xref.TmdbShowID, null, true, true);

                if ((titlesUpdated || overviewsUpdated) && xref.AnimeSeries is { } series)
                {
                    if (titlesUpdated)
                    {
                        series.ResetPreferredTitle();
                        series.ResetAnimeTitles();
                    }

                    if (overviewsUpdated)
                        series.ResetPreferredOverview();
                }
            }

            if (downloadImages && !quickRefresh)
                await ScheduleDownloadAllShowImages(showId, false);

            if (shouldFireEvents && (newlyAdded || updated || updatedEpisodes.Count > 0))
                ShokoEventHandler.Instance.OnSeriesUpdated(
                    tmdbShow,
                    newlyAdded ? UpdateReason.Added : UpdateReason.Updated,
                    updatedSeasons,
                    updatedEpisodes
                );

            return updated;
        }
    }

    private async Task<TmdbSeasonEpisodeUpdateResult> UpdateShowSeasonsAndEpisodes(TvShow show, bool downloadCrewAndCast = false, bool forceRefresh = false, bool downloadImages = false, bool quickRefresh = false, bool shouldFireEvents = false, TmdbShowChangedItems? changedItems = null)
    {
        var settings = _settingsProvider.GetSettings();
        var preferredTitleLanguages = settings.TMDB.DownloadAllTitles ? null : Languages.PreferredEpisodeNamingLanguages.Select(a => a.Language).ToHashSet();
        var preferredOverviewLanguages = settings.TMDB.DownloadAllOverviews ? null : Languages.PreferredDescriptionNamingLanguages.Select(a => a.Language).ToHashSet();

        var existingSeasons = _tmdbSeasons.GetByTmdbShowID(show.Id).ToDictionary(season => season.Id);
        var existingEpisodes = _tmdbEpisodes.GetByTmdbShowID(show.Id).ToDictionary(episode => episode.Id);
        var state = new ShowSyncState
        {
            DownloadCrewAndCast = downloadCrewAndCast,
            QuickRefresh = quickRefresh,
            ShouldFireEvents = shouldFireEvents,
            PreferredTitleLanguages = preferredTitleLanguages,
            PreferredOverviewLanguages = preferredOverviewLanguages,
            ChangedItems = changedItems,
        };
        var episodePeople = BuildEpisodePeopleLookup(show, state);

        foreach (var reducedSeason in show.Seasons!)
            await ProcessShowSeasonAsync(show, reducedSeason, existingSeasons, existingEpisodes, episodePeople, state).ConfigureAwait(false);

        var seasonsToRemove = existingSeasons.Values.ExceptBy(state.SeasonsToSkip, s => s.Id).ToList();
        var episodesToRemove = existingEpisodes.Values.ExceptBy(state.EpisodesToSkip, e => e.Id).ToList();

        _logger.LogDebug(
            "Added/updated/removed/skipped {Added}/{Updated}/{Removed}/{Skipped} seasons for show {ShowTitle} (Show={ShowId})",
            state.SeasonsAdded,
            state.SeasonsToSave.Count - state.SeasonsAdded,
            seasonsToRemove.Count,
            existingSeasons.Count + state.SeasonsAdded - seasonsToRemove.Count - state.SeasonsToSave.Count,
            show.Name,
            show.Id);
        _tmdbSeasons.Save(state.SeasonsToSave);

        foreach (var season in seasonsToRemove)
        {
            PurgeShowSeason(season);
            state.SeasonEvents.TryAdd(season, UpdateReason.Removed);
        }

        _tmdbSeasons.Delete(seasonsToRemove);

        _logger.LogDebug(
            "Added/updated/removed/skipped {Added}/{Updated}/{Removed}/{Skipped} episodes for show {ShowTitle} (Show={ShowId})",
            state.EpisodesAdded,
            state.EpisodesToSave.Count - state.EpisodesAdded,
            episodesToRemove.Count,
            existingEpisodes.Count + state.EpisodesAdded - episodesToRemove.Count - state.EpisodesToSave.Count,
            show.Name,
            show.Id);
        _tmdbEpisodes.Save(state.EpisodesToSave);

        foreach (var episode in episodesToRemove)
        {
            PurgeShowEpisode(episode);
            state.EpisodeEvents.TryAdd(episode, UpdateReason.Removed);
        }

        _tmdbEpisodes.Delete(episodesToRemove);

        if (quickRefresh)
            return new TmdbSeasonEpisodeUpdateResult(
                state.SeasonsToSave.Count > 0 || seasonsToRemove.Count > 0 || state.EpisodesToSave.Count > 0 || episodesToRemove.Count > 0,
                state.SeasonEvents,
                state.EpisodeEvents,
                state.TotalEpisodeCount,
                state.TotalHiddenEpisodeCount);

        var anyPeopleChanged = await UpdateShowPeopleAsync(show, forceRefresh, downloadImages, state.PeopleToAddOrKeep, state.PeopleToPotentiallyRemove).ConfigureAwait(false);

        return new TmdbSeasonEpisodeUpdateResult(
            state.SeasonsToSave.Count > 0 || seasonsToRemove.Count > 0 || state.EpisodesToSave.Count > 0 || episodesToRemove.Count > 0 || anyPeopleChanged,
            state.SeasonEvents,
            state.EpisodeEvents,
            state.TotalEpisodeCount,
            state.TotalHiddenEpisodeCount);
    }

    private ILookup<int, int> BuildEpisodePeopleLookup(TvShow show, ShowSyncState state)
        => state.DownloadCrewAndCast
            ? _tmdbEpisodeCast.GetByTmdbShowID(show.Id).Select(c => (c.TmdbEpisodeID, c.TmdbPersonID))
                .Concat(_tmdbEpisodeCrew.GetByTmdbShowID(show.Id).Select(c => (c.TmdbEpisodeID, c.TmdbPersonID)))
                .ToLookup(x => x.TmdbEpisodeID, x => x.TmdbPersonID)
            : Enumerable.Empty<TMDB_Episode_Cast>().ToLookup(c => c.TmdbEpisodeID, c => c.TmdbPersonID);

    private async Task ProcessShowSeasonAsync(
        TvShow show,
        SearchTvSeason reducedSeason,
        Dictionary<int, TMDB_Season> existingSeasons,
        Dictionary<int, TMDB_Episode> existingEpisodes,
        ILookup<int, int> episodePeople,
        ShowSyncState state)
    {
        _logger.LogDebug("Checking season {SeasonNumber} for show {ShowTitle} (Show={ShowId})", reducedSeason.SeasonNumber, show.Name, show.Id);

        if (TryAccumulateUnchangedSeason(show, reducedSeason, existingSeasons, episodePeople, state))
            return;

        var season = await UseClient(c => c.GetTvSeasonAsync(show.Id, reducedSeason.SeasonNumber, TvSeasonMethods.Translations), $"Get season {reducedSeason.SeasonNumber} for show {show.Id} \"{show.Name}\"").ConfigureAwait(false) ??
            throw new Exception($"Unable to fetch season {reducedSeason.SeasonNumber} for show \"{show.Name}\".");

        if (!existingSeasons.TryGetValue(reducedSeason.Id, out var tmdbSeason))
        {
            state.SeasonsAdded++;
            tmdbSeason = new(reducedSeason.Id);
        }
        var newlyAddedSeason = tmdbSeason.CreatedAt == tmdbSeason.LastUpdatedAt;

        var seasonUpdated = tmdbSeason.Populate(show, season);
        seasonUpdated = UpdateTitlesAndOverviews(tmdbSeason, season.Translations, state.PreferredTitleLanguages, state.PreferredOverviewLanguages) || seasonUpdated;

        var seasonAlreadySaved = false;
        if ((newlyAddedSeason && state.ShouldFireEvents) || seasonUpdated)
        {
            state.SeasonEvents.TryAdd(tmdbSeason, newlyAddedSeason ? UpdateReason.Added : UpdateReason.Updated);
            if (state.ShouldFireEvents)
                tmdbSeason.LastUpdatedAt = DateTime.Now;
            state.SeasonsToSave.Add(tmdbSeason);
            seasonAlreadySaved = true;
        }

        state.SeasonsToSkip.Add(tmdbSeason.Id);

        var (episodeCount, hiddenEpisodeCount) = await ProcessSeasonEpisodesAsync(show, season, existingEpisodes, episodePeople, state).ConfigureAwait(false);

        if (tmdbSeason.EpisodeCount != episodeCount)
        {
            tmdbSeason.EpisodeCount = episodeCount;
            seasonUpdated = true;
        }
        if (tmdbSeason.HiddenEpisodeCount != hiddenEpisodeCount)
        {
            tmdbSeason.HiddenEpisodeCount = hiddenEpisodeCount;
            seasonUpdated = true;
        }
        if (seasonUpdated)
        {
            tmdbSeason.LastUpdatedAt = DateTime.Now;
            if (!seasonAlreadySaved)
            {
                state.SeasonEvents.TryAdd(tmdbSeason, UpdateReason.Updated);
                state.SeasonsToSave.Add(tmdbSeason);
            }
        }

        state.TotalEpisodeCount += episodeCount;
        state.TotalHiddenEpisodeCount += hiddenEpisodeCount;
    }

    // Skipped seasons still need their episode counts registered so season totals stay accurate.
    private bool TryAccumulateUnchangedSeason(
        TvShow show,
        SearchTvSeason reducedSeason,
        Dictionary<int, TMDB_Season> existingSeasons,
        ILookup<int, int> episodePeople,
        ShowSyncState state)
    {
        if (!state.ChangedItems.HasValue || state.ChangedItems.Value.SeasonNumbers.Contains(reducedSeason.SeasonNumber) || !existingSeasons.TryGetValue(reducedSeason.Id, out var unchangedSeason))
            return false;

        state.SeasonsToSkip.Add(unchangedSeason.Id);
        var localEpisodes = _tmdbEpisodes.GetByTmdbSeasonID(unchangedSeason.Id);
        var visibleCount = 0;
        foreach (var ep in localEpisodes)
        {
            state.EpisodesToSkip.Add(ep.Id);
            if (!ep.IsHidden) visibleCount++;
        }
        state.TotalEpisodeCount += visibleCount;
        state.TotalHiddenEpisodeCount += localEpisodes.Count - visibleCount;
        if (state.DownloadCrewAndCast)
            foreach (var ep in localEpisodes)
                foreach (var personId in episodePeople[ep.Id])
                    state.PeopleToAddOrKeep.Add(personId);
        _logger.LogDebug("Skipping unchanged season {SeasonNumber} for show {ShowTitle} (Show={ShowId}).", reducedSeason.SeasonNumber, show.Name, show.Id);
        return true;
    }

    private async Task<TmdbSeasonEpisodeCounts> ProcessSeasonEpisodesAsync(
        TvShow show,
        TvSeason season,
        Dictionary<int, TMDB_Episode> existingEpisodes,
        ILookup<int, int> episodePeople,
        ShowSyncState state)
    {
        var episodeBag = new List<TMDB_Episode>();
        var hiddenEpisodeBag = new List<TMDB_Episode>();

        foreach (var reducedEpisode in season.Episodes!)
        {
            _logger.LogDebug("Checking episode {EpisodeNumber} in season {SeasonNumber} for show {ShowTitle} (Show={ShowId})", reducedEpisode.EpisodeNumber, season.SeasonNumber, show.Name, show.Id);
            if (!existingEpisodes.TryGetValue(reducedEpisode.Id, out var tmdbEpisode))
            {
                state.EpisodesAdded++;
                tmdbEpisode = new(reducedEpisode.Id);
            }
            var newlyAddedEpisode = tmdbEpisode.CreatedAt == tmdbEpisode.LastUpdatedAt;

            if (AccumulateUnchangedEpisode(tmdbEpisode, season, reducedEpisode, episodePeople, episodeBag, hiddenEpisodeBag, state))
                continue;

            var episodeUpdated = await FetchAndPopulateEpisodeAsync(show, season, reducedEpisode, tmdbEpisode, state).ConfigureAwait(false);

            TrySaveEpisode(tmdbEpisode, newlyAddedEpisode, episodeUpdated, state);
            state.EpisodesToSkip.Add(tmdbEpisode.Id);
            (tmdbEpisode.IsHidden ? hiddenEpisodeBag : episodeBag).Add(tmdbEpisode);
        }

        return new TmdbSeasonEpisodeCounts(episodeBag.Count, hiddenEpisodeBag.Count);
    }

    private static void TrySaveEpisode(TMDB_Episode tmdbEpisode, bool newlyAdded, bool updated, ShowSyncState state)
    {
        if (!(newlyAdded && state.ShouldFireEvents) && !updated)
            return;

        state.EpisodeEvents.TryAdd(tmdbEpisode, newlyAdded ? UpdateReason.Added : UpdateReason.Updated);
        if (state.ShouldFireEvents)
            tmdbEpisode.LastUpdatedAt = DateTime.Now;
        state.EpisodesToSave.Add(tmdbEpisode);
    }

    private static bool AccumulateUnchangedEpisode(
        TMDB_Episode tmdbEpisode,
        TvSeason season,
        TvSeasonEpisode reducedEpisode,
        ILookup<int, int> episodePeople,
        List<TMDB_Episode> episodeBag,
        List<TMDB_Episode> hiddenEpisodeBag,
        ShowSyncState state)
    {
        var newlyAdded = tmdbEpisode.CreatedAt == tmdbEpisode.LastUpdatedAt;
        if (!state.ChangedItems.HasValue || newlyAdded || state.ChangedItems.Value.Episodes.Contains((season.SeasonNumber, (int)reducedEpisode.EpisodeNumber)))
            return false;

        state.EpisodesToSkip.Add(tmdbEpisode.Id);
        (tmdbEpisode.IsHidden ? hiddenEpisodeBag : episodeBag).Add(tmdbEpisode);
        if (state.DownloadCrewAndCast)
            foreach (var personId in episodePeople[tmdbEpisode.Id])
                state.PeopleToAddOrKeep.Add(personId);
        return true;
    }

    private async Task<bool> FetchAndPopulateEpisodeAsync(
        TvShow show,
        TvSeason season,
        TvSeasonEpisode reducedEpisode,
        TMDB_Episode tmdbEpisode,
        ShowSyncState state)
    {
        if (state.QuickRefresh)
        {
            var baseUpdated = tmdbEpisode.Populate(show, season, reducedEpisode, null);
            return UpdateTitlesAndOverviews(tmdbEpisode, null, state.PreferredTitleLanguages, state.PreferredOverviewLanguages) || baseUpdated;
        }

        var episodeMethods = TvEpisodeMethods.ExternalIds | TvEpisodeMethods.Translations;
        if (state.DownloadCrewAndCast)
            episodeMethods |= TvEpisodeMethods.Credits;

        var episode = await UseClient(c => c.GetTvEpisodeAsync(show.Id, season.SeasonNumber, reducedEpisode.EpisodeNumber, episodeMethods), $"Get episode {reducedEpisode.EpisodeNumber} in season {season.SeasonNumber} for show {show.Id} \"{show.Name}\"").ConfigureAwait(false);
        if (episode is null)
            return false;

        var episodeUpdated = tmdbEpisode.Populate(show, season, reducedEpisode, episode.Translations);
        episodeUpdated = UpdateTitlesAndOverviews(tmdbEpisode, episode.Translations, state.PreferredTitleLanguages, state.PreferredOverviewLanguages) || episodeUpdated;
        episodeUpdated = UpdateEpisodeExternalIDs(tmdbEpisode, episode.ExternalIds!) || episodeUpdated;
        if (state.DownloadCrewAndCast)
        {
            var (castOrCrewUpdated, peopleToAddOrKeep, peopleToPotentiallyRemove) = UpdateEpisodeCastAndCrew(tmdbEpisode, episode.Credits!);
            episodeUpdated |= castOrCrewUpdated;
            AccumulateEpisodePeople(peopleToAddOrKeep, peopleToPotentiallyRemove, state);
        }
        return episodeUpdated;
    }

    private static void AccumulateEpisodePeople(IEnumerable<int> toAddOrKeep, IEnumerable<int> toPotentiallyRemove, ShowSyncState state)
    {
        foreach (var personId in toAddOrKeep)
            state.PeopleToAddOrKeep.Add(personId);
        foreach (var personId in toPotentiallyRemove)
            state.PeopleToPotentiallyRemove.Add(personId);
    }

    private async Task<bool> UpdateShowPeopleAsync(TvShow show, bool forceRefresh, bool downloadImages, HashSet<int> allPeopleToAddOrKeep, HashSet<int> allPeopleToPotentiallyRemove)
    {
        var added = 0;
        var updated = 0;
        var purged = 0;
        var peopleToCheck = allPeopleToAddOrKeep;
        var peopleToPurge = allPeopleToPotentiallyRemove.Except(peopleToCheck).ToList();
        var transientlyFailedShowPeople = new ConcurrentBag<int>();
        await ProcessWithConcurrencyAsync(_maxConcurrency, peopleToCheck, async personId =>
        {
            try
            {
                var (personAdded, personUpdated) = await UpdatePerson(personId, forceRefresh, downloadImages, currentShowId: show.Id);
                if (personAdded)
                    Interlocked.Increment(ref added);
                else if (personUpdated)
                    Interlocked.Increment(ref updated);
            }
            catch (Exception ex) when (IsTmdbTransient(ex))
            {
                // Transient failure — preserve cast/crew rows and retry later.
                transientlyFailedShowPeople.Add(personId);
            }
            catch (Exception ex)
            {
                // Non-transient failure — log and let CleanupOrphanedCastCrew handle it.
                _logger.LogWarning(ex, "TMDB: Unexpected error updating person {PersonId} for show {ShowId}", personId, show.Id);
            }
        }, onDropped: transientlyFailedShowPeople.Add);
        // Schedule retries for transiently-failed people; their cast/crew rows are preserved.
        var transientlyFailedShowSet = transientlyFailedShowPeople.ToHashSet();
        if (transientlyFailedShowSet.Count > 0)
            await Task.WhenAll(transientlyFailedShowSet.Select(personId =>
                _scheduler.Enqueue<UpdateTmdbPersonJob>(j => { j.TmdbPersonID = personId; j.DownloadImages = downloadImages; j.TmdbShowID = show.Id; })));
        // Remove cast/crew for people that permanently failed — transient failures are excluded.
        CleanupOrphanedCastCrew(peopleToCheck, transientlyFailedShowSet, missingPersonIds =>
        {
            var orphanedCast = _tmdbEpisodeCast.GetByTmdbShowID(show.Id)
                .Where(c => missingPersonIds.Contains(c.TmdbPersonID)).ToList();
            var orphanedCrew = _tmdbEpisodeCrew.GetByTmdbShowID(show.Id)
                .Where(c => missingPersonIds.Contains(c.TmdbPersonID)).ToList();
            if (orphanedCast.Count > 0 || orphanedCrew.Count > 0)
            {
                _logger.LogWarning("TMDB: Removed {CastCount} cast and {CrewCount} crew entries for {PersonCount} people that failed to fetch. (Show={ShowId})",
                    orphanedCast.Count, orphanedCrew.Count, missingPersonIds.Count, show.Id);
                _tmdbEpisodeCast.Delete(orphanedCast);
                _tmdbEpisodeCrew.Delete(orphanedCrew);
            }
        });
        try
        {
            await ProcessWithConcurrencyAsync(_maxConcurrency, peopleToPurge, async personId =>
            {
                if (await PurgePerson(personId))
                    Interlocked.Increment(ref purged);
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TMDB: Failed to purge one or more people during show cast/crew update (Show={ShowId})", show.Id);
        }
        _logger.LogDebug("Added/updated/purged/skipped {Added}/{Updated}/{Purged}/{Skipped} staff for show {ShowTitle} (Show={ShowId})",
            added, updated, purged, peopleToPurge.Count + peopleToCheck.Count - added - updated - purged, show.Name, show.Id);
        return added > 0 || purged > 0;
    }

    private void CleanupOrphanedCastCrew(IReadOnlyCollection<int> people, HashSet<int> transientlyFailed, Action<HashSet<int>> deleteOrphansAndLog)
    {
        var candidates = people.Except(transientlyFailed).ToHashSet();
        var existingIds = _tmdbPeople.GetExistingTmdbPersonIDs(candidates);
        var missingPersonIds = candidates.Except(existingIds).ToHashSet();
        if (missingPersonIds.Count > 0)
            deleteOrphansAndLog(missingPersonIds);
    }

    private async Task<bool> UpdateShowAlternateOrdering(TMDB_Show tmdbShow, TvShow show)
    {
        _logger.LogDebug(
            "Checking {count} episode group collections to create alternate orderings for show {ShowTitle} (Show={ShowId})",
            show.EpisodeGroups!.Results!.Count,
            show.Name,
            show.Id);

        var hiddenEpisodes = _tmdbEpisodes.GetByTmdbShowID(show.Id)
            .Where(episode => episode.IsHidden)
            .Select(episode => episode.Id)
            .ToHashSet();
        var existingOrdering = _tmdbAlternateOrdering.GetByTmdbShowID(show.Id)
            .ToDictionary(ordering => ordering.Id);
        var orderingToAdd = 0;
        var orderingToSkip = new HashSet<string>();
        var orderingToSave = new List<TMDB_AlternateOrdering>();

        var existingSeasons = _tmdbAlternateOrderingSeasons.GetByTmdbShowID(show.Id)
            .ToDictionary(season => season.Id);
        var seasonsToAdd = 0;
        var seasonsToSkip = new HashSet<string>();
        var seasonsToSave = new HashSet<TMDB_AlternateOrdering_Season>();

        var existingEpisodes = _tmdbAlternateOrderingEpisodes.GetByTmdbShowID(show.Id)
            .ToDictionary(episode => episode.Id);
        var episodesToAdd = 0;
        var episodesToSkip = new HashSet<string>();
        var episodesToSave = new List<TMDB_AlternateOrdering_Episode>();

        foreach (var reducedCollection in show.EpisodeGroups.Results)
        {
            // The object sent from the show endpoint doesn't have the groups,
            // we need to send another request for the full episode group
            // collection to get the groups.
            var collection = await UseClient(c => c.GetTvEpisodeGroupsAsync(reducedCollection.Id!), $"Get alternate ordering {reducedCollection.Id} \"{reducedCollection.Name}\" for show {show.Id} \"{show.Name}\"").ConfigureAwait(false) ??
                throw new Exception($"Unable to fetch alternate ordering \"{reducedCollection.Name}\" for show \"{show.Name}\".");

            if (!existingOrdering.TryGetValue(collection.Id!, out var tmdbOrdering))
            {
                orderingToAdd++;
                tmdbOrdering = new(collection.Id!);
            }

            var orderingUpdated = tmdbOrdering.Populate(collection, show.Id);

            var totalEpisodeCount = 0;
            var totalHiddenEpisodeCount = 0;
            foreach (var episodeGroup in collection.Groups!)
            {
                if (!existingSeasons.TryGetValue(episodeGroup.Id!, out var tmdbSeason))
                {
                    seasonsToAdd++;
                    tmdbSeason = new(episodeGroup.Id!);
                }

                var seasonUpdated = tmdbSeason.Populate(episodeGroup, collection.Id!, show.Id, episodeGroup.Order);

                var episodeCount = 0;
                var hiddenEpisodeCount = 0;
                var episodeNumberCount = 1;
                foreach (var episode in episodeGroup.Episodes!)
                {
                    if (!episode.Id.HasValue)
                        continue;

                    var episodeNumber = episodeNumberCount++;
                    var episodeId = episode.Id.Value;
                    if (hiddenEpisodes.Contains(episodeId))
                        hiddenEpisodeCount++;
                    else
                        episodeCount++;

                    if (!existingEpisodes.TryGetValue($"{episodeGroup.Id}:{episodeId}", out var tmdbEpisode))
                    {
                        episodesToAdd++;
                        tmdbEpisode = new(episodeGroup.Id!, episodeId);
                    }

                    var episodeUpdated = tmdbEpisode.Populate(collection.Id!, show.Id, episodeGroup.Order, episodeNumber);
                    if (episodeUpdated)
                    {
                        tmdbEpisode.LastUpdatedAt = DateTime.Now;
                        episodesToSave.Add(tmdbEpisode);
                    }

                    episodesToSkip.Add(tmdbEpisode.Id);
                }

                if (tmdbSeason.EpisodeCount != episodeCount)
                {
                    tmdbSeason.EpisodeCount = episodeCount;
                    seasonUpdated = true;
                }
                if (tmdbSeason.HiddenEpisodeCount != hiddenEpisodeCount)
                {
                    tmdbSeason.HiddenEpisodeCount = hiddenEpisodeCount;
                    seasonUpdated = true;
                }

                if (seasonUpdated)
                {
                    tmdbSeason.LastUpdatedAt = DateTime.Now;
                    seasonsToSave.Add(tmdbSeason);
                }

                totalEpisodeCount += episodeCount;
                totalHiddenEpisodeCount += hiddenEpisodeCount;
                seasonsToSkip.Add(tmdbSeason.Id);
            }

            if (tmdbOrdering.EpisodeCount != totalEpisodeCount)
            {
                tmdbOrdering.EpisodeCount = totalEpisodeCount;
                orderingUpdated = true;
            }
            if (tmdbOrdering.HiddenEpisodeCount != totalHiddenEpisodeCount)
            {
                tmdbOrdering.HiddenEpisodeCount = totalHiddenEpisodeCount;
                orderingUpdated = true;
            }

            if (orderingUpdated)
            {
                tmdbOrdering.LastUpdatedAt = DateTime.Now;
                orderingToSave.Add(tmdbOrdering);
            }

            orderingToSkip.Add(tmdbOrdering.Id);
        }
        var orderingToRemove = existingOrdering.Values
            .ExceptBy(orderingToSkip, ordering => ordering.Id)
            .ToList();
        var seasonsToRemove = existingSeasons.Values
            .ExceptBy(seasonsToSkip, season => season.Id)
            .ToList();
        var episodesToRemove = existingEpisodes.Values
            .ExceptBy(episodesToSkip, episode => episode.Id)
            .ToList();

        _logger.LogDebug(
            "Added/updated/removed/skipped {oa}/{ou}/{or}/{os} alternate orderings, {sa}/{su}/{sr}/{ss} alternate ordering seasons, and {ea}/{eu}/{er}/{es} alternate ordering episodes for show {ShowTitle} (Show={ShowId})",
            orderingToAdd,
            orderingToSave.Count - orderingToAdd,
            orderingToRemove.Count,
            existingOrdering.Count + orderingToAdd - orderingToRemove.Count - orderingToSave.Count,
            seasonsToAdd,
            seasonsToSave.Count - seasonsToAdd,
            seasonsToRemove.Count,
            existingSeasons.Count + seasonsToAdd - seasonsToRemove.Count - seasonsToSave.Count,
            episodesToAdd,
            episodesToSave.Count - episodesToAdd,
            episodesToRemove.Count,
            existingEpisodes.Count + episodesToAdd - episodesToRemove.Count - episodesToSave.Count,
            show.Name,
            show.Id);

        _tmdbAlternateOrdering.Save(orderingToSave);
        _tmdbAlternateOrdering.Delete(orderingToRemove);

        _tmdbAlternateOrderingSeasons.Save(seasonsToSave);
        _tmdbAlternateOrderingSeasons.Delete(seasonsToRemove);

        _tmdbAlternateOrderingEpisodes.Save(episodesToSave);
        _tmdbAlternateOrderingEpisodes.Delete(episodesToRemove);

        var preferredOrderingUpdated = false;
        if (tmdbShow.PreferredAlternateOrderingID is not null)
        {
            var allOrderings = show.EpisodeGroups.Results.Select(ordering => ordering.Id).ToHashSet();
            if (!allOrderings.Contains(tmdbShow.PreferredAlternateOrderingID))
            {
                tmdbShow.PreferredAlternateOrderingID = null;
                preferredOrderingUpdated = true;
            }
        }

        return orderingToSave.Count > 0 ||
            orderingToRemove.Count > 0 ||
            seasonsToSave.Count > 0 ||
            seasonsToRemove.Count > 0 ||
            episodesToSave.Count > 0 ||
            episodesToRemove.Count > 0 ||
            preferredOrderingUpdated;
    }

    private (bool, IEnumerable<int>, IEnumerable<int>) UpdateEpisodeCastAndCrew(TMDB_Episode tmdbEpisode, CreditsWithGuestStars credits)
    {
        var peopleToAddOrKeep = new HashSet<int>();
        var counter = 0;
        var castToAdd = 0;
        var castToKeep = new HashSet<string>();
        var castToSave = new List<TMDB_Episode_Cast>();
        var existingCastDict = _tmdbEpisodeCast.GetByTmdbEpisodeID(tmdbEpisode.Id)
            .ToDictionary(cast => cast.TmdbCreditID);
        var guestOffset = credits.Cast!.Count;
        foreach (var cast in credits.Cast.Concat(credits.GuestStars!))
        {
            var ordering = counter++;
            var isGuestRole = ordering >= guestOffset;
            castToKeep.Add(cast.CreditId!);
            peopleToAddOrKeep.Add(cast.Id);

            var roleUpdated = false;
            if (!existingCastDict.TryGetValue(cast.CreditId!, out var role))
            {
                role = new()
                {
                    TmdbShowID = tmdbEpisode.TmdbShowID,
                    TmdbSeasonID = tmdbEpisode.TmdbSeasonID,
                    TmdbEpisodeID = tmdbEpisode.Id,
                    TmdbPersonID = cast.Id,
                    TmdbCreditID = cast.CreditId!,
                    Ordering = ordering,
                    IsGuestRole = isGuestRole,
                };
                castToAdd++;
                roleUpdated = true;
            }

            var characterName = cast.Character!.Replace(" (voice)", "");
            if (role.CharacterName != characterName)
            {
                role.CharacterName = characterName;
                roleUpdated = true;
            }

            if (role.Ordering != ordering)
            {
                role.Ordering = ordering;
                roleUpdated = true;
            }

            if (role.IsGuestRole != isGuestRole)
            {
                role.IsGuestRole = isGuestRole;
                roleUpdated = true;
            }

            if (roleUpdated)
            {
                castToSave.Add(role);
            }
        }

        var crewToAdd = 0;
        var crewToKeep = new HashSet<string>();
        var crewToSave = new List<TMDB_Episode_Crew>();
        var existingCrewDict = _tmdbEpisodeCrew.GetByTmdbEpisodeID(tmdbEpisode.Id)
            .ToDictionary(crew => crew.TmdbCreditID);
        foreach (var crew in credits.Crew!)
        {
            peopleToAddOrKeep.Add(crew.Id);
            crewToKeep.Add(crew.CreditId!);
            var roleUpdated = false;
            if (!existingCrewDict.TryGetValue(crew.CreditId!, out var role))
            {
                role = new()
                {
                    TmdbShowID = tmdbEpisode.TmdbShowID,
                    TmdbSeasonID = tmdbEpisode.TmdbSeasonID,
                    TmdbEpisodeID = tmdbEpisode.Id,
                    TmdbPersonID = crew.Id,
                    TmdbCreditID = crew.CreditId!,
                };
                crewToAdd++;
                roleUpdated = true;
            }

            if (role.Department != crew.Department)
            {
                role.Department = crew.Department!;
                roleUpdated = true;
            }

            if (role.Job != crew.Job)
            {
                role.Job = crew.Job!;
                roleUpdated = true;
            }

            if (roleUpdated)
            {
                crewToSave.Add(role);
            }
        }

        var castToRemove = existingCastDict.Values
            .ExceptBy(castToKeep, cast => cast.TmdbCreditID)
            .ToList();
        var crewToRemove = existingCrewDict.Values
            .ExceptBy(crewToKeep, crew => crew.TmdbCreditID)
            .ToList();

        _tmdbEpisodeCast.Save(castToSave);
        _tmdbEpisodeCrew.Save(crewToSave);
        _tmdbEpisodeCast.Delete(castToRemove);
        _tmdbEpisodeCrew.Delete(crewToRemove);

        var peopleToPotentiallyRemove = new HashSet<int>([
            ..existingCastDict.Values.Select(cast => cast.TmdbPersonID),
            ..existingCrewDict.Values.Select(crew => crew.TmdbPersonID),
        ]);

        _logger.LogDebug(
            "Added/updated/removed/skipped {aa}/{au}/{ar}/{as} cast and {ra}/{ru}/{rr}/{rs} crew for episode {EpisodeTitle} (Show={ShowId},Season={SeasonId},Episode={EpisodeId})",
            castToAdd,
            castToSave.Count - castToAdd,
            castToRemove.Count,
            existingCastDict.Count - (castToSave.Count - castToAdd),
            crewToAdd,
            crewToSave.Count - crewToAdd,
            crewToRemove.Count,
            existingCrewDict.Count - (crewToSave.Count - crewToAdd),
            tmdbEpisode.EnglishTitle,
            tmdbEpisode.TmdbShowID,
            tmdbEpisode.TmdbSeasonID,
            tmdbEpisode.TmdbEpisodeID
        );
        return (
            castToSave.Count > 0 ||
            castToRemove.Count > 0 ||
            crewToSave.Count > 0 ||
            crewToRemove.Count > 0,
            peopleToAddOrKeep,
            peopleToPotentiallyRemove
        );
    }

    private async Task<bool> UpdateShowNetworks(TMDB_Show tmdbShow, TvShow show)
    {
        var index = 0;
        var xrefsToSkip = new HashSet<int>();
        var xrefsToSave = new List<TMDB_Show_Network>();
        var existingNetworks = tmdbShow.TmdbNetworkCrossReferences
            .ToDictionary(network => network.TmdbNetworkID);
        var existingXref = _xrefTmdbShowNetwork.GetByTmdbShowID(tmdbShow.Id)
            .ToDictionary(xref => xref.TmdbNetworkID);
        foreach (var network in show.Networks!)
        {
            var ordering = index++;
            if (existingXref.TryGetValue(network.Id, out var xref))
            {
                xrefsToSkip.Add(xref.TMDB_Show_NetworkID);
                if (xref.Ordering != ordering)
                {
                    xref.Ordering = ordering;
                    xrefsToSave.Add(xref);
                }
            }
            else
            {
                xref = new()
                {
                    Ordering = ordering,
                    TmdbNetworkID = network.Id,
                    TmdbShowID = tmdbShow.Id,
                };
                xrefsToSave.Add(xref);
            }
        }

        var networksToPurge = new HashSet<int>();
        var xrefsToRemove = existingNetworks.Values.ExceptBy(xrefsToSkip, network => network.TMDB_Show_NetworkID).ToList();
        foreach (var xref in xrefsToRemove)
        {
            if (_xrefTmdbShowNetwork.GetByTmdbNetworkID(xref.TmdbNetworkID).Count <= 1)
                networksToPurge.Add(xref.TmdbNetworkID);
        }

        _xrefTmdbShowNetwork.Save(xrefsToSave);
        _xrefTmdbShowNetwork.Delete(xrefsToRemove);

        foreach (var network in show.Networks)
            await CreateOrUpdateNetwork(network);
        foreach (var networkId in networksToPurge)
            await PurgeShowNetwork(networkId);

        return true;
    }

    private async Task CreateOrUpdateNetwork(NetworkWithLogo network)
    {
        using (await GetLockForEntity(DataEntityType.Network, network.Id, "metadata & images", "Update").ConfigureAwait(false))
        {
            var tmdbNetwork = _tmdbNetwork.GetByTmdbNetworkID(network.Id) ??
                new() { TmdbNetworkID = network.Id };
            var updated = tmdbNetwork.TMDB_NetworkID is 0;
            if (!string.Equals(tmdbNetwork.Name, network.Name))
            {
                tmdbNetwork.Name = network.Name!;
                updated = true;
            }
            if (!string.Equals(tmdbNetwork.CountryOfOrigin, network.OriginCountry))
            {
                tmdbNetwork.CountryOfOrigin = network.OriginCountry!;
                updated = true;
            }
            if (_xrefTmdbShowNetwork.GetByTmdbNetworkID(network.Id).Count == 0 && tmdbNetwork.LastOrphanedAt.HasValue)
            {
                tmdbNetwork.LastOrphanedAt = null;
                updated = true;
            }
            if (updated)
            {
                _tmdbNetwork.Save(tmdbNetwork);
                _logger.LogDebug("Updated TMDB Network (Network={NetworkId})", network.Id);
            }

            var settings = _settingsProvider.GetSettings();
            if (!string.IsNullOrEmpty(network.LogoPath))
                await _imageService.DownloadImageByType(network.LogoPath, ImageEntityType.Logo, tmdbNetwork, isDesired: settings.TMDB.AutoDownloadStudioImages);
        }
    }

    public async Task ScheduleDownloadAllShowImages(int showId, bool forceDownload = false)
    {
        // Schedule the movie info to be downloaded or updated.
        await _scheduler.StartJob<DownloadTmdbShowImagesJob>(c =>
        {
            c.TmdbShowID = showId;
            c.ForceDownload = forceDownload;
        }).ConfigureAwait(false);
    }

    public async Task DownloadAllShowImages(int showId, bool forceDownload = false)
    {
        using (await GetLockForEntity(DataEntityType.Show, showId, "images", "Update").ConfigureAwait(false))
        {
            // Abort if we're within a certain time frame as to not try and get us rate-limited.
            var tmdbShow = _tmdbShows.GetByTmdbShowID(showId);
            if (tmdbShow is null)
                return;

            _logger.LogDebug("Downloading all images for show {ShowTitle} (Show={ShowId})", tmdbShow.EnglishTitle, showId);

            await DownloadShowImages(showId, tmdbShow.OriginalLanguage, forceDownload);

            foreach (var tmdbSeason in tmdbShow.TmdbSeasons)
            {
                await DownloadSeasonImages(tmdbSeason.TmdbSeasonID, tmdbSeason.TmdbShowID, tmdbSeason.SeasonNumber, tmdbShow.OriginalLanguage, forceDownload);
                foreach (var tmdbEpisode in tmdbSeason.TmdbEpisodes)
                {
                    await DownloadEpisodeImages(tmdbEpisode.TmdbEpisodeID, tmdbEpisode.TmdbShowID, tmdbSeason.SeasonNumber, tmdbEpisode.EpisodeNumber, tmdbShow.OriginalLanguage, forceDownload);
                }
            }
        }
    }

    private async Task DownloadShowImages(int showId, TitleLanguage? mainLanguage = null, bool forceDownload = false)
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.TMDB.AutoDownloadPosters && !settings.TMDB.AutoDownloadLogos && !settings.TMDB.AutoDownloadBackdrops)
            return;

        if (_tmdbShows.GetByTmdbShowID(showId) is not { } show)
            return;

        _logger.LogDebug("Downloading images for show. (Show={ShowId})", showId);

        var images = await UseClient(c => c.GetTvShowImagesAsync(showId), $"Get images for show {showId}").ConfigureAwait(false);
        if (images is null)
            return;

        var languages = GetLanguages(mainLanguage);
        if (settings.TMDB.AutoDownloadPosters)
            await _imageService.DownloadImagesByType(show.PosterPath, images.Posters!, ImageEntityType.Primary, show, settings.TMDB.MaxAutoPosters, languages, forceDownload);
        if (settings.TMDB.AutoDownloadLogos)
            await _imageService.DownloadImagesByType(null, images.Logos!, ImageEntityType.Logo, show, settings.TMDB.MaxAutoLogos, languages, forceDownload);
        if (settings.TMDB.AutoDownloadBackdrops)
            await _imageService.DownloadImagesByType(show.BackdropPath, images.Backdrops!, ImageEntityType.Backdrop, show, settings.TMDB.MaxAutoBackdrops, languages, forceDownload);
    }

    private async Task DownloadSeasonImages(int seasonId, int showId, int seasonNumber, TitleLanguage? mainLanguage = null, bool forceDownload = false)
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.TMDB.AutoDownloadPosters)
            return;

        if (_tmdbSeasons.GetByTmdbSeasonID(seasonId) is not { } season)
            return;

        _logger.LogDebug("Downloading images for season {SeasonNumber}. (Show={ShowId}, Season={SeasonId})", seasonNumber, showId, seasonId);

        var images = await UseClient(c => c.GetTvSeasonImagesAsync(showId, seasonNumber), $"Get images for season {seasonNumber} in show {showId}").ConfigureAwait(false);
        if (images is null)
            return;

        var languages = GetLanguages(mainLanguage);
        await _imageService.DownloadImagesByType(season.PosterPath, images.Posters!, ImageEntityType.Primary, season, settings.TMDB.MaxAutoPosters, languages, forceDownload);
    }

    private async Task DownloadEpisodeImages(int episodeId, int showId, int seasonNumber, int episodeNumber, TitleLanguage mainLanguage, bool forceDownload = false)
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.TMDB.AutoDownloadThumbnails)
            return;

        if (_tmdbEpisodes.GetByTmdbEpisodeID(episodeId) is not { } episode)
            return;

        _logger.LogDebug("Downloading images for episode {EpisodeNumber} in season {SeasonNumber}. (Show={ShowId}, Episode={EpisodeId})", episodeNumber, seasonNumber, showId, episodeId);

        var images = await UseClient(c => c.GetTvEpisodeImagesAsync(showId, seasonNumber, episodeNumber), $"Get images for episode {episodeNumber} in season {seasonNumber} in show {showId}").ConfigureAwait(false);
        if (images is null)
            return;

        var languages = GetLanguages(mainLanguage);
        await _imageService.DownloadImagesByType(episode.ThumbnailPath, images.Stills!, ImageEntityType.Backdrop, episode, settings.TMDB.MaxAutoThumbnails, languages, forceDownload);
    }

    private List<TitleLanguage> GetLanguages(TitleLanguage? mainLanguage = null) => _settingsProvider.GetSettings().TMDB.ImageLanguageOrder
        .Select(a => a is TitleLanguage.Main ? mainLanguage is not TitleLanguage.None and not TitleLanguage.Unknown ? mainLanguage : null : a)
        .WhereNotNull()
        .Distinct()
        .ToList();

    #endregion

    #region Purge (Shows)

    public async Task PurgeAllUnusedShows(DateTime? olderThan = null)
    {
        var toKeep = _xrefAnidbTmdbShows.GetAll().Select(xref => xref.TmdbShowID).ToHashSet();
        // Seeded with toKeep so AllCount in the log includes xref-linked IDs that have no TMDB_Show row yet.
        var allShows = new HashSet<int>(toKeep);
        allShows.UnionWith(_tmdbShows.GetAll().Select(s => s.TmdbShowID));
        allShows.UnionWith(_shokoImageXrefRepository.GetByEntity(DataSource.TMDB, DataEntityType.Show).Select(xref => int.Parse(xref.EntityID)));
        allShows.UnionWith(_xrefTmdbCompanyEntity.GetAll().Where(x => x.TmdbEntityType is DataEntityType.Show).Select(x => x.TmdbEntityID));
        allShows.UnionWith(_xrefTmdbShowNetwork.GetAll().Select(x => x.TmdbShowID));
        allShows.UnionWith(_tmdbSeasons.GetAll().Select(x => x.TmdbShowID));
        allShows.UnionWith(_tmdbEpisodes.GetAll().Select(x => x.TmdbShowID));
        allShows.UnionWith(_tmdbAlternateOrdering.GetAll().Select(x => x.TmdbShowID));
        allShows.UnionWith(_tmdbAlternateOrderingSeasons.GetAll().Select(x => x.TmdbShowID));
        allShows.UnionWith(_tmdbAlternateOrderingEpisodes.GetAll().Select(x => x.TmdbShowID));
        var toBePurged = allShows.Except(toKeep).ToHashSet();

        if (olderThan.HasValue)
            toBePurged = toBePurged
                .Where(id => _tmdbShows.GetByTmdbShowID(id) is not { LastUpdatedAt: var t } || t < olderThan.Value)
                .ToHashSet();

        _logger.LogInformation("Scheduling {Count} out of {AllCount} shows to be purged.", toBePurged.Count, allShows.Count);
        foreach (var showID in toBePurged)
            await _scheduler.StartJob<PurgeTmdbShowJob>(c => c.TmdbShowID = showID);
    }

    public async Task SchedulePurgeOfShow(int showId)
    {
        await _scheduler.StartJob<PurgeTmdbShowJob>(c => c.TmdbShowID = showId).ConfigureAwait(false);
    }

    public async Task PurgeShow(int showId)
    {
        using (await GetLockForEntity(DataEntityType.Show, showId, "metadata", "Purge").ConfigureAwait(false))
        {
            var show = _tmdbShows.GetByTmdbShowID(showId);

            await _linkingService.RemoveAllShowLinksForShow(showId);

            _imageService.PurgeImages(show ?? new() { TmdbShowID = showId });

            if (show is not null)
            {
                _logger.LogTrace(
                    "Removing show {ShowName} (Show={ShowId})",
                    show.EnglishTitle,
                    showId
                );
                _tmdbShows.Delete(show);
            }

            PurgeTitlesAndOverviews(DataEntityType.Show, showId);

            PurgeShowCompanies(showId);

            await PurgeShowNetworks(showId);

            PurgeShowEpisodes(showId);

            PurgeShowSeasons(showId);

            await PurgeShowCastAndCrew(showId);

            PurgeShowEpisodeGroups(showId);
        }
    }

    private void PurgeShowCompanies(int showId)
    {
        var xrefsToRemove = _xrefTmdbCompanyEntity.GetByTmdbEntityTypeAndID(DataEntityType.Show, showId);
        foreach (var xref in xrefsToRemove)
        {
            // Delete xref or purge company.
            var xrefs = _xrefTmdbCompanyEntity.GetByTmdbCompanyID(xref.TmdbCompanyID);
            if (xrefs.Count > 1)
                _xrefTmdbCompanyEntity.Delete(xref);
            else
                PurgeCompany(xref.TmdbCompanyID);
        }
    }

    private async Task PurgeShowNetworks(int showId)
    {
        var xrefsToRemove = _xrefTmdbShowNetwork.GetByTmdbShowID(showId);
        foreach (var xref in xrefsToRemove)
        {
            // Delete xref or purge network. Always delete the xref first so PurgeShowNetwork's
            // re-check guard sees Count == 0 and proceeds with cleanup.
            var xrefs = _xrefTmdbShowNetwork.GetByTmdbNetworkID(xref.TmdbNetworkID);
            _xrefTmdbShowNetwork.Delete(xref);
            if (xrefs.Count == 1)
                await PurgeShowNetwork(xref.TmdbNetworkID);
        }
    }

    public async Task PurgeUnlinkedShowNetworks()
    {
        var linkedNetworkIds = _xrefTmdbShowNetwork.GetAll().Select(x => x.TmdbNetworkID).ToHashSet();
        var networks = _tmdbNetwork.GetAll().Where(p => !linkedNetworkIds.Contains(p.TmdbNetworkID)).ToList();
        _logger.LogDebug("Checking {Count} orphaned networks if they should be purged.", networks.Count);
        foreach (var network in networks)
            await PurgeShowNetwork(network.TmdbNetworkID);
    }

    private async Task PurgeShowNetwork(int networkId)
    {
        using (await GetLockForEntity(DataEntityType.Network, networkId, "metadata & images", "Purge").ConfigureAwait(false))
        {
            // Re-check under the lock: abort if the network was re-linked since the caller's snapshot.
            if (_xrefTmdbShowNetwork.GetByTmdbNetworkID(networkId).Count > 0)
                return;

            var tmdbNetwork = _tmdbNetwork.GetByTmdbNetworkID(networkId);
            if (tmdbNetwork is not null)
            {
                if (!tmdbNetwork.LastOrphanedAt.HasValue)
                {
                    tmdbNetwork.LastOrphanedAt = DateTime.UtcNow;
                    _tmdbNetwork.Save(tmdbNetwork);
                    _logger.LogDebug("Marked TMDB Network as orphaned. (Network={NetworkId})", networkId);
                    return;
                }
                if (tmdbNetwork.LastOrphanedAt.Value.AddDays(7) > DateTime.UtcNow)
                {
                    _logger.LogDebug("TMDB Network has not been orphaned for 7 days yet. Skipping. (Network={NetworkId})", networkId);
                    return;
                }

                _logger.LogDebug("Removing TMDB Network. (Network={NetworkId})", networkId);
                _tmdbNetwork.Delete(tmdbNetwork);
            }

            _imageService.PurgeImages(tmdbNetwork ?? new() { TmdbNetworkID = networkId });
        }
    }

    private void PurgeShowEpisodes(int showId)
    {
        var episodesToRemove = _tmdbEpisodes.GetByTmdbShowID(showId);

        _logger.LogDebug(
            "Removing {count} episodes for show (Show={ShowId})",
            episodesToRemove.Count,
            showId
        );
        foreach (var episode in episodesToRemove)
            PurgeShowEpisode(episode);

        _tmdbEpisodes.Delete(episodesToRemove);
    }

    private void PurgeShowEpisode(TMDB_Episode episode)
    {
        _imageService.PurgeImages(episode);

        PurgeTitlesAndOverviews(DataEntityType.Episode, episode.Id);
    }

    private void PurgeShowSeasons(int showId)
    {
        var seasonsToRemove = _tmdbSeasons.GetByTmdbShowID(showId);

        _logger.LogDebug(
            "Removing {count} seasons for show (Show={ShowId})",
            seasonsToRemove.Count,
            showId
        );
        foreach (var season in seasonsToRemove)
            PurgeShowSeason(season);

        _tmdbSeasons.Delete(seasonsToRemove);
    }

    private void PurgeShowSeason(TMDB_Season season)
    {
        _imageService.PurgeImages(season);

        PurgeTitlesAndOverviews(DataEntityType.Season, season.Id);
    }

    private async Task PurgeShowCastAndCrew(int showId)
    {
        var castMembers = _tmdbEpisodeCast.GetByTmdbShowID(showId);
        var crewMembers = _tmdbEpisodeCrew.GetByTmdbShowID(showId);

        _tmdbEpisodeCast.Delete(castMembers);
        _tmdbEpisodeCrew.Delete(crewMembers);

        var allPeopleSet = castMembers.Select(c => c.TmdbPersonID)
            .Concat(crewMembers.Select(c => c.TmdbPersonID))
            .Distinct()
            .ToHashSet();
        foreach (var personId in allPeopleSet)
            await PurgePerson(personId);
    }

    private void PurgeShowEpisodeGroups(int showId)
    {
        var episodes = _tmdbAlternateOrderingEpisodes.GetByTmdbShowID(showId);
        var seasons = _tmdbAlternateOrderingSeasons.GetByTmdbShowID(showId);
        var orderings = _tmdbAlternateOrdering.GetByTmdbShowID(showId);

        _logger.LogDebug("Removing {EpisodeCount} episodes and {SeasonCount} seasons across {OrderingCount} alternate orderings for show. (Show={ShowId})", episodes.Count, seasons.Count, orderings.Count, showId);
        _tmdbAlternateOrderingEpisodes.Delete(episodes);
        _tmdbAlternateOrderingSeasons.Delete(seasons);
        _tmdbAlternateOrdering.Delete(orderings);
    }

    public void PurgeAllShowEpisodeGroups()
    {
        _logger.LogInformation("Purging all show episode groups.");

        var episodes = _tmdbAlternateOrderingEpisodes.GetAll();
        var seasons = _tmdbAlternateOrderingSeasons.GetAll();
        var orderings = _tmdbAlternateOrdering.GetAll();
        var shows = new HashSet<int>([
            ..episodes.Select(e => e.TmdbShowID),
            ..seasons.Select(s => s.TmdbShowID),
            ..orderings.Select(o => o.TmdbShowID),
        ]);

        _logger.LogDebug("Removing {EpisodeCount} episodes and {SeasonCount} seasons across {OrderingCount} alternate orderings for {ShowCount} shows.", episodes.Count, seasons.Count, orderings.Count, shows.Count);
        _tmdbAlternateOrderingEpisodes.Delete(episodes);
        _tmdbAlternateOrderingSeasons.Delete(seasons);
        _tmdbAlternateOrdering.Delete(orderings);
    }

    #endregion

    #endregion

    #region Shared

    #region Titles & Overviews (Shared)

    /// <summary>
    /// Updates the titles and overviews for the <paramref name="tmdbEntity"/>
    /// using the translation data available in the <paramref name="translations"/>.
    /// </summary>
    /// <param name="tmdbEntity">The local TMDB Entity to update titles and overviews for.</param>
    /// <param name="translations">The translations container returned from the API.</param>
    /// <param name="preferredTitleLanguages">The preferred title languages to store. If not set then we will store all languages.</param>
    /// <param name="preferredOverviewLanguages">The preferred overview languages to store. If not set then we will store all languages.</param>
    /// <returns>A boolean indicating if any changes were made to the titles and/or overviews.</returns>
    private bool UpdateTitlesAndOverviews(IEntityMetadata tmdbEntity, TranslationsContainer? translations, HashSet<TitleLanguage>? preferredTitleLanguages, HashSet<TitleLanguage>? preferredOverviewLanguages)
    {
        var (titlesUpdated, overviewsUpdated) = UpdateTitlesAndOverviewsWithTuple(tmdbEntity, translations, preferredTitleLanguages, preferredOverviewLanguages);
        return titlesUpdated || overviewsUpdated;
    }

    /// <summary>
    /// Updates the titles and overviews for the <paramref name="tmdbEntity"/>
    /// using the translation data available in the <paramref name="translations"/>.
    /// </summary>
    /// <param name="tmdbEntity">The local TMDB Entity to update titles and overviews for.</param>
    /// <param name="translations">The translations container returned from the API.</param>
    /// <param name="preferredTitleLanguages">The preferred title languages to store. If not set then we will store all languages.</param>
    /// <param name="preferredOverviewLanguages">The preferred overview languages to store. If not set then we will store all languages.</param>
    /// <returns>A tuple indicating if any changes were made to the titles and/or overviews.</returns>
    private (bool titlesUpdated, bool overviewsUpdated) UpdateTitlesAndOverviewsWithTuple(IEntityMetadata tmdbEntity, TranslationsContainer? translations, HashSet<TitleLanguage>? preferredTitleLanguages, HashSet<TitleLanguage>? preferredOverviewLanguages)
    {
        var existingOverviews = _tmdbOverview.GetByParentTypeAndID(tmdbEntity.Type, tmdbEntity.Id);
        var existingTitles = _tmdbTitle.GetByParentTypeAndID(tmdbEntity.Type, tmdbEntity.Id);
        var overviewsToAdd = 0;
        var overviewsToSkip = new HashSet<int>();
        var overviewsToSave = new List<TMDB_Overview>();
        var titlesToAdd = 0;
        var titlesToSkip = new HashSet<int>();
        var titlesToSave = new List<TMDB_Title>();
        foreach (var translation in translations?.Translations ?? [new() { EnglishName = string.Empty, Iso_3166_1 = "US", Iso_639_1 = "en", Data = new() { Name = string.Empty, Overview = string.Empty } }])
        {
            var languageCode = translation.Iso_639_1!.ToLowerInvariant();
            var countryCode = translation.Iso_3166_1!.ToUpperInvariant();

            var alwaysInclude = false;
            var currentTitle = translation.Data?.Name ?? string.Empty;
            if (!string.IsNullOrEmpty(tmdbEntity.OriginalLanguageCode) && languageCode == tmdbEntity.OriginalLanguageCode)
            {
                currentTitle = tmdbEntity.OriginalTitle;
                alwaysInclude = true;
            }
            else if (languageCode == "en" && countryCode == "US")
            {
                currentTitle = tmdbEntity.EnglishTitle;
                alwaysInclude = true;
            }

            var shouldInclude = alwaysInclude || preferredTitleLanguages is null || preferredTitleLanguages.Contains(languageCode.GetTitleLanguage()) || preferredTitleLanguages.Contains(languageCode.GetTitleLanguage(countryCode));
            var existingTitle = existingTitles.FirstOrDefault(title => title.LanguageCode == languageCode && title.CountryCode == countryCode);
            if (shouldInclude && !string.IsNullOrEmpty(currentTitle) && !(
                // Make sure the "translation" is not just the English Title or
                (languageCode != "en" && languageCode != "US" && !string.IsNullOrEmpty(tmdbEntity.EnglishTitle) && string.Equals(tmdbEntity.EnglishTitle, currentTitle, StringComparison.InvariantCultureIgnoreCase)) ||
                // the Original Title.
                (!string.IsNullOrEmpty(tmdbEntity.OriginalLanguageCode) && languageCode != tmdbEntity.OriginalLanguageCode && !string.IsNullOrEmpty(tmdbEntity.OriginalTitle) && string.Equals(tmdbEntity.OriginalTitle, currentTitle, StringComparison.InvariantCultureIgnoreCase))
            ))
            {
                if (existingTitle is null)
                {
                    titlesToAdd++;
                    titlesToSave.Add(new(tmdbEntity.Type, tmdbEntity.Id, currentTitle, languageCode, countryCode));
                }
                else
                {
                    if (!string.Equals(existingTitle.Value, currentTitle))
                    {
                        existingTitle.Value = currentTitle;
                        titlesToSave.Add(existingTitle);
                    }
                    titlesToSkip.Add(existingTitle.TMDB_TitleID);
                }
            }

            alwaysInclude = false;
            var currentOverview = translation.Data?.Overview ?? string.Empty;
            if (languageCode == "en" && countryCode == "US")
            {
                alwaysInclude = true;
                currentOverview = tmdbEntity.EnglishOverview ?? translation.Data?.Overview ?? string.Empty;
            }

            shouldInclude = alwaysInclude || preferredOverviewLanguages is null || preferredOverviewLanguages.Contains(languageCode.GetTitleLanguage()) || preferredOverviewLanguages.Contains(languageCode.GetTitleLanguage(countryCode));
            var existingOverview = existingOverviews.FirstOrDefault(overview => overview.LanguageCode == languageCode && overview.CountryCode == countryCode);
            if (shouldInclude && !string.IsNullOrEmpty(currentOverview))
            {
                if (existingOverview is null)
                {
                    overviewsToAdd++;
                    overviewsToSave.Add(new(tmdbEntity.Type, tmdbEntity.Id, currentOverview, languageCode, countryCode));
                }
                else
                {
                    if (!string.Equals(existingOverview.Value, currentOverview))
                    {
                        existingOverview.Value = currentOverview;
                        overviewsToSave.Add(existingOverview);
                    }
                    overviewsToSkip.Add(existingOverview.TMDB_OverviewID);
                }
            }
        }

        var titlesToRemove = existingTitles.ExceptBy(titlesToSkip, t => t.TMDB_TitleID).ToList();
        var overviewsToRemove = existingOverviews.ExceptBy(overviewsToSkip, o => o.TMDB_OverviewID).ToList();
        _logger.LogDebug(
            "Added/updated/removed/skipped {ta}/{tu}/{tr}/{ts} titles and {oa}/{ou}/{or}/{os} overviews for {type} {EntityTitle} ({EntityType}={EntityId})",
            titlesToAdd,
            titlesToSave.Count - titlesToAdd,
            titlesToRemove.Count,
            titlesToSkip.Count + titlesToAdd - titlesToSave.Count,
            overviewsToAdd,
            overviewsToSave.Count - overviewsToAdd,
            overviewsToRemove.Count,
            overviewsToSkip.Count + overviewsToAdd - overviewsToSave.Count,
            tmdbEntity.Type.ToString().ToLowerInvariant(),
            tmdbEntity.OriginalTitle ?? tmdbEntity.EnglishTitle ?? $"<untitled {tmdbEntity.Type.ToString().ToLowerInvariant()}>",
            tmdbEntity.Type.ToString(),
            tmdbEntity.Id);
        _tmdbOverview.Save(overviewsToSave);
        _tmdbOverview.Delete(overviewsToRemove);
        _tmdbTitle.Save(titlesToSave);
        _tmdbTitle.Delete(titlesToRemove);

        return (
            titlesToSave.Count > 0 || titlesToRemove.Count > 0,
            overviewsToSave.Count > 0 || overviewsToRemove.Count > 0
        );
    }

    private void PurgeTitlesAndOverviews(DataEntityType foreignType, int foreignId)
    {
        var overviewsToRemove = _tmdbOverview.GetByParentTypeAndID(foreignType, foreignId);
        var titlesToRemove = _tmdbTitle.GetByParentTypeAndID(foreignType, foreignId);

        _logger.LogDebug(
            "Removing {tr} titles and {or} overviews for {type} with id {EntityId}",
            titlesToRemove.Count,
            overviewsToRemove.Count,
            foreignType.ToString().ToLowerInvariant(),
            foreignId);
        _tmdbOverview.Delete(overviewsToRemove);
        _tmdbTitle.Delete(titlesToRemove);
    }

    #endregion

    #region Companies (Shared)

    private async Task<bool> UpdateCompanies(IEntityMetadata tmdbEntity, List<ProductionCompany> companies)
    {
        var existingXrefs = _xrefTmdbCompanyEntity.GetByTmdbEntityTypeAndID(tmdbEntity.Type, tmdbEntity.Id)
            .GroupBy(xref => xref.TmdbCompanyID)
            .ToDictionary(xref => xref.Key, groupBy => groupBy.ToList());
        var xrefsToAdd = 0;
        var xrefsToSkip = new HashSet<int>();
        var xrefsToSave = new List<TMDB_Company_Entity>();
        var indexCounter = 0;
        foreach (var company in companies)
        {
            var currentIndex = indexCounter++;
            if (existingXrefs.TryGetValue(company.Id, out var existingXrefList))
            {
                var existingXref = existingXrefList[0];
                if (existingXref.Ordering != currentIndex || existingXref.ReleasedAt != tmdbEntity.ReleasedAt)
                {
                    existingXref.Ordering = currentIndex;
                    existingXref.ReleasedAt = tmdbEntity.ReleasedAt;
                    xrefsToSave.Add(existingXref);
                }
                xrefsToSkip.Add(existingXref.TMDB_Company_EntityID);
            }
            else
            {
                xrefsToAdd++;
                xrefsToSave.Add(new(company.Id, tmdbEntity.Type, tmdbEntity.Id, currentIndex, tmdbEntity.ReleasedAt));
            }

            await UpdateCompany(company);
        }
        var xrefsToRemove = existingXrefs.Values
            .SelectMany(xrefs => xrefs)
            .ExceptBy(xrefsToSkip, o => o.TMDB_Company_EntityID)
            .ToList();

        _logger.LogDebug(
            "Added/updated/removed/skipped {oa}/{ou}/{or}/{os} company cross-references for {type} {EntityTitle} ({EntityType}={EntityId})",
            xrefsToAdd,
            xrefsToSave.Count - xrefsToAdd,
            xrefsToRemove.Count,
            xrefsToSkip.Count + xrefsToAdd - xrefsToSave.Count,
            tmdbEntity.Type.ToString().ToLowerInvariant(),
            tmdbEntity.OriginalTitle,
            tmdbEntity.Type.ToString(),
            tmdbEntity.Id);

        _xrefTmdbCompanyEntity.Save(xrefsToSave);
        foreach (var xref in xrefsToRemove)
        {
            // Delete xref or purge company.
            var xrefs = _xrefTmdbCompanyEntity.GetByTmdbCompanyID(xref.TmdbCompanyID);
            if (xrefs.Count > 1)
                _xrefTmdbCompanyEntity.Delete(xref);
            else
                PurgeCompany(xref.TmdbCompanyID);
        }


        return false;
    }

    private async Task UpdateCompany(ProductionCompany company)
    {
        var tmdbCompany = _tmdbCompany.GetByTmdbCompanyID(company.Id) ?? new(company.Id);
        var updated = tmdbCompany.Populate(company);
        if (updated)
        {
            _logger.LogDebug("Updating studio. (Company={CompanyId})", company.Id);
            _tmdbCompany.Save(tmdbCompany);
        }

        var settings = _settingsProvider.GetSettings();
        if (!string.IsNullOrEmpty(company.LogoPath))
            await _imageService.DownloadImageByType(company.LogoPath, ImageEntityType.Primary, tmdbCompany, isDesired: settings.TMDB.AutoDownloadStudioImages);
    }

    private void PurgeCompany(int companyId)
    {
        var tmdbCompany = _tmdbCompany.GetByTmdbCompanyID(companyId);
        if (tmdbCompany is not null)
        {
            _logger.LogDebug("Removing studio. (Company={CompanyId})", companyId);
            _tmdbCompany.Delete(tmdbCompany);
        }

        _imageService.PurgeImages(tmdbCompany ?? new() { TmdbCompanyID = companyId });

        var xrefs = _xrefTmdbCompanyEntity.GetByTmdbCompanyID(companyId);
        if (xrefs.Count > 0)
        {
            _logger.LogDebug("Removing {count} cross-references for studio. (Company={CompanyId})", xrefs.Count, companyId);
            _xrefTmdbCompanyEntity.Delete(xrefs);
        }
    }

    #endregion

    #region People

    public async Task RepairMissingPeople()
    {
        var missingIds = new HashSet<int>();
        var updateCount = 0;
        var skippedCount = 0;
        var peopleIds = _tmdbPeople.GetAll().Select(person => person.TmdbPersonID).ToHashSet();
        foreach (var person in _tmdbEpisodeCast.GetAll())
            if (!peopleIds.Contains(person.TmdbPersonID)) missingIds.Add(person.TmdbPersonID);
        foreach (var person in _tmdbEpisodeCrew.GetAll())
            if (!peopleIds.Contains(person.TmdbPersonID)) missingIds.Add(person.TmdbPersonID);

        foreach (var person in _tmdbMovieCast.GetAll())
            if (!peopleIds.Contains(person.TmdbPersonID)) missingIds.Add(person.TmdbPersonID);
        foreach (var person in _tmdbMovieCrew.GetAll())
            if (!peopleIds.Contains(person.TmdbPersonID)) missingIds.Add(person.TmdbPersonID);

        _logger.LogDebug("Found {Count} unique missing TMDB People for Episode & Movie staff", missingIds.Count);
        await ProcessWithConcurrencyAsync(_maxConcurrency, missingIds, async personId =>
        {
            var (_, updated) = await UpdatePerson(personId, forceRefresh: true);
            if (updated)
                Interlocked.Increment(ref updateCount);
            else
                Interlocked.Increment(ref skippedCount);
        });

        _logger.LogInformation("Updated missing TMDB People: Found/Updated/Skipped {Found}/{Updated}/{Skipped}",
            missingIds.Count, updateCount, skippedCount);
    }

    public async Task<(bool added, bool updated)> UpdatePerson(int personId, bool forceRefresh = false, bool downloadImages = false, int? currentShowId = null, int? currentMovieId = null)
    {
        using (await GetLockForEntity(DataEntityType.Person, personId, "metadata & images", "Update").ConfigureAwait(false))
        {
            var tmdbPerson = _tmdbPeople.GetByTmdbPersonID(personId) ?? new(personId);
            if (!forceRefresh && tmdbPerson.TMDB_PersonID is not 0 && tmdbPerson.LastUpdatedAt > DateTime.Now.AddHours(-1))
            {
                _logger.LogDebug("Skipping update for staff member. (Person={PersonId})", personId);
                return (false, false);
            }

            _logger.LogDebug("Updating staff member. (Person={PersonId})", personId);
            var methods = PersonMethods.Translations | PersonMethods.ExternalIds;
            if (downloadImages)
                methods |= PersonMethods.Images;
            var newlyAdded = tmdbPerson.TMDB_PersonID is 0;
            Person? person;
            try
            {
                person = await UseClient(c => c.GetPersonAsync(personId, methods), $"Get person {personId}");
            }
            catch (NotFoundException ex)
            {
                // 404 — person was deleted or merged on TMDB.
                _logger.LogWarning(ex, "Staff member not found on TMDB (HTTP 404). Purging local records. (Person={PersonId})", personId);
                await PurgePersonInternal(personId, currentShowId: currentShowId, currentMovieId: currentMovieId);
                return (false, !newlyAdded);
            }
            // Defensive: TMDbLib returned null without throwing — treat the same as a 404.
            if (person is null)
            {
                _logger.LogWarning("Staff member returned null from TMDB API. Purging local records. (Person={PersonId})", personId);
                await PurgePersonInternal(personId, currentShowId: currentShowId, currentMovieId: currentMovieId);
                return (false, !newlyAdded);
            }

            var updated = tmdbPerson.Populate(person);
            if (IsPersonLinkedToOtherEntities(personId) && tmdbPerson.LastOrphanedAt.HasValue)
            {
                tmdbPerson.LastOrphanedAt = null;
                updated = true;
            }

            if (updated)
            {
                tmdbPerson.LastUpdatedAt = DateTime.Now;
                _tmdbPeople.Save(tmdbPerson);
            }

            if (downloadImages)
                await DownloadPersonImages(personId, person.Images!);

            return (newlyAdded, updated);
        }
    }

    private async Task DownloadPersonImages(int personId, ProfileImages images, bool forceDownload = false)
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.TMDB.AutoDownloadStaffImages)
            return;

        _logger.LogDebug("Downloading images for staff member. (Person={personId})", personId);
        if (_tmdbPeople.GetByTmdbPersonID(personId) is not { } person)
            return;

        await _imageService.DownloadImagesByType(null, images.Profiles!, ImageEntityType.Primary, person, settings.TMDB.MaxAutoStaffImages, [], forceDownload);
    }

    public async Task PurgeUnlinkedPeople()
    {
        var linkedPersonIds = GetLinkedPersonIds();
        var people = _tmdbPeople.GetAll().Where(p => !linkedPersonIds.Contains(p.TmdbPersonID)).ToList();
        _logger.LogDebug("Checking {count} orphaned staff members if they should be purged.", people.Count);
        await ProcessWithConcurrencyAsync(_maxConcurrency, people, person => PurgePerson(person.TmdbPersonID));
    }

    public async Task<bool> PurgePerson(int personId, bool force = false)
    {
        using (await GetLockForEntity(DataEntityType.Person, personId, "metadata & images", "Purge"))
        {
            if (!force && IsPersonLinkedToOtherEntities(personId))
                return false;

            if (_tmdbPeople.GetByTmdbPersonID(personId) is { } person)
            {
                if (!person.LastOrphanedAt.HasValue)
                {
                    person.LastOrphanedAt = DateTime.UtcNow;
                    _tmdbPeople.Save(person);
                    _logger.LogDebug("Marked staff member as orphaned. (Person={PersonId})", personId);
                    return false;
                }
                // Keep orphaned people for 7 days before purging.
                if (person.LastOrphanedAt.Value.AddDays(7) > DateTime.UtcNow)
                {
                    _logger.LogDebug("Staff member has not been orphaned for 7 days yet. Skipping. (Person={PersonId})", personId);
                    return false;
                }
            }

            await PurgePersonInternal(personId).ConfigureAwait(false);

            return true;
        }
    }

    internal async Task PurgePersonInternal(int personId, int? currentShowId = null, int? currentMovieId = null)
    {
        var person = _tmdbPeople.GetByTmdbPersonID(personId);
        if (person is not null)
        {
            _logger.LogDebug("Removing staff member. (Person={PersonId})", personId);
            _tmdbPeople.Delete(person);
        }

        _imageService.PurgeImages(person ?? new() { TmdbPersonID = personId });

        var movieCast = _tmdbMovieCast.GetByTmdbPersonID(personId);
        if (movieCast.Count > 0)
        {
            _logger.LogDebug("Removing {count} movie cast roles for staff member. (Person={PersonId})", movieCast.Count, personId);
            _tmdbMovieCast.Delete(movieCast);
        }

        var movieCrew = _tmdbMovieCrew.GetByTmdbPersonID(personId);
        if (movieCrew.Count > 0)
        {
            _logger.LogDebug("Removing {count} movie crew roles for staff member. (Person={PersonId})", movieCrew.Count, personId);
            _tmdbMovieCrew.Delete(movieCrew);
        }

        var episodeCast = _tmdbEpisodeCast.GetByTmdbPersonID(personId);
        if (episodeCast.Count > 0)
        {
            _logger.LogDebug("Removing {count} show cast roles for staff member. (Person={PersonId})", episodeCast.Count, personId);
            _tmdbEpisodeCast.Delete(episodeCast);
        }

        var episodeCrew = _tmdbEpisodeCrew.GetByTmdbPersonID(personId);
        if (episodeCrew.Count > 0)
        {
            _logger.LogDebug("Removing {count} show crew roles for staff member. (Person={PersonId})", episodeCrew.Count, personId);
            _tmdbEpisodeCrew.Delete(episodeCrew);
        }

        var showIds = new HashSet<int>([
            ..episodeCast.Select(x => x.TmdbShowID),
            ..episodeCrew.Select(x => x.TmdbShowID),
        ]);
        if (currentShowId.HasValue)
            showIds.Remove(currentShowId.Value);
        if (showIds.Count > 0)
        {
            _logger.LogDebug("Scheduling {count} shows to be updated. (Person={PersonId})", showIds.Count, personId);
            foreach (var showId in showIds)
                await ScheduleUpdateOfShow(new() { ShowId = showId, DownloadCrewAndCast = true });
        }

        var movieIds = new HashSet<int>([
            ..movieCast.Select(x => x.TmdbMovieID),
            ..movieCrew.Select(x => x.TmdbMovieID),
        ]);
        if (currentMovieId.HasValue)
            movieIds.Remove(currentMovieId.Value);
        if (movieIds.Count > 0)
        {
            _logger.LogDebug("Scheduling {count} movies to be updated. (Person={PersonId})", movieIds.Count, personId);
            foreach (var movieId in movieIds)
                await ScheduleUpdateOfMovie(new() { MovieId = movieId, DownloadCrewAndCast = true });
        }
    }

    /// <summary>
    /// Returns the set of all person IDs currently referenced by at least one cast or crew record
    /// across movies and episodes. Used as the single source of truth when deciding which
    /// <see cref="TMDB_Person"/> records are safe to purge, avoiding the old pattern of issuing
    /// up to 4 DB queries per person to check linkage.
    /// </summary>
    private HashSet<int> GetLinkedPersonIds()
    {
        var ids = new HashSet<int>();
        ids.UnionWith(_tmdbMovieCast.GetAll().Select(x => x.TmdbPersonID));
        ids.UnionWith(_tmdbMovieCrew.GetAll().Select(x => x.TmdbPersonID));
        ids.UnionWith(_tmdbEpisodeCast.GetAll().Select(x => x.TmdbPersonID));
        ids.UnionWith(_tmdbEpisodeCrew.GetAll().Select(x => x.TmdbPersonID));
        return ids;
    }

    private bool IsPersonLinkedToOtherEntities(int tmdbPersonId)
    {
        var movieCastLinks = _tmdbMovieCast.GetByTmdbPersonID(tmdbPersonId);
        if (movieCastLinks.Any())
            return true;

        var movieCrewLinks = _tmdbMovieCrew.GetByTmdbPersonID(tmdbPersonId);
        if (movieCrewLinks.Any())
            return true;

        var episodeCastLinks = _tmdbEpisodeCast.GetByTmdbPersonID(tmdbPersonId);
        if (episodeCastLinks.Any())
            return true;

        var episodeCrewLinks = _tmdbEpisodeCrew.GetByTmdbPersonID(tmdbPersonId);
        if (episodeCrewLinks.Any())
            return true;

        return false;
    }

    #endregion

    #endregion

    #region Helpers (Shared)

    private static async Task ProcessWithConcurrencyAsync<T>(
        int maxConcurrent,
        IEnumerable<T> enumerable,
        Func<T, Task> processAsync,
        Action<T>? onDropped = null
    )
    {
        if (maxConcurrent < 1)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrent), "Concurrency level must be at least 1.");

        var exceptions = new ConcurrentBag<Exception>();
        using var cts = new CancellationTokenSource();
        var failureCount = 0;
        var block = new ActionBlock<T>(
            async item =>
            {
                if (cts.IsCancellationRequested) { onDropped?.Invoke(item); return; }
                try { await processAsync(item); }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    if (Interlocked.Increment(ref failureCount) >= maxConcurrent)
                        await cts.CancelAsync();
                }
            },
            new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = maxConcurrent }
        );
        foreach (var item in enumerable)
            block.Post(item);
        block.Complete();
        await block.Completion.ConfigureAwait(false);
        if (!exceptions.IsEmpty)
            throw new AggregateException(exceptions);
    }

    #endregion

    #region External IDs (Shared)

    /// <summary>
    /// Update TvDB ID for the TMDB show if needed and the ID is available.
    /// </summary>
    /// <param name="show">TMDB Show.</param>
    /// <param name="externalIds">External IDs.</param>
    /// <returns>Indicates that the ID was updated.</returns>
    private bool UpdateShowExternalIDs(TMDB_Show show, ExternalIdsTvShow externalIds)
    {
        if (string.IsNullOrEmpty(externalIds.TvdbId))
        {
            if (!show.TvdbShowID.HasValue)
                return false;

            show.TvdbShowID = null;
            return true;
        }

        if (!int.TryParse(externalIds.TvdbId, out var tvdbId) || tvdbId <= 0 || show.TvdbShowID == tvdbId)
            return false;

        show.TvdbShowID = tvdbId;
        return true;
    }

    /// <summary>
    /// Update TvDB ID for the TMDB episode if needed and the ID is available.
    /// </summary>
    /// <param name="episode">TMDB Episode.</param>
    /// <param name="externalIds">External IDs.</param>
    /// <returns>Indicates that the ID was updated.</returns>
    private bool UpdateEpisodeExternalIDs(TMDB_Episode episode, ExternalIdsTvEpisode externalIds)
    {
        if (string.IsNullOrEmpty(externalIds.TvdbId))
        {
            if (!episode.TvdbEpisodeID.HasValue)
                return false;

            episode.TvdbEpisodeID = null;
            return true;
        }

        if (!int.TryParse(externalIds.TvdbId, out var tvdbId) || tvdbId <= 0 || episode.TvdbEpisodeID == tvdbId)
            return false;

        episode.TvdbEpisodeID = tvdbId;
        return true;
    }

    /// <summary>
    /// Update IMDb ID for the TMDB movie if needed and the ID is available.
    /// </summary>
    /// <param name="movie">TMDB Movie.</param>
    /// <param name="externalIds">External IDs.</param>
    /// <returns>Indicates that the ID was updated.</returns>
    private bool UpdateMovieExternalIDs(TMDB_Movie movie, ExternalIdsMovie externalIds)
    {
        if (movie.ImdbMovieID == externalIds.ImdbId)
            return false;

        movie.ImdbMovieID = externalIds.ImdbId;
        return true;
    }

    #endregion

    #region Locking


    private bool WaitIfEntityLocked(DataEntityType entityType, int id, string metadataKey)
        => _entityLock.WaitIfEntityLocked(entityType, id, metadataKey);

    private Task<bool> WaitIfEntityLockedAsync(DataEntityType entityType, int id, string metadataKey)
        => _entityLock.WaitIfEntityLockedAsync(entityType, id, metadataKey);

    private bool IsEntityLocked(DataEntityType entityType, int id, string metadataKey)
        => _entityLock.IsEntityLocked(entityType, id, metadataKey);

    private Task<IDisposable> GetLockForEntity(DataEntityType entityType, int id, string metadataKey, string reason)
        => _entityLock.GetLockForEntityAsync(entityType, id, metadataKey, reason);

    #endregion

    #region Changes (Shared)

    /// <summary>
    /// Returns <see langword="true"/> if TMDB has recorded any changes for the given movie since
    /// <paramref name="since"/>, or if the TMDB Changes API 14-day window has been exceeded (in
    /// which case a full refresh is required because the history is no longer available).
    /// </summary>
    private async Task<bool> HasMovieChangedSinceAsync(int movieId, DateTime since)
    {
        var changesWindowDays = _settingsProvider.GetSettings().TMDB.IncrementalChangesWindowDays;
        if (changesWindowDays is 0)
            return true;

        // The TMDB Changes API covers at most the last 14 days. Treat anything older as changed
        // so the caller always falls through to a full refresh when history is unavailable.
        var sinceUtc = since.ToUniversalTime();
        if (sinceUtc < DateTime.UtcNow.AddDays(-changesWindowDays))
            return true;
        var changes = await UseClient(c => c.GetMovieChangesAsync(movieId, 0, sinceUtc, null), $"Get changes for movie {movieId}").ConfigureAwait(false);
        return changes is null || changes.Count > 0;
    }

    internal readonly record struct TmdbShowChangedItems(HashSet<int> SeasonNumbers, HashSet<(int Season, int Episode)> Episodes);

    private readonly record struct TmdbSeasonEpisodeCounts(int EpisodeCount, int HiddenEpisodeCount);

    private readonly record struct TmdbSeasonEpisodeUpdateResult(
        bool EpisodesOrSeasonsUpdated,
        Dictionary<TMDB_Season, UpdateReason> UpdatedSeasons,
        Dictionary<TMDB_Episode, UpdateReason> UpdatedEpisodes,
        int EpisodeCount,
        int HiddenEpisodeCount);

    private sealed class ShowSyncState
    {
        public required bool DownloadCrewAndCast { get; init; }
        public required bool QuickRefresh { get; init; }
        public required bool ShouldFireEvents { get; init; }
        public required HashSet<TitleLanguage>? PreferredTitleLanguages { get; init; }
        public required HashSet<TitleLanguage>? PreferredOverviewLanguages { get; init; }
        public required TmdbShowChangedItems? ChangedItems { get; init; }

        public int SeasonsAdded;
        public int EpisodesAdded;
        public int TotalEpisodeCount;
        public int TotalHiddenEpisodeCount;
        public readonly HashSet<int> SeasonsToSkip = [];
        public readonly List<TMDB_Season> SeasonsToSave = [];
        public readonly Dictionary<TMDB_Season, UpdateReason> SeasonEvents = [];
        public readonly HashSet<int> EpisodesToSkip = [];
        public readonly List<TMDB_Episode> EpisodesToSave = [];
        public readonly Dictionary<TMDB_Episode, UpdateReason> EpisodeEvents = [];
        public readonly HashSet<int> PeopleToAddOrKeep = [];
        public readonly HashSet<int> PeopleToPotentiallyRemove = [];
    }

    /// <summary>
    /// Fetches the set of changed seasons and episodes for the given show from the TMDB Changes
    /// API since <paramref name="since"/>, for use as a selective-refresh filter.
    /// </summary>
    /// <returns>
    /// <para><see langword="null"/> if the 14-day Changes API window has been exceeded or the API
    /// call failed — the caller should fall back to a full refresh of all seasons and episodes.</para>
    /// <para>Empty sets if no season or episode changes were reported — the caller can skip the update.</para>
    /// <para>Populated sets containing the season numbers and (season, episode) pairs that changed.</para>
    /// </returns>
    private async Task<TmdbShowChangedItems?> GetShowChangedItemsAsync(int showId, DateTime since)
    {
        var changesWindowDays = _settingsProvider.GetSettings().TMDB.IncrementalChangesWindowDays;
        if (changesWindowDays is 0)
            return null;

        // The TMDB Changes API covers at most the last 14 days. Return null to signal the caller
        // to perform a full refresh rather than a selective one.
        var sinceUtc = since.ToUniversalTime();
        if (sinceUtc < DateTime.UtcNow.AddDays(-changesWindowDays))
            return null;
        var changes = await UseClient(c => c.GetTvShowChangesAsync(showId, 0, sinceUtc, null), $"Get changes for show {showId}").ConfigureAwait(false);
        if (changes is null)
            return null;
        return ParseShowChanges(changes);
    }

    /// <summary>
    /// Parses a TMDB Changes API response into the sets of season numbers and (season, episode)
    /// pairs that changed, for use as a selective-refresh filter in
    /// <see cref="UpdateShowSeasonsAndEpisodes"/>.
    /// </summary>
    /// <remarks>
    /// Only <c>"episode"</c> and <c>"season"</c> change keys are processed; all other keys
    /// (e.g. <c>"name"</c>, <c>"overview"</c>) are ignored. Episode changes also implicitly add
    /// their season number to <c>SeasonNumbers</c> so season metadata is always refreshed when
    /// one of its episodes changed.
    /// </remarks>
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

    private static void ApplyChangeItem(string key, ChangeItemBase item, HashSet<int> seasonNumbers, HashSet<(int Season, int Episode)> episodes)
    {
        // Each change item type stores its payload differently; normalise to a JObject.
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

        // For episode changes, also record the specific (season, episode) pair so the
        // fast-path can skip episodes within the season that were not individually changed.
        if (key != "episode")
            return;

        var episodeNumber = value["episode_number"]?.Value<int>();
        if (episodeNumber.HasValue)
            episodes.Add((seasonNumber.Value, episodeNumber.Value));
    }

    #endregion
}
