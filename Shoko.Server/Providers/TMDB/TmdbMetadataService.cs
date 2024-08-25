using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.RateLimit;
using Polly.Retry;
using Quartz;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using TMDbLib.Client;
using TMDbLib.Objects.Collections;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.People;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.TvShows;

using TitleLanguage = Shoko.Plugin.Abstractions.DataModels.TitleLanguage;
using MovieCredits = TMDbLib.Objects.Movies.Credits;

// Suggestions we don't need in this file.
#pragma warning disable CA1822
#pragma warning disable CA1826

#nullable enable
namespace Shoko.Server.Providers.TMDB;

public class TmdbMetadataService
{
    private static TmdbMetadataService? _instance = null;

    private static string? _imageServerUrl = null;

    public static string? ImageServerUrl
    {
        get
        {
            // Return cached version if possible.
            if (_imageServerUrl is not null)
                return _imageServerUrl;
            if (_instance is null)
                return null;
            try
            {
                var config = _instance.UseClient(c => c.GetAPIConfiguration()).Result;
                return _imageServerUrl = config.Images.SecureBaseUrl;
            }
            catch (Exception ex)
            {
                _instance._logger.LogError(ex, "Encountered an exception while trying to find the image server url to use; {ErrorMessage}", ex.Message);
                throw;
            }
        }
    }

    private readonly ILogger<TmdbMetadataService> _logger;

    private readonly ISchedulerFactory _schedulerFactory;

    private readonly ISettingsProvider _settingsProvider;

    private readonly TmdbImageService _imageService;

    private readonly TmdbLinkingService _linkingService;

    private readonly AnimeSeriesRepository _animeSeries;

    private readonly TMDB_AlternateOrderingRepository _tmdbAlternateOrdering;

    private readonly TMDB_AlternateOrdering_EpisodeRepository _tmdbAlternateOrderingEpisodes;

    private readonly TMDB_AlternateOrdering_SeasonRepository _tmdbAlternateOrderingSeasons;

    private readonly TMDB_CollectionRepository _tmdbCollections;

    private readonly TMDB_CompanyRepository _tmdbCompany;

    private readonly TMDB_EpisodeRepository _tmdbEpisodes;

    private readonly TMDB_Episode_CastRepository _tmdbEpisodeCast;

    private readonly TMDB_Episode_CrewRepository _tmdbEpisodeCrew;

    private readonly TMDB_ImageRepository _tmdbImages;

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
            : throw new Exception("You need to provide an api key before using the TMDB provider!")
    ))
    {
        MaxRetryCount = 5,
    };

    private static readonly TimeSpan[] _retryTimeSpans = [TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(40)];

    private readonly AsyncRetryPolicy _retryPolicy = Policy
        .Handle<TaskCanceledException>()
        .WaitAndRetryAsync(_retryTimeSpans);

    private readonly AsyncRateLimitPolicy _rateLimitPolicy = Policy
        .RateLimitAsync(40, TimeSpan.FromSeconds(10));

    protected Task<T> UseClient<T>(Func<TMDbClient, Task<T>> func) =>
        _retryPolicy.ExecuteAsync<T>(() => _rateLimitPolicy.ExecuteAsync<T>(() => func(CachedClient)));

    public TmdbMetadataService(
        ILogger<TmdbMetadataService> logger,
        ISchedulerFactory commandFactory,
        ISettingsProvider settingsProvider,
        TmdbImageService imageService,
        TmdbLinkingService linkingService,
        AnimeSeriesRepository animeSeries,
        TMDB_AlternateOrderingRepository tmdbAlternateOrdering,
        TMDB_AlternateOrdering_EpisodeRepository tmdbAlternateOrderingEpisodes,
        TMDB_AlternateOrdering_SeasonRepository tmdbAlternateOrderingSeasons,
        TMDB_CollectionRepository tmdbCollections,
        TMDB_CompanyRepository tmdbCompany,
        TMDB_EpisodeRepository tmdbEpisodes,
        TMDB_Episode_CastRepository tmdbEpisodeCast,
        TMDB_Episode_CrewRepository tmdbEpisodeCrew,
        TMDB_ImageRepository tmdbImages,
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
        _schedulerFactory = commandFactory;
        _settingsProvider = settingsProvider;
        _imageService = imageService;
        _linkingService = linkingService;
        _animeSeries = animeSeries;
        _tmdbAlternateOrdering = tmdbAlternateOrdering;
        _tmdbAlternateOrderingEpisodes = tmdbAlternateOrderingEpisodes;
        _tmdbAlternateOrderingSeasons = tmdbAlternateOrderingSeasons;
        _tmdbCollections = tmdbCollections;
        _tmdbCompany = tmdbCompany;
        _tmdbEpisodes = tmdbEpisodes;
        _tmdbEpisodeCast = tmdbEpisodeCast;
        _tmdbEpisodeCrew = tmdbEpisodeCrew;
        _tmdbImages = tmdbImages;
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
        _instance ??= this;
    }

    public async Task ScheduleSearchForMatch(int anidbId, bool force)
    {
        await (await _schedulerFactory.GetScheduler()).StartJob<SearchTmdbJob>(c =>
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
        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var ser in allSeries)
        {
            if (ser.IsTMDBAutoMatchingDisabled)
                continue;

            var anime = ser.AniDB_Anime;
            if (anime == null)
                continue;

            if (anime.Restricted > 0 && !settings.TMDB.AutoLinkRestricted)
                continue;

            if (anime.TmdbMovieCrossReferences is { Count: > 0 })
                continue;

            if (anime.TmdbShowCrossReferences is { Count: > 0 })
                continue;

            _logger.LogTrace("Found anime without TMDB association: {MainTitle}", anime.MainTitle);

            await scheduler.StartJob<SearchTmdbJob>(c => c.AnimeID = ser.AniDB_ID);
        }
    }

    #region Movies

    #region Search

    public async Task<(List<SearchMovie> Page, int TotalCount)> SearchMovies(string query, bool includeRestricted = false, int year = 0, int page = 1, int pageSize = 6)
    {
        var results = new List<SearchMovie>();
        var firstPage = await UseClient(c => c.SearchMovieAsync(query, 1, includeRestricted, year)).ConfigureAwait(false);
        var total = firstPage.TotalResults;
        if (total == 0)
            return (results, total);

        var lastPage = firstPage.TotalPages;
        var actualPageSize = firstPage.Results.Count;
        var startIndex = (page - 1) * pageSize;
        var startPage = (int)Math.Floor((decimal)startIndex / actualPageSize) + 1;
        var endIndex = Math.Min(startIndex + pageSize, total);
        var endPage = total == endIndex ? lastPage : Math.Min((int)Math.Floor((decimal)endIndex / actualPageSize) + (endIndex % actualPageSize > 0 ? 1 : 0), lastPage);
        for (var i = startPage; i <= endPage; i++)
        {
            var actualPage = await UseClient(c => c.SearchMovieAsync(query, i, includeRestricted, year)).ConfigureAwait(false);
            results.AddRange(actualPage.Results);
        }

        var skipCount = startIndex - (startPage - 1) * actualPageSize;
        var pagedResults = results.Skip(skipCount).Take(pageSize).ToList();

        _logger.LogTrace(
            "Got {Count} movies from {Results} total movies at {IndexRange} across {PageRange}.",
            pagedResults.Count,
            total,
            startIndex == endIndex ? $"index {startIndex}" : $"indexes {startIndex}-{endIndex}",
            startPage == endPage ? $"{startPage} actual page" : $"{startPage}-{endPage} actual pages"
        );

        return (pagedResults, total);
    }

    #endregion

    #region Update

    public async Task UpdateAllMovies(bool force, bool saveImages)
    {
        var allXRefs = _xrefAnidbTmdbMovies.GetAll();
        _logger.LogInformation("Scheduling {Count} movies to be updated.", allXRefs.Count);
        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var xref in allXRefs)
            await scheduler.StartJob<UpdateTmdbMovieJob>(
                c =>
                {
                    c.TmdbMovieID = xref.TmdbMovieID;
                    c.ForceRefresh = force;
                    c.DownloadImages = saveImages;
                }
            ).ConfigureAwait(false);
    }

    public async Task ScheduleUpdateOfMovie(int movieId, bool forceRefresh = false, bool downloadImages = false, bool? downloadCrewAndCast = null, bool? downloadCollections = null)
    {
        // Schedule the movie info to be downloaded or updated.
        await (await _schedulerFactory.GetScheduler().ConfigureAwait(false)).StartJob<UpdateTmdbMovieJob>(c =>
        {
            c.TmdbMovieID = movieId;
            c.ForceRefresh = forceRefresh;
            c.DownloadImages = downloadImages;
            c.DownloadCrewAndCast = downloadCrewAndCast;
            c.DownloadCollections = downloadCollections;
        }).ConfigureAwait(false);
    }

    public async Task<bool> UpdateMovie(int movieId, bool forceRefresh = false, bool downloadImages = false, bool downloadCrewAndCast = false, bool downloadCollections = false)
    {
        // Abort if we're within a certain time frame as to not try and get us rate-limited.
        var tmdbMovie = _tmdbMovies.GetByTmdbMovieID(movieId) ?? new(movieId);
        var newlyAdded = tmdbMovie.TMDB_MovieID == 0;
        if (!forceRefresh && tmdbMovie.CreatedAt != tmdbMovie.LastUpdatedAt && tmdbMovie.LastUpdatedAt > DateTime.Now.AddHours(-1))
            return false;

        // Abort if we couldn't find the movie by id.
        var methods = MovieMethods.Translations | MovieMethods.ReleaseDates;
        if (downloadCrewAndCast)
            methods |= MovieMethods.Credits;
        var movie = await UseClient(c => c.GetMovieAsync(movieId, "en-US", null, methods)).ConfigureAwait(false);
        if (movie == null)
            return false;

        var settings = _settingsProvider.GetSettings();
        var preferredTitleLanguages = settings.TMDB.DownloadAllTitles ? null : Languages.PreferredNamingLanguages.Select(a => a.Language).ToHashSet();
        var preferredOverviewLanguages = settings.TMDB.DownloadAllOverviews ? null : Languages.PreferredDescriptionNamingLanguages.Select(a => a.Language).ToHashSet();

        var updated = tmdbMovie.Populate(movie);
        var (titlesUpdated, overviewsUpdated) = UpdateTitlesAndOverviewsWithTuple(tmdbMovie, movie.Translations, preferredTitleLanguages, preferredOverviewLanguages);
        updated = titlesUpdated || overviewsUpdated || updated;
        updated = await UpdateMovieExternalIDs(tmdbMovie) || updated;
        updated = await UpdateCompanies(tmdbMovie, movie.ProductionCompanies) || updated;
        if (downloadCrewAndCast)
            updated = await UpdateMovieCastAndCrew(tmdbMovie, movie.Credits, downloadImages) || updated;
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

    private async Task<bool> UpdateMovieCastAndCrew(TMDB_Movie tmdbMovie, MovieCredits credits, bool downloadImages)
    {
        var peopleToAdd = 0;
        var peopleToSave = new List<TMDB_Person>();
        var knownPeopleDict = new Dictionary<int, TMDB_Person>();

        var counter = 0;
        var castToAdd = 0;
        var castToKeep = new HashSet<string>();
        var castToSave = new List<TMDB_Movie_Cast>();
        var existingCastDict = _tmdbMovieCast.GetByTmdbMovieID(tmdbMovie.Id)
            .ToDictionary(cast => cast.TmdbCreditID);
        foreach (var cast in credits.Cast)
        {
            var ordering = counter++;
            castToKeep.Add(cast.CreditId);
            if (!knownPeopleDict.TryGetValue(cast.Id, out var tmdbPerson))
            {
                var person = await UseClient(c => c.GetPersonAsync(cast.Id, PersonMethods.Translations)).ConfigureAwait(false) ??
                    throw new Exception($"Unable to get TMDB Person with id {cast.Id}. (Movie={tmdbMovie.Id},Person={cast.Id})");

                tmdbPerson = _tmdbPeople.GetByTmdbPersonID(cast.Id);
                if (tmdbPerson == null)
                {
                    tmdbPerson = new(cast.Id);
                    peopleToAdd++;
                }
                if (tmdbPerson.Populate(person))
                {
                    tmdbPerson.LastUpdatedAt = DateTime.Now;
                    peopleToSave.Add(tmdbPerson);
                }
                if (downloadImages)
                    await DownloadPersonImages(tmdbPerson.Id);

                knownPeopleDict.Add(cast.Id, tmdbPerson);
            }

            var roleUpdated = false;
            if (!existingCastDict.TryGetValue(cast.CreditId, out var role))
            {
                role = new()
                {
                    TmdbMovieID = tmdbMovie.Id,
                    TmdbPersonID = tmdbPerson.Id,
                    TmdbCreditID = cast.CreditId,
                };
                castToAdd++;
                roleUpdated = true;
            }

            if (role.CharacterName != cast.Character)
            {
                role.CharacterName = cast.Character;
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
        foreach (var crew in credits.Crew)
        {
            crewToKeep.Add(crew.CreditId);
            if (!knownPeopleDict.TryGetValue(crew.Id, out var tmdbPerson))
            {
                var person = await UseClient(c => c.GetPersonAsync(crew.Id, PersonMethods.Translations)).ConfigureAwait(false) ??
                    throw new Exception($"Unable to get TMDB Person with id {crew.Id}. (Movie={tmdbMovie.Id},Person={crew.Id})");

                tmdbPerson = _tmdbPeople.GetByTmdbPersonID(crew.Id);
                if (tmdbPerson == null)
                {
                    tmdbPerson = new(crew.Id);
                    peopleToAdd++;
                }
                if (tmdbPerson.Populate(person))
                {
                    tmdbPerson.LastUpdatedAt = DateTime.Now;
                    peopleToSave.Add(tmdbPerson);
                }
                if (downloadImages)
                    await DownloadPersonImages(tmdbPerson.Id);

                knownPeopleDict.Add(crew.Id, tmdbPerson);
            }

            var roleUpdated = false;
            if (!existingCrewDict.TryGetValue(crew.CreditId, out var role))
            {
                role = new()
                {
                    TmdbMovieID = tmdbMovie.Id,
                    TmdbPersonID = tmdbPerson.Id,
                    TmdbCreditID = crew.CreditId,
                };
                crewToAdd++;
                roleUpdated = true;
            }

            if (role.Department != crew.Department)
            {
                role.Department = crew.Department;
                roleUpdated = true;
            }

            if (role.Job != crew.Job)
            {
                role.Job = crew.Job;
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

        _tmdbPeople.Save(peopleToSave);
        _tmdbMovieCast.Save(castToSave);
        _tmdbMovieCrew.Save(crewToSave);
        _tmdbMovieCast.Delete(castToRemove);
        _tmdbMovieCrew.Delete(crewToRemove);

        var peopleToRemove = 0;
        var peopleToCheck = existingCastDict.Values
            .Select(cast => cast.TmdbPersonID)
            .Concat(existingCrewDict.Values.Select(crew => crew.TmdbPersonID))
            .Except(knownPeopleDict.Keys)
            .ToHashSet();
        foreach (var personId in peopleToCheck)
        {
            if (IsPersonLinkedToOtherEntities(personId))
                continue;

            PurgePerson(personId);
            peopleToRemove++;
        }

        _logger.LogDebug(
            "Added/updated/removed/skipped {aa}/{au}/{ar}/{as} cast and {ra}/{ru}/{rr}/{rs} crew across {pa}/{pu}/{pr}/{ps} people for movie {MovieTitle} (Movie={MovieId})",
            castToAdd,
            castToSave.Count - castToAdd,
            castToRemove.Count,
            existingCastDict.Count - (castToSave.Count - castToAdd),
            crewToAdd,
            crewToSave.Count - crewToAdd,
            crewToRemove.Count,
            existingCrewDict.Count - (crewToSave.Count - crewToAdd),
            peopleToAdd,
            peopleToSave.Count - peopleToAdd,
            peopleToRemove,
            knownPeopleDict.Count - (peopleToSave.Count - peopleToAdd),
            tmdbMovie.EnglishTitle,
            tmdbMovie.Id
            );
        return castToSave.Count > 0 ||
            castToRemove.Count > 0 ||
            crewToSave.Count > 0 ||
            crewToRemove.Count > 0 ||
            peopleToSave.Count > 0 ||
            peopleToRemove > 0;
    }

    private async Task UpdateMovieCollections(Movie movie)
    {
        if (movie.BelongsToCollection?.Id is not { } collectionId)
        {
            CleanupMovieCollection(movie.Id);
            return;
        }

        var movieXRefs = _xrefTmdbCollectionMovies.GetByTmdbCollectionID(collectionId);
        var tmdbCollection = _tmdbCollections.GetByTmdbCollectionID(collectionId) ?? new(collectionId);
        var collection = await UseClient(c => c.GetCollectionAsync(collectionId, CollectionMethods.Images | CollectionMethods.Translations)).ConfigureAwait(false);
        if (collection == null)
        {
            PurgeMovieCollection(collectionId);
            return;
        }

        var settings = _settingsProvider.GetSettings();
        var preferredTitleLanguages = settings.TMDB.DownloadAllTitles ? null : Languages.PreferredNamingLanguages.Select(a => a.Language).ToHashSet();
        var preferredOverviewLanguages = settings.TMDB.DownloadAllOverviews ? null : Languages.PreferredDescriptionNamingLanguages.Select(a => a.Language).ToHashSet();

        var updated = tmdbCollection.Populate(collection);
        updated = UpdateTitlesAndOverviews(tmdbCollection, collection.Translations, preferredTitleLanguages, preferredOverviewLanguages) || updated;

        var xrefsToAdd = 0;
        var xrefsToSave = new List<TMDB_Collection_Movie>();
        var xrefsToRemove = movieXRefs.Where(xref => !collection.Parts.Any(part => xref.TmdbMovieID == part.Id)).ToList();
        var movieXref = movieXRefs.FirstOrDefault(xref => xref.TmdbMovieID == movie.Id);
        var index = collection.Parts.FindIndex(part => part.Id == movie.Id);
        if (index == -1)
            index = collection.Parts.Count;
        if (movieXref == null)
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

    public async Task ScheduleDownloadAllMovieImages(int movieId, bool forceDownload = false)
    {
        // Schedule the movie info to be downloaded or updated.
        await (await _schedulerFactory.GetScheduler().ConfigureAwait(false)).StartJob<DownloadTmdbMovieImagesJob>(c =>
        {
            c.TmdbMovieID = movieId;
            c.ForceDownload = forceDownload;
        }).ConfigureAwait(false);
    }

    public async Task DownloadAllMovieImages(int movieId, bool forceDownload = false)
    {
        var tmdbMovie = _tmdbMovies.GetByTmdbMovieID(movieId);
        if (tmdbMovie is null)
            return;

        await DownloadMovieImages(movieId, tmdbMovie.OriginalLanguage, forceDownload);
    }

    public async Task DownloadMovieImages(int movieId, TitleLanguage? mainLanguage = null, bool forceDownload = false)
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.TMDB.AutoDownloadPosters && !settings.TMDB.AutoDownloadLogos && !settings.TMDB.AutoDownloadBackdrops)
            return;

        var images = await UseClient(c => c.GetMovieImagesAsync(movieId)).ConfigureAwait(false);
        var languages = GetLanguages(mainLanguage);
        if (settings.TMDB.AutoDownloadPosters)
            await _imageService.DownloadImagesByType(images.Posters, ImageEntityType.Poster, ForeignEntityType.Movie, movieId, settings.TMDB.MaxAutoPosters, languages, forceDownload);
        if (settings.TMDB.AutoDownloadLogos)
            await _imageService.DownloadImagesByType(images.Logos, ImageEntityType.Logo, ForeignEntityType.Movie, movieId, settings.TMDB.MaxAutoLogos, languages, forceDownload);
        if (settings.TMDB.AutoDownloadBackdrops)
            await _imageService.DownloadImagesByType(images.Backdrops, ImageEntityType.Backdrop, ForeignEntityType.Movie, movieId, settings.TMDB.MaxAutoBackdrops, languages, forceDownload);
    }

    #endregion

    #region Purge

    public async Task PurgeAllUnusedMovies()
    {
        var allMovies = _tmdbMovies.GetAll().Select(movie => movie.TmdbMovieID)
            .Concat(_tmdbImages.GetAll().Where(image => image.TmdbMovieID.HasValue).Select(image => image.TmdbMovieID!.Value))
            .Concat(_xrefAnidbTmdbMovies.GetAll().Select(xref => xref.TmdbMovieID))
            .Concat(_xrefTmdbCompanyEntity.GetAll().Where(x => x.TmdbEntityType == ForeignEntityType.Movie).Select(x => x.TmdbEntityID))
            .Concat(_tmdbMovieCast.GetAll().Select(x => x.TmdbMovieID))
            .Concat(_tmdbMovieCrew.GetAll().Select(x => x.TmdbMovieID))
            .Concat(_tmdbCollections.GetAll().Select(collection => collection.TmdbCollectionID))
            .Concat(_xrefTmdbCollectionMovies.GetAll().Select(collectionMovie => collectionMovie.TmdbMovieID))
            .ToHashSet();
        var toKeep = _xrefAnidbTmdbMovies.GetAll()
            .Select(xref => xref.TmdbMovieID)
            .ToHashSet();
        var toBePurged = allMovies
            .Except(toKeep)
            .ToHashSet();

        _logger.LogInformation("Scheduling {Count} out of {AllCount} movies to be purged.", toBePurged.Count, allMovies.Count);
        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var movieID in toBePurged)
            await scheduler.StartJob<PurgeTmdbMovieJob>(c => c.TmdbMovieID = movieID);
    }

    public async Task SchedulePurgeOfMovie(int movieId, bool removeImageFiles = true)
    {
        await (await _schedulerFactory.GetScheduler().ConfigureAwait(false)).StartJob<PurgeTmdbMovieJob>(c =>
        {
            c.TmdbMovieID = movieId;
            c.RemoveImageFiles = removeImageFiles;
        });
    }

    /// <summary>
    /// Purge a TMDB movie from the local database.
    /// </summary>
    /// <param name="movieId">TMDB Movie ID.</param>
    /// <param name="removeImageFiles">Remove image files.</param>
    public async Task PurgeMovie(int movieId, bool removeImageFiles = true)
    {
        await _linkingService.RemoveAllMovieLinksForMovie(movieId);

        _imageService.PurgeImages(ForeignEntityType.Movie, movieId, removeImageFiles);

        var movie = _tmdbMovies.GetByTmdbMovieID(movieId);
        if (movie != null)
        {
            _logger.LogTrace("Removing movie {MovieName} (Movie={MovieID})", movie.OriginalTitle, movie.Id);
            _tmdbMovies.Delete(movie);
        }

        PurgeMovieCompanies(movieId, removeImageFiles);

        PurgeMovieCastAndCrew(movieId, removeImageFiles);

        CleanupMovieCollection(movieId);

        PurgeTitlesAndOverviews(ForeignEntityType.Movie, movieId);
    }

    private void PurgeMovieCompanies(int movieId, bool removeImageFiles = true)
    {
        var xrefsToRemove = _xrefTmdbCompanyEntity.GetByTmdbEntityTypeAndID(ForeignEntityType.Movie, movieId);
        foreach (var xref in xrefsToRemove)
        {
            // Delete xref or purge company.
            var xrefs = _xrefTmdbCompanyEntity.GetByTmdbCompanyID(xref.TmdbCompanyID);
            if (xrefs.Count > 1)
                _xrefTmdbCompanyEntity.Delete(xref);
            else
                PurgeCompany(xref.TmdbCompanyID, removeImageFiles);
        }
    }

    private void PurgeMovieCastAndCrew(int movieId, bool removeImageFiles = true)
    {
        var castMembers = _tmdbMovieCast.GetByTmdbMovieID(movieId);
        var crewMembers = _tmdbMovieCrew.GetByTmdbMovieID(movieId);

        _tmdbMovieCast.Delete(castMembers);
        _tmdbMovieCrew.Delete(crewMembers);

        var allPeopleSet = castMembers
            .Select(c => c.TmdbPersonID)
            .Concat(crewMembers.Select(c => c.TmdbPersonID))
            .Distinct()
            .ToHashSet();
        foreach (var personId in allPeopleSet)
            if (!IsPersonLinkedToOtherEntities(personId))
                PurgePerson(personId, removeImageFiles);
    }

    private void CleanupMovieCollection(int movieId, bool removeImageFiles = true)
    {
        var xref = _xrefTmdbCollectionMovies.GetByTmdbMovieID(movieId);
        if (xref == null)
            return;

        var allXRefs = _xrefTmdbCollectionMovies.GetByTmdbCollectionID(xref.TmdbCollectionID);
        if (allXRefs.Count > 1)
            _xrefTmdbCollectionMovies.Delete(xref);
        else
            PurgeMovieCollection(xref.TmdbCollectionID, removeImageFiles);
    }

    private void PurgeMovieCollection(int collectionId, bool removeImageFiles = true)
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

        _imageService.PurgeImages(ForeignEntityType.Collection, collectionId, removeImageFiles);

        PurgeTitlesAndOverviews(ForeignEntityType.Collection, collectionId);

        if (collection != null)
        {
            _logger.LogTrace(
                "Removing movie collection {CollectionName} (Collection={CollectionID})",
                collection.EnglishTitle,
                collectionId
            );
            _tmdbCollections.Delete(collection);
        }
    }

    #endregion

    #endregion

    #region Show

    #region Search

    public async Task<(List<SearchTv> Page, int TotalCount)> SearchShows(string query, bool includeRestricted = false, int year = 0, int page = 1, int pageSize = 6)
    {
        var results = new List<SearchTv>();
        var firstPage = await UseClient(c => c.SearchTvShowAsync(query, 1, includeRestricted, year)).ConfigureAwait(false);
        var total = firstPage.TotalResults;
        if (total == 0)
            return (results, total);

        var lastPage = firstPage.TotalPages;
        var actualPageSize = firstPage.Results.Count;
        var startIndex = (page - 1) * pageSize;
        var startPage = (int)Math.Floor((decimal)startIndex / actualPageSize) + 1;
        var endIndex = Math.Min(startIndex + pageSize, total);
        var endPage = total == endIndex ? lastPage : Math.Min((int)Math.Floor((decimal)endIndex / actualPageSize) + (endIndex % actualPageSize > 0 ? 1 : 0), lastPage);
        for (var i = startPage; i <= endPage; i++)
        {
            var actualPage = await UseClient(c => c.SearchTvShowAsync(query, i, includeRestricted, year)).ConfigureAwait(false);
            results.AddRange(actualPage.Results);
        }

        var skipCount = startIndex - (startPage - 1) * actualPageSize;
        var pagedResults = results.Skip(skipCount).Take(pageSize).ToList();

        _logger.LogTrace(
            "Got {Count} shows from {Results} total shows at {IndexRange} across {PageRange}.",
            pagedResults.Count,
            total,
            startIndex == endIndex ? $"index {startIndex}" : $"indexes {startIndex}-{endIndex}",
            startPage == endPage ? $"{startPage} actual page" : $"{startPage}-{endPage} actual pages"
        );

        return (pagedResults, total);
    }

    #endregion

    #region Update

    public async Task UpdateAllShows(bool force = false, bool downloadImages = false)
    {
        var allXRefs = _xrefAnidbTmdbShows.GetAll();
        _logger.LogInformation("Scheduling {Count} shows to be updated.", allXRefs.Count);
        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var xref in allXRefs)
        {
            await scheduler.StartJob<UpdateTmdbShowJob>(
                c =>
                {
                    c.TmdbShowID = xref.TmdbShowID;
                    c.ForceRefresh = force;
                    c.DownloadImages = downloadImages;
                }
            ).ConfigureAwait(false);
        }
    }

    public async Task ScheduleUpdateOfShow(int showId, bool forceRefresh = false, bool downloadImages = false, bool? downloadCrewAndCast = null, bool? downloadAlternateOrdering = null)
    {
        // Schedule the show info to be downloaded or updated.
        await (await _schedulerFactory.GetScheduler().ConfigureAwait(false)).StartJob<UpdateTmdbShowJob>(c =>
        {
            c.TmdbShowID = showId;
            c.ForceRefresh = forceRefresh;
            c.DownloadImages = downloadImages;
            c.DownloadCrewAndCast = downloadCrewAndCast;
            c.DownloadAlternateOrdering = downloadAlternateOrdering;
        }).ConfigureAwait(false);
    }

    public async Task<bool> UpdateShow(int showId, bool forceRefresh = false, bool downloadImages = false, bool downloadCrewAndCast = false, bool downloadAlternateOrdering = false)
    {
        // Abort if we're within a certain time frame as to not try and get us rate-limited.
        var tmdbShow = _tmdbShows.GetByTmdbShowID(showId) ?? new(showId);
        var newlyAdded = tmdbShow.TMDB_ShowID == 0;
        if (!forceRefresh && tmdbShow.CreatedAt != tmdbShow.LastUpdatedAt && tmdbShow.LastUpdatedAt > DateTime.Now.AddHours(-1))
            return false;

        var methods = TvShowMethods.ContentRatings | TvShowMethods.Translations;
        if (downloadAlternateOrdering)
            methods |= TvShowMethods.EpisodeGroups;
        var show = await UseClient(c => c.GetTvShowAsync(showId, methods, "en-US")).ConfigureAwait(false);
        if (show == null)
            return false;

        var settings = _settingsProvider.GetSettings();
        var preferredTitleLanguages = settings.TMDB.DownloadAllTitles ? null : Languages.PreferredNamingLanguages.Select(a => a.Language).ToHashSet();
        var preferredOverviewLanguages = settings.TMDB.DownloadAllOverviews ? null : Languages.PreferredDescriptionNamingLanguages.Select(a => a.Language).ToHashSet();

        var updated = tmdbShow.Populate(show);
        var (titlesUpdated, overviewsUpdated) = UpdateTitlesAndOverviewsWithTuple(tmdbShow, show.Translations, preferredTitleLanguages, preferredOverviewLanguages);
        updated = titlesUpdated || overviewsUpdated || updated;
        updated = await UpdateShowExternalIDs(tmdbShow) || updated;
        updated = await UpdateCompanies(tmdbShow, show.ProductionCompanies) || updated;
        var (episodesOrSeasonsUpdated, updatedEpisodes) = await UpdateShowSeasonsAndEpisodes(show, downloadCrewAndCast);
        updated = episodesOrSeasonsUpdated || updated;
        if (downloadAlternateOrdering)
            updated = await UpdateShowAlternateOrdering(show) || updated;
        if (updated)
        {
            tmdbShow.LastUpdatedAt = DateTime.Now;
            _tmdbShows.Save(tmdbShow);
        }

        foreach (var xref in _xrefAnidbTmdbShows.GetByTmdbShowID(showId))
        {
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

        if (downloadImages)
            await DownloadAllShowImages(showId, false);

        if (newlyAdded || updated)
            ShokoEventHandler.Instance.OnSeriesUpdated(tmdbShow, newlyAdded ? UpdateReason.Added : UpdateReason.Updated);
        foreach (var (episode, reason) in updatedEpisodes)
            ShokoEventHandler.Instance.OnEpisodeUpdated(tmdbShow, episode, reason);

        return updated;
    }

    private async Task<(bool episodesOrSeasonsUpdated, List<(TMDB_Episode, UpdateReason)> updatedEpisodes)> UpdateShowSeasonsAndEpisodes(TvShow show, bool downloadCrewAndCast = false)
    {
        var settings = _settingsProvider.GetSettings();
        var preferredTitleLanguages = settings.TMDB.DownloadAllTitles ? null : Languages.PreferredEpisodeNamingLanguages.Select(a => a.Language).ToHashSet();
        var preferredOverviewLanguages = settings.TMDB.DownloadAllOverviews ? null : Languages.PreferredDescriptionNamingLanguages.Select(a => a.Language).ToHashSet();

        var existingSeasons = _tmdbSeasons.GetByTmdbShowID(show.Id)
            .ToDictionary(season => season.Id);
        var seasonsToAdd = 0;
        var seasonsToSkip = new HashSet<int>();
        var seasonsToSave = new List<TMDB_Season>();

        var existingEpisodes = new ConcurrentDictionary<int, TMDB_Episode>();
        foreach (var episode in _tmdbEpisodes.GetByTmdbShowID(show.Id))
            existingEpisodes.TryAdd(episode.Id, episode);
        var episodesToAdd = 0;
        var episodesToSkip = new ConcurrentBag<int>();
        var episodesToSave = new ConcurrentBag<TMDB_Episode>();
        var episodeEventsToEmit = new List<(TMDB_Episode, UpdateReason)>();
        foreach (var reducedSeason in show.Seasons)
        {
            var season = await UseClient(c => c.GetTvSeasonAsync(show.Id, reducedSeason.SeasonNumber, TvSeasonMethods.Translations)).ConfigureAwait(false) ??
                throw new Exception($"Unable to fetch season {reducedSeason.SeasonNumber} for show \"{show.Name}\".");
            if (!existingSeasons.TryGetValue(reducedSeason.Id, out var tmdbSeason))
            {
                seasonsToAdd++;
                tmdbSeason = new(reducedSeason.Id);
            }

            var seasonUpdated = tmdbSeason.Populate(show, season);
            seasonUpdated = UpdateTitlesAndOverviews(tmdbSeason, season.Translations, preferredTitleLanguages, preferredOverviewLanguages) || seasonUpdated;
            if (seasonUpdated)
            {
                tmdbSeason.LastUpdatedAt = DateTime.Now;
                seasonsToSave.Add(tmdbSeason);
            }

            seasonsToSkip.Add(tmdbSeason.Id);

            await ProcessWithConcurrencyAsync(5, season.Episodes, async (episode) =>
            {
                var episodeNew = false;
                if (!existingEpisodes.TryGetValue(episode.Id, out var tmdbEpisode))
                {
                    episodesToAdd++;
                    tmdbEpisode = new(episode.Id);
                    episodeNew = true;
                }

                // Update base episode and titles/overviews.
                var episodeTranslations = await UseClient(c => c.GetTvEpisodeTranslationsAsync(show.Id, season.SeasonNumber, episode.EpisodeNumber)).ConfigureAwait(false);
                var episodeUpdated = tmdbEpisode.Populate(show, season, episode, episodeTranslations!);
                episodeUpdated = UpdateTitlesAndOverviews(tmdbEpisode, episodeTranslations!, preferredTitleLanguages, preferredOverviewLanguages) || episodeUpdated;
                episodeUpdated = await UpdateEpisodeExternalIDs(tmdbEpisode) || episodeUpdated;

                // Update crew & cast.
                if (downloadCrewAndCast)
                {
                    var credits = await UseClient(c => c.GetTvEpisodeCreditsAsync(show.Id, season.SeasonNumber, episode.EpisodeNumber)).ConfigureAwait(false);
                    episodeUpdated = await UpdateEpisodeCastAndCrew(tmdbEpisode, credits) || episodeUpdated;
                }

                if (episodeNew || episodeUpdated)
                    episodeEventsToEmit.Add((tmdbEpisode, episodeNew ? UpdateReason.Added : UpdateReason.Updated));

                if (episodeUpdated)
                {
                    tmdbEpisode.LastUpdatedAt = DateTime.Now;
                    episodesToSave.Add(tmdbEpisode);
                }

                episodesToSkip.Add(tmdbEpisode.Id);
            });
        }
        var seasonsToRemove = existingSeasons.Values
            .ExceptBy(seasonsToSkip, season => season.Id)
            .ToList();
        var episodesToRemove = existingEpisodes.Values
            .ExceptBy(episodesToSkip, episode => episode.Id)
            .ToList();

        _logger.LogDebug(
            "Added/updated/removed/skipped {a}/{u}/{r}/{s} seasons for show {ShowTitle} (Show={ShowId})",
            seasonsToAdd,
            seasonsToSave.Count - seasonsToAdd,
            seasonsToRemove.Count,
            existingSeasons.Count + seasonsToAdd - seasonsToRemove.Count - seasonsToSave.Count,
            show.Name,
            show.Id);
        _tmdbSeasons.Save(seasonsToSave);

        foreach (var season in seasonsToRemove)
            PurgeShowSeason(season);

        _tmdbSeasons.Delete(seasonsToRemove);

        _logger.LogDebug(
            "Added/updated/removed/skipped {a}/{u}/{r}/{s} episodes for show {ShowTitle} (Show={ShowId})",
            episodesToAdd,
            episodesToSave.Count - episodesToAdd,
            episodesToRemove.Count,
            existingEpisodes.Count + episodesToAdd - episodesToRemove.Count - episodesToSave.Count,
            show.Name,
            show.Id);
        _tmdbEpisodes.Save(episodesToSave);

        foreach (var episode in episodesToRemove)
            PurgeShowEpisode(episode);

        _tmdbEpisodes.Delete(episodesToRemove);

        foreach (var episode in episodesToRemove)
            episodeEventsToEmit.Add((episode, UpdateReason.Removed));

        return (
            seasonsToSave.Count > 0 || seasonsToRemove.Count > 0 || episodesToSave.IsEmpty || episodesToRemove.Count > 0,
            episodeEventsToEmit
        );
    }

    private async Task<bool> UpdateShowAlternateOrdering(TvShow show)
    {
        _logger.LogDebug(
            "Checking {count} episode group collections to create alternate orderings for show {ShowTitle} (Show={ShowId})",
            show.EpisodeGroups.Results.Count,
            show.Name,
            show.Id);

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
            var collection = await UseClient(c => c.GetTvEpisodeGroupsAsync(reducedCollection.Id)).ConfigureAwait(false) ??
                throw new Exception($"Unable to fetch alternate ordering \"{reducedCollection.Name}\" for show \"{show.Name}\".");

            if (!existingOrdering.TryGetValue(collection.Id, out var tmdbOrdering))
            {
                orderingToAdd++;
                tmdbOrdering = new(collection.Id);
            }

            var orderingUpdated = tmdbOrdering.Populate(collection, show.Id);
            if (orderingUpdated)
            {
                tmdbOrdering.LastUpdatedAt = DateTime.Now;
                orderingToSave.Add(tmdbOrdering);
            }

            foreach (var episodeGroup in collection.Groups)
            {
                if (!existingSeasons.TryGetValue(episodeGroup.Id, out var tmdbSeason))
                {
                    seasonsToAdd++;
                    tmdbSeason = new(episodeGroup.Id);
                }

                var seasonUpdated = tmdbSeason.Populate(episodeGroup, collection.Id, show.Id, episodeGroup.Order);
                if (seasonUpdated)
                {
                    tmdbSeason.LastUpdatedAt = DateTime.Now;
                    seasonsToSave.Add(tmdbSeason);
                }

                var episodeNumberCount = 1;
                foreach (var episode in episodeGroup.Episodes)
                {
                    if (!episode.Id.HasValue)
                        continue;

                    var episodeNumber = episodeNumberCount++;
                    var episodeId = episode.Id.Value;

                    if (!existingEpisodes.TryGetValue($"{episodeGroup.Id}:{episodeId}", out var tmdbEpisode))
                    {
                        episodesToAdd++;
                        tmdbEpisode = new(episodeGroup.Id, episodeId);
                    }

                    var episodeUpdated = tmdbEpisode.Populate(collection.Id, show.Id, episodeGroup.Order, episodeNumber);
                    if (episodeUpdated)
                    {
                        tmdbEpisode.LastUpdatedAt = DateTime.Now;
                        episodesToSave.Add(tmdbEpisode);
                    }

                    episodesToSkip.Add(tmdbEpisode.Id);
                }

                seasonsToSkip.Add(tmdbSeason.Id);
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

        return orderingToSave.Count > 0 ||
            orderingToRemove.Count > 0 ||
            seasonsToSave.Count > 0 ||
            seasonsToRemove.Count > 0 ||
            episodesToSave.Count > 0 ||
            episodesToRemove.Count > 0;
    }

    private async Task<bool> UpdateEpisodeCastAndCrew(TMDB_Episode tmdbEpisode, CreditsWithGuestStars credits)
    {
        var peopleToAdd = 0;
        var peopleToSave = new List<TMDB_Person>();
        var knownPeopleDict = new Dictionary<int, TMDB_Person>();

        var counter = 0;
        var castToAdd = 0;
        var castToKeep = new HashSet<string>();
        var castToSave = new List<TMDB_Episode_Cast>();
        var existingCastDict = _tmdbEpisodeCast.GetByTmdbEpisodeID(tmdbEpisode.Id)
            .ToDictionary(cast => cast.TmdbCreditID);
        var guestOffset = credits.Cast.Count;
        foreach (var cast in credits.Cast.Concat(credits.GuestStars))
        {
            var ordering = counter++;
            var isGuestRole = ordering >= guestOffset;
            castToKeep.Add(cast.CreditId);
            if (!knownPeopleDict.TryGetValue(cast.Id, out var tmdbPerson))
            {
                var person = await UseClient(c => c.GetPersonAsync(cast.Id, PersonMethods.Translations)).ConfigureAwait(false) ??
                    throw new Exception($"Unable to get TMDB Person with id {cast.Id}. (Show={tmdbEpisode.TmdbShowID},Season={tmdbEpisode.TmdbSeasonID},Episode={tmdbEpisode.Id},Person={cast.Id})");

                tmdbPerson = _tmdbPeople.GetByTmdbPersonID(cast.Id);
                if (tmdbPerson == null)
                {
                    tmdbPerson = new(cast.Id);
                    peopleToAdd++;
                }
                if (tmdbPerson.Populate(person))
                {
                    tmdbPerson.LastUpdatedAt = DateTime.Now;
                    peopleToSave.Add(tmdbPerson);
                }

                knownPeopleDict.Add(cast.Id, tmdbPerson);
            }

            var roleUpdated = false;
            if (!existingCastDict.TryGetValue(cast.CreditId, out var role))
            {
                role = new()
                {
                    TmdbShowID = tmdbEpisode.TmdbShowID,
                    TmdbSeasonID = tmdbEpisode.TmdbSeasonID,
                    TmdbEpisodeID = tmdbEpisode.Id,
                    TmdbPersonID = tmdbPerson.Id,
                    TmdbCreditID = cast.CreditId,
                    Ordering = ordering,
                    IsGuestRole = isGuestRole,
                };
                castToAdd++;
                roleUpdated = true;
            }

            if (role.CharacterName != cast.Character)
            {
                role.CharacterName = cast.Character;
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
        foreach (var crew in credits.Crew)
        {
            crewToKeep.Add(crew.CreditId);
            if (!knownPeopleDict.TryGetValue(crew.Id, out var tmdbPerson))
            {
                var person = await UseClient(c => c.GetPersonAsync(crew.Id, PersonMethods.Translations)).ConfigureAwait(false) ??
                    throw new Exception($"Unable to get TMDB Person with id {crew.Id}. (Show={tmdbEpisode.TmdbShowID},Season={tmdbEpisode.TmdbSeasonID},Episode={tmdbEpisode.Id},Person={crew.Id})");

                tmdbPerson = _tmdbPeople.GetByTmdbPersonID(crew.Id);
                if (tmdbPerson == null)
                {
                    tmdbPerson = new(crew.Id);
                    peopleToAdd++;
                }
                if (tmdbPerson.Populate(person))
                {
                    tmdbPerson.LastUpdatedAt = DateTime.Now;
                    peopleToSave.Add(tmdbPerson);
                }

                knownPeopleDict.Add(crew.Id, tmdbPerson);
            }

            var roleUpdated = false;
            if (!existingCrewDict.TryGetValue(crew.CreditId, out var role))
            {
                role = new()
                {
                    TmdbShowID = tmdbEpisode.TmdbShowID,
                    TmdbSeasonID = tmdbEpisode.TmdbSeasonID,
                    TmdbEpisodeID = tmdbEpisode.Id,
                    TmdbPersonID = tmdbPerson.Id,
                    TmdbCreditID = crew.CreditId,
                };
                crewToAdd++;
                roleUpdated = true;
            }

            if (role.Department != crew.Department)
            {
                role.Department = crew.Department;
                roleUpdated = true;
            }

            if (role.Job != crew.Job)
            {
                role.Job = crew.Job;
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

        _tmdbPeople.Save(peopleToSave);
        _tmdbEpisodeCast.Save(castToSave);
        _tmdbEpisodeCrew.Save(crewToSave);
        _tmdbEpisodeCast.Delete(castToRemove);
        _tmdbEpisodeCrew.Delete(crewToRemove);

        var peopleToRemove = 0;
        var peopleToCheck = existingCastDict.Values
            .Select(cast => cast.TmdbPersonID)
            .Concat(existingCrewDict.Values.Select(crew => crew.TmdbPersonID))
            .Except(knownPeopleDict.Keys)
            .ToHashSet();
        foreach (var personId in peopleToCheck)
        {
            if (IsPersonLinkedToOtherEntities(personId))
                continue;

            PurgePerson(personId);
            peopleToRemove++;
        }

        _logger.LogDebug(
            "Added/updated/removed/skipped {aa}/{au}/{ar}/{as} cast and {ra}/{ru}/{rr}/{rs} crew across {pa}/{pu}/{pr}/{ps} people for episode {EpisodeTitle} (Show={ShowId},Season={SeasonId},Episode={EpisodeId})",
            castToAdd,
            castToSave.Count - castToAdd,
            castToRemove.Count,
            existingCastDict.Count - (castToSave.Count - castToAdd),
            crewToAdd,
            crewToSave.Count - crewToAdd,
            crewToRemove.Count,
            existingCrewDict.Count - (crewToSave.Count - crewToAdd),
            peopleToAdd,
            peopleToSave.Count - peopleToAdd,
            peopleToRemove,
            knownPeopleDict.Count - (peopleToSave.Count - peopleToAdd),
            tmdbEpisode.EnglishTitle,
            tmdbEpisode.TmdbShowID,
            tmdbEpisode.TmdbSeasonID,
            tmdbEpisode.TmdbEpisodeID
        );
        return castToSave.Count > 0 ||
            castToRemove.Count > 0 ||
            crewToSave.Count > 0 ||
            crewToRemove.Count > 0 ||
            peopleToSave.Count > 0 ||
            peopleToRemove > 0;
    }

    public async Task ScheduleDownloadAllShowImages(int showId, bool forceDownload = false)
    {
        // Schedule the movie info to be downloaded or updated.
        await (await _schedulerFactory.GetScheduler().ConfigureAwait(false)).StartJob<DownloadTmdbShowImagesJob>(c =>
        {
            c.TmdbShowID = showId;
            c.ForceDownload = forceDownload;
        }).ConfigureAwait(false);
    }

    public async Task DownloadAllShowImages(int showId, bool forceDownload = false)
    {
        // Abort if we're within a certain time frame as to not try and get us rate-limited.
        var tmdbShow = _tmdbShows.GetByTmdbShowID(showId);
        if (tmdbShow is null)
            return;

        await DownloadShowImages(showId, tmdbShow.OriginalLanguage, forceDownload);

        var peopleToDownload = new HashSet<int>();
        foreach (var tmdbSeason in tmdbShow.TmdbSeasons)
        {
            await DownloadSeasonImages(tmdbSeason.TmdbSeasonID, tmdbSeason.TmdbShowID, tmdbSeason.SeasonNumber, tmdbShow.OriginalLanguage, forceDownload);
            foreach (var tmdbEpisode in tmdbSeason.TmdbEpisodes)
            {
                await DownloadEpisodeImages(tmdbEpisode.TmdbEpisodeID, tmdbEpisode.TmdbShowID, tmdbSeason.SeasonNumber, tmdbEpisode.EpisodeNumber, tmdbShow.OriginalLanguage, forceDownload);
                foreach (var cast in tmdbEpisode.Cast)
                    peopleToDownload.Add(cast.TmdbPersonID);
                foreach (var crew in tmdbEpisode.Crew)
                    peopleToDownload.Add(crew.TmdbPersonID);
            }
        }

        foreach (var personId in peopleToDownload)
            await DownloadPersonImages(personId, forceDownload);
    }

    public async Task DownloadShowImages(int showId, TitleLanguage? mainLanguage = null, bool forceDownload = false)
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.TMDB.AutoDownloadPosters && !settings.TMDB.AutoDownloadLogos && !settings.TMDB.AutoDownloadBackdrops)
            return;

        var images = await UseClient(c => c.GetTvShowImagesAsync(showId)).ConfigureAwait(false);
        var languages = GetLanguages(mainLanguage);
        if (settings.TMDB.AutoDownloadPosters)
            await _imageService.DownloadImagesByType(images.Posters, ImageEntityType.Poster, ForeignEntityType.Show, showId, settings.TMDB.MaxAutoBackdrops, languages, forceDownload);
        if (settings.TMDB.AutoDownloadLogos)
            await _imageService.DownloadImagesByType(images.Logos, ImageEntityType.Logo, ForeignEntityType.Show, showId, settings.TMDB.MaxAutoBackdrops, languages, forceDownload);
        if (settings.TMDB.AutoDownloadBackdrops)
            await _imageService.DownloadImagesByType(images.Backdrops, ImageEntityType.Backdrop, ForeignEntityType.Show, showId, settings.TMDB.MaxAutoBackdrops, languages, forceDownload);
    }

    private async Task DownloadSeasonImages(int seasonId, int showId, int seasonNumber, TitleLanguage? mainLanguage = null, bool forceDownload = false)
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.TMDB.AutoDownloadPosters)
            return;

        var images = await UseClient(c => c.GetTvSeasonImagesAsync(showId, seasonNumber)).ConfigureAwait(false);
        var languages = GetLanguages(mainLanguage);
        await _imageService.DownloadImagesByType(images.Posters, ImageEntityType.Poster, ForeignEntityType.Season, seasonId, settings.TMDB.MaxAutoBackdrops, languages, forceDownload);
    }

    private async Task DownloadEpisodeImages(int episodeId, int showId, int seasonNumber, int episodeNumber, TitleLanguage mainLanguage, bool forceDownload = false)
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.TMDB.AutoDownloadThumbnails)
            return;

        var images = await UseClient(c => c.GetTvEpisodeImagesAsync(showId, seasonNumber, episodeNumber)).ConfigureAwait(false);
        var languages = GetLanguages(mainLanguage);
        await _imageService.DownloadImagesByType(images.Stills, ImageEntityType.Thumbnail, ForeignEntityType.Episode, episodeId, settings.TMDB.MaxAutoBackdrops, languages, forceDownload);
    }

    private List<TitleLanguage> GetLanguages(TitleLanguage? mainLanguage = null) => _settingsProvider.GetSettings().TMDB.ImageLanguageOrder
        .Select(a => a is TitleLanguage.Main ? mainLanguage is not TitleLanguage.None and not TitleLanguage.Unknown ? mainLanguage : null : a)
        .WhereNotNull()
        .ToList();

    #endregion

    #region Purge

    public async Task PurgeAllUnusedShows()
    {
        var allShows = _tmdbShows.GetAll().Select(show => show.TmdbShowID)
            .Concat(_tmdbImages.GetAll().Where(image => image.TmdbShowID.HasValue).Select(image => image.TmdbShowID!.Value))
            .Concat(_xrefAnidbTmdbShows.GetAll().Select(xref => xref.TmdbShowID))
            .Concat(_xrefTmdbCompanyEntity.GetAll().Where(x => x.TmdbEntityType == ForeignEntityType.Show).Select(x => x.TmdbEntityID))
            .Concat(_xrefTmdbShowNetwork.GetAll().Select(x => x.TmdbShowID))
            .Concat(_tmdbSeasons.GetAll().Select(x => x.TmdbShowID))
            .Concat(_tmdbEpisodes.GetAll().Select(x => x.TmdbShowID))
            .Concat(_tmdbAlternateOrdering.GetAll().Select(ordering => ordering.TmdbShowID))
            .Concat(_tmdbAlternateOrderingSeasons.GetAll().Select(season => season.TmdbShowID))
            .Concat(_tmdbAlternateOrderingEpisodes.GetAll().Select(episode => episode.TmdbShowID))
            .ToHashSet();
        var toKeep = _xrefAnidbTmdbShows.GetAll()
            .Select(xref => xref.TmdbShowID)
            .ToHashSet();
        var toBePurged = allShows
            .Except(toKeep)
            .ToHashSet();

        _logger.LogInformation("Scheduling {Count} out of {AllCount} shows to be purged.", toBePurged.Count, allShows.Count);
        var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);
        foreach (var showID in toBePurged)
            await scheduler.StartJob<PurgeTmdbShowJob>(c => c.TmdbShowID = showID);
    }

    public async Task SchedulePurgeOfShow(int showId, bool removeImageFiles = true)
    {
        await (await _schedulerFactory.GetScheduler().ConfigureAwait(false)).StartJob<PurgeTmdbShowJob>(c =>
        {
            c.TmdbShowID = showId;
            c.RemoveImageFiles = removeImageFiles;
        });
    }

    public async Task<bool> PurgeShow(int showId, bool removeImageFiles = true)
    {
        var show = _tmdbShows.GetByTmdbShowID(showId);

        await _linkingService.RemoveAllShowLinksForShow(showId);

        _imageService.PurgeImages(ForeignEntityType.Show, showId, removeImageFiles);

        PurgeTitlesAndOverviews(ForeignEntityType.Show, showId);

        PurgeShowCompanies(showId, removeImageFiles);

        PurgeShowNetworks(showId, removeImageFiles);

        PurgeShowEpisodes(showId, removeImageFiles);

        PurgeShowSeasons(showId, removeImageFiles);

        PurgeShowCastAndCrew(showId, removeImageFiles);

        PurgeShowEpisodeGroups(showId);

        if (show != null)
        {
            _logger.LogTrace(
                "Removing show {ShowName} (Show={ShowId})",
                show.EnglishTitle,
                showId
            );
            _tmdbShows.Delete(show);
        }

        return false;
    }

    private void PurgeShowCompanies(int showId, bool removeImageFiles = true)
    {
        var xrefsToRemove = _xrefTmdbCompanyEntity.GetByTmdbEntityTypeAndID(ForeignEntityType.Show, showId);
        foreach (var xref in xrefsToRemove)
        {
            // Delete xref or purge company.
            var xrefs = _xrefTmdbCompanyEntity.GetByTmdbCompanyID(xref.TmdbCompanyID);
            if (xrefs.Count > 1)
                _xrefTmdbCompanyEntity.Delete(xref);
            else
                PurgeCompany(xref.TmdbCompanyID, removeImageFiles);
        }
    }

    private void PurgeShowNetworks(int showId, bool removeImageFiles = true)
    {
        var xrefsToRemove = _xrefTmdbShowNetwork.GetByTmdbShowID(showId);
        foreach (var xref in xrefsToRemove)
        {
            // Delete xref or purge company.
            var xrefs = _xrefTmdbShowNetwork.GetByTmdbNetworkID(xref.TmdbNetworkID);
            if (xrefs.Count > 1)
                _xrefTmdbShowNetwork.Delete(xref);
            else
                PurgeShowNetwork(xref.TmdbNetworkID, removeImageFiles);
        }
    }

    private void PurgeShowNetwork(int networkId, bool removeImageFiles = true)
    {
        var tmdbNetwork = _tmdbNetwork.GetByTmdbNetworkID(networkId);
        if (tmdbNetwork != null)
        {
            _logger.LogDebug("Removing TMDB Network (Network={NetworkId})", networkId);
            _tmdbNetwork.Delete(tmdbNetwork);
        }

        var images = _tmdbImages.GetByTmdbCompanyID(networkId);
        if (images.Count > 0)
            foreach (var image in images)
                _imageService.PurgeImage(image, ForeignEntityType.Company, removeImageFiles);

        var xrefs = _xrefTmdbShowNetwork.GetByTmdbNetworkID(networkId);
        if (xrefs.Count > 0)
        {
            _logger.LogDebug("Removing {count} cross-references for TMDB Network (Network={NetworkId})", xrefs.Count, networkId);
            _xrefTmdbShowNetwork.Delete(xrefs);
        }
    }

    private void PurgeShowEpisodes(int showId, bool removeImageFiles = true)
    {
        var episodesToRemove = _tmdbEpisodes.GetByTmdbShowID(showId);

        _logger.LogDebug(
            "Removing {count} episodes for show (Show={ShowId})",
            episodesToRemove.Count,
            showId
        );
        foreach (var episode in episodesToRemove)
            PurgeShowEpisode(episode, removeImageFiles);

        _tmdbEpisodes.Delete(episodesToRemove);
    }

    private void PurgeShowEpisode(TMDB_Episode episode, bool removeImageFiles = true)
    {
        _imageService.PurgeImages(ForeignEntityType.Episode, episode.Id, removeImageFiles);

        PurgeTitlesAndOverviews(ForeignEntityType.Episode, episode.Id);
    }

    private void PurgeShowSeasons(int showId, bool removeImageFiles = true)
    {
        var seasonsToRemove = _tmdbSeasons.GetByTmdbShowID(showId);

        _logger.LogDebug(
            "Removing {count} seasons for show (Show={ShowId})",
            seasonsToRemove.Count,
            showId
        );
        foreach (var season in seasonsToRemove)
            PurgeShowSeason(season, removeImageFiles);

        _tmdbSeasons.Delete(seasonsToRemove);
    }

    private void PurgeShowSeason(TMDB_Season season, bool removeImageFiles = true)
    {
        _imageService.PurgeImages(ForeignEntityType.Season, season.Id, removeImageFiles);

        PurgeTitlesAndOverviews(ForeignEntityType.Season, season.Id);
    }

    private void PurgeShowCastAndCrew(int showId, bool removeImageFiles = true)
    {
        var castMembers = _tmdbEpisodeCast.GetByTmdbShowID(showId);
        var crewMembers = _tmdbEpisodeCrew.GetByTmdbShowID(showId);

        _tmdbEpisodeCast.Delete(castMembers);
        _tmdbEpisodeCrew.Delete(crewMembers);

        var allPeopleSet = castMembers
            .Select(c => c.TmdbPersonID)
            .Concat(crewMembers.Select(c => c.TmdbPersonID))
            .Distinct()
            .ToHashSet();
        foreach (var personId in allPeopleSet)
            if (!IsPersonLinkedToOtherEntities(personId))
                PurgePerson(personId, removeImageFiles);
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

    #endregion

    #endregion

    #region Shared

    #region Titles & Overviews

    /// <summary>
    /// Updates the titles and overviews for the <paramref name="tmdbEntity"/>
    /// using the translation data available in the <paramref name="translations"/>.
    /// </summary>
    /// <param name="tmdbEntity">The local TMDB Entity to update titles and overviews for.</param>
    /// <param name="translations">The translations container returned from the API.</param>
    /// <param name="preferredTitleLanguages">The preferred title languages to store. If not set then we will store all languages.</param>
    /// <param name="preferredOverviewLanguages">The preferred overview languages to store. If not set then we will store all languages.</param>
    /// <returns>A boolean indicating if any changes were made to the titles and/or overviews.</returns>
    private bool UpdateTitlesAndOverviews(IEntityMetadata tmdbEntity, TranslationsContainer translations, HashSet<TitleLanguage>? preferredTitleLanguages, HashSet<TitleLanguage>? preferredOverviewLanguages)
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
    private (bool titlesUpdated, bool overviewsUpdated) UpdateTitlesAndOverviewsWithTuple(IEntityMetadata tmdbEntity, TranslationsContainer translations, HashSet<TitleLanguage>? preferredTitleLanguages, HashSet<TitleLanguage>? preferredOverviewLanguages)
    {
        var existingOverviews = _tmdbOverview.GetByParentTypeAndID(tmdbEntity.Type, tmdbEntity.Id);
        var existingTitles = _tmdbTitle.GetByParentTypeAndID(tmdbEntity.Type, tmdbEntity.Id);
        var overviewsToAdd = 0;
        var overviewsToSkip = new HashSet<int>();
        var overviewsToSave = new List<TMDB_Overview>();
        var titlesToAdd = 0;
        var titlesToSkip = new HashSet<int>();
        var titlesToSave = new List<TMDB_Title>();
        foreach (var translation in translations.Translations)
        {
            var languageCode = translation.Iso_639_1.ToLowerInvariant();
            var countryCode = translation.Iso_3166_1.ToUpperInvariant();

            var alwaysInclude = false;
            var currentTitle = translation.Data.Name ?? string.Empty;
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

            var shouldInclude = alwaysInclude || preferredTitleLanguages is null || preferredTitleLanguages.Contains(languageCode.GetTitleLanguage());
            var existingTitle = existingTitles.FirstOrDefault(title => title.LanguageCode == languageCode && title.CountryCode == countryCode);
            if (shouldInclude && !string.IsNullOrEmpty(currentTitle) && !(
                // Make sure the "translation" is not just the English Title or
                (languageCode != "en" && languageCode != "US" && !string.IsNullOrEmpty(tmdbEntity.EnglishTitle) && string.Equals(tmdbEntity.EnglishTitle, currentTitle, StringComparison.InvariantCultureIgnoreCase)) ||
                // the Original Title.
                (!string.IsNullOrEmpty(tmdbEntity.OriginalLanguageCode) && languageCode != tmdbEntity.OriginalLanguageCode && !string.IsNullOrEmpty(tmdbEntity.OriginalTitle) && string.Equals(tmdbEntity.OriginalTitle, currentTitle, StringComparison.InvariantCultureIgnoreCase))
            ))
            {
                if (existingTitle == null)
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
            var currentOverview = translation.Data.Overview ?? string.Empty;
            if (languageCode == "en" && countryCode == "US")
            {
                alwaysInclude = true;
                currentOverview = tmdbEntity.EnglishOverview ?? translation.Data.Overview ?? string.Empty;
            }

            shouldInclude = alwaysInclude || preferredOverviewLanguages is null || preferredOverviewLanguages.Contains(languageCode.GetTitleLanguage());
            var existingOverview = existingOverviews.FirstOrDefault(overview => overview.LanguageCode == languageCode && overview.CountryCode == countryCode);
            if (shouldInclude && !string.IsNullOrEmpty(currentOverview))
            {
                if (existingOverview == null)
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

    private void PurgeTitlesAndOverviews(ForeignEntityType foreignType, int foreignId)
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

    #region Companies

    private async Task<bool> UpdateCompanies(IEntityMetadata tmdbEntity, List<ProductionCompany> companies)
    {
        var existingXrefs = _xrefTmdbCompanyEntity.GetByTmdbEntityTypeAndID(tmdbEntity.Type, tmdbEntity.Id)
            .ToDictionary(xref => xref.TmdbCompanyID);
        var xrefsToAdd = 0;
        var xrefsToSkip = new HashSet<int>();
        var xrefsToSave = new List<TMDB_Company_Entity>();
        var indexCounter = 0;
        foreach (var company in companies)
        {
            var currentIndex = indexCounter++;
            if (existingXrefs.TryGetValue(company.Id, out var existingXref))
            {
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
            _logger.LogDebug("Updating TMDB Company (Company={CompanyId})", company.Id);
            _tmdbCompany.Save(tmdbCompany);
        }

        var settings = _settingsProvider.GetSettings();
        if (!string.IsNullOrEmpty(company.LogoPath) && settings.TMDB.AutoDownloadStudioImages)
            await _imageService.DownloadImageByType(company.LogoPath, ImageEntityType.Logo, ForeignEntityType.Company, company.Id);
    }

    private void PurgeCompany(int companyId, bool removeImageFiles = true)
    {
        var tmdbCompany = _tmdbCompany.GetByTmdbCompanyID(companyId);
        if (tmdbCompany != null)
        {
            _logger.LogDebug("Removing TMDB Company (Company={CompanyId})", companyId);
            _tmdbCompany.Delete(tmdbCompany);
        }

        var images = _tmdbImages.GetByTmdbCompanyID(companyId);
        if (images.Count > 0)
            foreach (var image in images)
                _imageService.PurgeImage(image, ForeignEntityType.Company, removeImageFiles);

        var xrefs = _xrefTmdbCompanyEntity.GetByTmdbCompanyID(companyId);
        if (xrefs.Count > 0)
        {
            _logger.LogDebug("Removing {count} cross-references for TMDB Company (Company={CompanyId})", xrefs.Count, companyId);
            _xrefTmdbCompanyEntity.Delete(xrefs);
        }
    }

    #endregion

    #region People

    public async Task DownloadPersonImages(int personId, bool forceDownload = false)
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.TMDB.AutoDownloadStaffImages)
            return;

        var images = await UseClient(c => c.GetPersonImagesAsync(personId)).ConfigureAwait(false);
        await _imageService.DownloadImagesByType(images.Profiles, ImageEntityType.Person, ForeignEntityType.Person, personId, settings.TMDB.MaxAutoStaffImages, [], forceDownload);
    }

    private void PurgePerson(int personId, bool removeImageFiles = true)
    {
        var person = _tmdbPeople.GetByTmdbPersonID(personId);
        if (person != null)
        {
            _logger.LogDebug("Removing TMDB Person (Person={PersonId})", personId);
            _tmdbPeople.Delete(person);
        }

        var images = _tmdbImages.GetByTmdbPersonID(personId);
        if (images.Count > 0)
            foreach (var image in images)
                _imageService.PurgeImage(image, ForeignEntityType.Person, removeImageFiles);

        var movieCast = _tmdbMovieCast.GetByTmdbPersonID(personId);
        if (movieCast.Count > 0)
        {
            _logger.LogDebug("Removing {count} movie cast roles for TMDB Person (Person={PersonId})", movieCast.Count, personId);
            _tmdbMovieCast.Delete(movieCast);
        }

        var movieCrew = _tmdbMovieCrew.GetByTmdbPersonID(personId);
        if (movieCrew.Count > 0)
        {
            _logger.LogDebug("Removing {count} movie crew roles for TMDB Person (Person={PersonId})", movieCrew.Count, personId);
            _tmdbMovieCrew.Delete(movieCrew);
        }

        var episodeCast = _tmdbEpisodeCast.GetByTmdbPersonID(personId);
        if (episodeCast.Count > 0)
        {
            _logger.LogDebug("Removing {count} show cast roles for TMDB Person (Person={PersonId})", episodeCast.Count, personId);
            _tmdbEpisodeCast.Delete(episodeCast);
        }

        var episodeCrew = _tmdbEpisodeCrew.GetByTmdbPersonID(personId);
        if (episodeCrew.Count > 0)
        {
            _logger.LogDebug("Removing {count} show crew roles for TMDB Person (Person={PersonId})", episodeCrew.Count, personId);
            _tmdbEpisodeCrew.Delete(episodeCrew);
        }
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

    #region Helpers

    private static async Task ProcessWithConcurrencyAsync<T>(
        int maxConcurrent,
        IEnumerable<T> enumerable,
        Func<T, Task> processAsync
    )
    {
        if (maxConcurrent < 1)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrent), "Concurrency level must be at least 1.");

        var semaphore = new SemaphoreSlim(maxConcurrent);
        var exceptions = new List<Exception>();
        var cancellationTokenSource = new CancellationTokenSource();
        var tasks = enumerable
            .Select(item => Task.Run(async () =>
            {
                try
                {
                    await semaphore.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                try
                {
                    await processAsync(item).ConfigureAwait(false);
                }
                finally
                {
                    semaphore.Release();
                }
            }))
            .ToList();
        while (tasks.Count > 0)
        {
            try
            {
                var task = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(task);
            }
            catch (Exception ex)
            {
                var task = tasks.First(task => task.IsFaulted);
                tasks.Remove(task);
                exceptions.Add(ex);
                if (exceptions.Count > maxConcurrent)
                {
                    cancellationTokenSource.Cancel();
                    throw new AggregateException(exceptions);
                }
                continue;
            }
        }
    }

    #endregion

    #region External IDs

    /// <summary>
    /// Update TvDB ID for the TMDB show if needed and the ID is available.
    /// </summary>
    /// <param name="show">TMDB Show.</param>
    /// <returns>Indicates that the ID was updated.</returns>
    private async Task<bool> UpdateShowExternalIDs(TMDB_Show show)
    {
        var externalIds = await UseClient(c => c.GetTvShowExternalIdsAsync(show.TmdbShowID)).ConfigureAwait(false);
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
    /// <returns>Indicates that the ID was updated.</returns>
    private async Task<bool> UpdateEpisodeExternalIDs(TMDB_Episode episode)
    {
        var externalIds = await UseClient(c => c.GetTvEpisodeExternalIdsAsync(episode.TmdbShowID, episode.SeasonNumber, episode.EpisodeNumber)).ConfigureAwait(false);
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
    /// <returns>Indicates that the ID was updated.</returns>
    private async Task<bool> UpdateMovieExternalIDs(TMDB_Movie movie)
    {
        var externalIds = await UseClient(c => c.GetMovieExternalIdsAsync(movie.TmdbMovieID)).ConfigureAwait(false);
        if (movie.ImdbMovieID == externalIds.ImdbId)
            return false;

        movie.ImdbMovieID = externalIds.ImdbId;
        return true;
    }

    #endregion
}
