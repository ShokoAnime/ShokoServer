using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB.Search;
using Shoko.Server.Repositories;
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

#nullable enable
namespace Shoko.Server.Providers.TMDB;

public class TMDBHelper
{
    private readonly ILogger<TMDBHelper> _logger;

    private readonly ICommandRequestFactory _commandFactory;

    private readonly ISettingsProvider _settingsProvider;

    public readonly TMDBOfflineSearch OfflineSearch;

    private readonly TMDbClient _client;

    private static string? _imageServerUrl = null;

    public static string? ImageServerUrl =>
        _imageServerUrl;

    private const string APIKey = "8192e8032758f0ef4f7caa1ab7b32dd3";

    public TMDBHelper(ILoggerFactory loggerFactory, ICommandRequestFactory commandFactory, ISettingsProvider settingsProvider)
    {
        _logger = loggerFactory.CreateLogger<TMDBHelper>();
        _commandFactory = commandFactory;
        _settingsProvider = settingsProvider;
        _client = new(APIKey);
        OfflineSearch = new(loggerFactory);

        if (string.IsNullOrEmpty(_imageServerUrl))
        {
            var config = _client.GetAPIConfiguration().Result;
            _imageServerUrl = config.Images.SecureBaseUrl;
        }
    }

    public void ScanForMatches()
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.TvDB.AutoLink)
            return;

        var allSeries = RepoFactory.AnimeSeries.GetAll();
        foreach (var ser in allSeries)
        {
            if (ser.IsTMDBAutoMatchingDisabled)
                continue;

            var anime = ser.GetAnime();
            if (anime == null)
                continue;

            if (anime.Restricted > 0)
                continue;

            if (anime.GetCrossRefTmdbMovies().Count > 0)
                continue;

            if (anime.GetCrossRefTmdbShows().Count > 0)
                continue;

            _logger.LogTrace("Found anime without TMDB association: {MainTitle}", anime.MainTitle);

            _commandFactory.CreateAndSave<CommandRequest_TMDB_Search>(c => c.AnimeID = ser.AniDB_ID);
        }
    }

    #region Movies

    #region Search

    public (List<SearchMovie> Page, int TotalCount) SearchMovies(string query, bool includeRestricted = false, int page = 1)
    {
        var results = _client.SearchMovieAsync(query, page, includeRestricted)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        _logger.LogInformation("Got {Count} of {Results} results", results?.Results.Count ?? 0, results?.TotalResults ?? 0);
        return (results?.Results ?? new(), results?.TotalResults ?? 0);
    }

    #endregion

    #region Links

    public void AddMovieLink(int animeId, int movieId, int? episodeId = null, bool additiveLink = false, bool isAutomatic = false)
    {
        // Remove all existing links.
        if (!additiveLink)
            RemoveAllMovieLinks(animeId);

        // Add or update the link.
        _logger.LogInformation("Adding TMDB Movie Link: AniDB (ID:{AnidbID}) → TvDB Movie (ID:{TmdbID})", animeId, movieId);
        var xref = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeAndTmdbMovieIDs(animeId, movieId) ??
            new(animeId, movieId);
        if (episodeId.HasValue)
            xref.AnidbEpisodeID = episodeId.Value <= 0 ? null : episodeId.Value;
        xref.Source = isAutomatic ? CrossRefSource.Automatic : CrossRefSource.User;
        RepoFactory.CrossRef_AniDB_TMDB_Movie.Save(xref);

    }

    public void RemoveMovieLink(int animeId, int movieId, bool purge = false, bool removeImageFiles = true)
    {
        var xref = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeAndTmdbMovieIDs(animeId, movieId);
        if (xref == null)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        var series = RepoFactory.AnimeSeries.GetByAnimeID(animeId);
        if (series != null && !series.IsTMDBAutoMatchingDisabled)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            RepoFactory.AnimeSeries.Save(series, false, true, true);
        }

        RemoveMovieLink(xref, removeImageFiles, purge ? true : null);
    }

    public void RemoveAllMovieLinks(int animeId, bool purge = false, bool removeImageFiles = true)
    {
        _logger.LogInformation("Removing All TMDB Movie Links for: {AnimeID}", animeId);
        var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID(animeId);
        if (xrefs.Count == 0)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        var series = RepoFactory.AnimeSeries.GetByAnimeID(animeId);
        if (series != null && !series.IsTMDBAutoMatchingDisabled)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            RepoFactory.AnimeSeries.Save(series, false, true, true);
        }

        foreach (var xref in xrefs)
            RemoveMovieLink(xref, removeImageFiles, purge ? true : null);
    }

    private void RemoveMovieLink(CrossRef_AniDB_TMDB_Movie xref, bool removeImageFiles = true, bool? purge = null)
    {
        ResetPreferredImage(xref.AnidbAnimeID, ForeignEntityType.Movie, xref.TmdbMovieID);

        _logger.LogInformation("Removing TMDB Movie Link: AniDB ({AnidbID}) → TMDB Movie (ID:{TmdbID})", xref.AnidbAnimeID, xref.TmdbMovieID);
        RepoFactory.CrossRef_AniDB_TMDB_Movie.Delete(xref);

        if (purge ?? RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByTmdbMovieID(xref.TmdbMovieID).Count == 0)
            _commandFactory.CreateAndSave<CommandRequest_TMDB_Movie_Purge>(c =>
            {
                c.TmdbMovieID = xref.TmdbMovieID;
                c.RemoveImageFiles = removeImageFiles;
            });
    }

    #endregion

    #region Update

    public void UpdateAllMovies(bool force, bool saveImages)
    {
        var allXRefs = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetAll();
        _logger.LogInformation("Scheduling {Count} movies to be updated.", allXRefs.Count);
        foreach (var xref in allXRefs)
            _commandFactory.CreateAndSave<CommandRequest_TMDB_Movie_Update>(
                c =>
                {
                    c.TmdbMovieID = xref.TmdbMovieID;
                    c.ForceRefresh = force;
                    c.DownloadImages = saveImages;
                }
            );
    }

    public void ScheduleUpdateOfMovie(int movieId, bool forceRefresh = false, bool downloadImages = false, bool? downloadCollections = null)
    {
        // Schedule the movie info to be downloaded or updated.
        _commandFactory.CreateAndSave<CommandRequest_TMDB_Movie_Update>(c =>
        {
            c.TmdbMovieID = movieId;
            c.ForceRefresh = forceRefresh;
            c.DownloadImages = downloadImages;
            c.DownloadCollections = downloadCollections;
        });
    }

    public async Task<bool> UpdateMovie(int movieId, bool forceRefresh = false, bool downloadImages = false, bool downloadCollections = false)
    {
        // Abort if we're within a certain time frame as to not try and get us rate-limited.
        var tmdbMovie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieId) ?? new(movieId);
        if (!forceRefresh && tmdbMovie.CreatedAt != tmdbMovie.LastUpdatedAt && tmdbMovie.LastUpdatedAt < DateTime.Now.AddHours(-1))
            return false;

        // Abort if we couldn't find the movie by id.
        var movie = await _client.GetMovieAsync(movieId, "en-US", null, MovieMethods.Translations | MovieMethods.Credits);
        if (movie == null)
            return false;

        var updated = tmdbMovie.Populate(movie);
        updated |= UpdateTitlesAndOverviews(tmdbMovie, movie.Translations);
        updated |= UpdateCompanies(tmdbMovie, movie.ProductionCompanies);
        updated |= await UpdateMovieCastAndCrew(tmdbMovie, movie.Credits, downloadImages);
        if (updated)
        {
            tmdbMovie.LastUpdatedAt = DateTime.Now;
            RepoFactory.TMDB_Movie.Save(tmdbMovie);
        }

        if (downloadImages && downloadCollections)
            await Task.WhenAll(
                DownloadMovieImages(movieId),
                UpdateMovieCollections(movie)
            );
        else if (downloadImages)
            await DownloadMovieImages(movieId);
        else if (downloadCollections)
            await UpdateMovieCollections(movie);

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
        var existingCastDict = RepoFactory.TMDB_Movie_Cast.GetByTmdbMovieID(tmdbMovie.Id)
            .ToDictionary(cast => cast.TmdbCreditID);
        foreach (var cast in credits.Cast)
        {
            var ordering = counter++;
            castToKeep.Add(cast.CreditId);
            if (!knownPeopleDict.TryGetValue(cast.Id, out var tmdbPerson))
            {
                var person = await _client.GetPersonAsync(cast.Id, PersonMethods.Translations) ??
                    throw new Exception($"Unable to get TMDB Person with id {cast.Id}. (Movie={tmdbMovie.Id},Person={cast.Id})");

                tmdbPerson = RepoFactory.TMDB_Person.GetByTmdbPersonID(cast.Id);
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
        var existingCrewDict = RepoFactory.TMDB_Movie_Crew.GetByTmdbMovieID(tmdbMovie.Id)
            .ToDictionary(crew => crew.TmdbCreditID);
        foreach (var crew in credits.Crew)
        {
            crewToKeep.Add(crew.CreditId);
            if (!knownPeopleDict.TryGetValue(crew.Id, out var tmdbPerson))
            {
                var person = await _client.GetPersonAsync(crew.Id, PersonMethods.Translations) ??
                    throw new Exception($"Unable to get TMDB Person with id {crew.Id}. (Movie={tmdbMovie.Id},Person={crew.Id})");

                tmdbPerson = RepoFactory.TMDB_Person.GetByTmdbPersonID(crew.Id);
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

        RepoFactory.TMDB_Person.Save(peopleToSave);
        RepoFactory.TMDB_Movie_Cast.Save(castToSave);
        RepoFactory.TMDB_Movie_Crew.Save(crewToSave);
        RepoFactory.TMDB_Movie_Cast.Delete(castToRemove);
        RepoFactory.TMDB_Movie_Crew.Delete(crewToRemove);

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
        var collectionId = movie.BelongsToCollection?.Id;
        if (collectionId.HasValue)
        {
            var movieXRefs = RepoFactory.TMDB_Collection_Movie.GetByTmdbCollectionID(collectionId.Value);
            var tmdbCollection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(collectionId.Value) ?? new(collectionId.Value);
            var collection = await _client.GetCollectionAsync(collectionId.Value, CollectionMethods.Images | CollectionMethods.Translations);
            if (collection == null)
            {
                PurgeMovieCollection(collectionId.Value);
                return;
            }

            var updated = tmdbCollection.Populate(collection);
            updated |= UpdateTitlesAndOverviews(tmdbCollection, collection.Translations);

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
                xrefsToSave.Add(new(collectionId.Value, movie.Id, index + 1));
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
            RepoFactory.TMDB_Collection_Movie.Save(xrefsToSave);
            RepoFactory.TMDB_Collection_Movie.Delete(xrefsToRemove);

            if (updated || xrefsToSave.Count > 0 || xrefsToRemove.Count > 0)
            {
                tmdbCollection.LastUpdatedAt = DateTime.Now;
                RepoFactory.TMDB_Collection.Save(tmdbCollection);
            }
        }
        else
        {
            CleanupMovieCollection(movie.Id);
        }
    }

    public async Task DownloadMovieImages(int movieId, bool forceDownload = false)
    {
        var settings = _settingsProvider.GetSettings();
        var images = await _client.GetMovieImagesAsync(movieId);
        if (settings.TMDB.AutoDownloadPosters)
            DownloadImagesByType(images.Posters, ImageEntityType.Poster, ForeignEntityType.Movie, movieId, settings.TMDB.MaxAutoPosters, forceDownload);

        if (settings.TMDB.AutoDownloadLogos)
            DownloadImagesByType(images.Logos, ImageEntityType.Logo, ForeignEntityType.Movie, movieId, settings.TMDB.MaxAutoLogos, forceDownload);

        if (settings.TMDB.AutoDownloadBackdrops)
            DownloadImagesByType(images.Backdrops, ImageEntityType.Backdrop, ForeignEntityType.Movie, movieId, settings.TMDB.MaxAutoBackdrops, forceDownload);
    }

    #endregion

    #region Purge

    public void PurgeAllUnusedMovies()
    {
        var allMovies = RepoFactory.TMDB_Movie.GetAll().Select(movie => movie.TmdbMovieID)
            .Concat(RepoFactory.TMDB_Image.GetAll().Where(image => image.TmdbMovieID.HasValue).Select(image => image.TmdbMovieID!.Value))
            .Concat(RepoFactory.CrossRef_AniDB_TMDB_Movie.GetAll().Select(xref => xref.TmdbMovieID))
            .Concat(RepoFactory.TMDB_Company_Entity.GetAll().Where(x => x.TmdbEntityType == ForeignEntityType.Movie).Select(x => x.TmdbEntityID))
            .Concat(RepoFactory.TMDB_Movie_Cast.GetAll().Select(x => x.TmdbMovieID))
            .Concat(RepoFactory.TMDB_Movie_Crew.GetAll().Select(x => x.TmdbMovieID))
            .Concat(RepoFactory.TMDB_Collection.GetAll().Select(collection => collection.TmdbCollectionID))
            .Concat(RepoFactory.TMDB_Collection_Movie.GetAll().Select(collectionMovie => collectionMovie.TmdbMovieID))
            .ToHashSet();
        var toKeep = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetAll()
            .Select(xref => xref.TmdbMovieID)
            .ToHashSet();
        var toBePurged = allMovies
            .Except(toKeep)
            .ToHashSet();

        _logger.LogInformation("Scheduling {Count} out of {AllCount} movies to be purged.", toBePurged.Count, allMovies.Count);
        foreach (var movieID in toBePurged)
            _commandFactory.CreateAndSave<CommandRequest_TMDB_Movie_Purge>(c => c.TmdbMovieID = movieID);
    }

    /// <summary>
    /// Purge a TMDB movie from the local database.
    /// </summary>
    /// <param name="movieId">TMDB Movie ID.</param>
    /// <param name="removeImageFiles">Remove image files.</param>
    public void PurgeMovie(int movieId, bool removeImageFiles = true)
    {
        var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByTmdbMovieID(movieId);
        if (xrefs.Count > 0)
            foreach (var xref in xrefs)
                RemoveMovieLink(xref, removeImageFiles, false);

        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieId);
        if (movie != null)
        {
            _logger.LogTrace("Removing movie {MovieName} (Movie={MovieID})", movie.OriginalTitle, movie.Id);
            RepoFactory.TMDB_Movie.Delete(movie);
        }

        PurgeImages(ForeignEntityType.Movie, movieId, removeImageFiles);

        PurgeMovieCompanies(movieId, removeImageFiles);

        PurgeMovieCastAndCrew(movieId, removeImageFiles);

        CleanupMovieCollection(movieId);

        PurgeTitlesAndOverviews(ForeignEntityType.Movie, movieId);
    }

    private void PurgeMovieCompanies(int movieId, bool removeImageFiles = true)
    {
        var xrefsToRemove = RepoFactory.TMDB_Company_Entity.GetByTmdbEntityTypeAndID(ForeignEntityType.Movie, movieId);
        foreach (var xref in xrefsToRemove)
        {
            // Delete xref or purge company.
            var xrefs = RepoFactory.TMDB_Company_Entity.GetByTmdbCompanyID(xref.TmdbCompanyID);
            if (xrefs.Count > 1)
                RepoFactory.TMDB_Company_Entity.Delete(xref);
            else
                PurgeCompany(xref.TmdbCompanyID, removeImageFiles);
        }
    }

    private void PurgeMovieCastAndCrew(int movieId, bool removeImageFiles = true)
    {
        var castMembers = RepoFactory.TMDB_Movie_Cast.GetByTmdbMovieID(movieId);
        var crewMembers = RepoFactory.TMDB_Movie_Crew.GetByTmdbMovieID(movieId);

        RepoFactory.TMDB_Movie_Cast.Delete(castMembers);
        RepoFactory.TMDB_Movie_Crew.Delete(crewMembers);

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
        var xref = RepoFactory.TMDB_Collection_Movie.GetByTmdbMovieID(movieId);
        if (xref == null)
            return;

        var allXRefs = RepoFactory.TMDB_Collection_Movie.GetByTmdbCollectionID(xref.TmdbCollectionID);
        if (allXRefs.Count > 1)
            RepoFactory.TMDB_Collection_Movie.Delete(xref);
        else
            PurgeMovieCollection(xref.TmdbCollectionID, removeImageFiles);
    }

    private void PurgeMovieCollection(int collectionId, bool removeImageFiles = true)
    {
        var collection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(collectionId);
        var collectionXRefs = RepoFactory.TMDB_Collection_Movie.GetByTmdbCollectionID(collectionId);
        if (collectionXRefs.Count > 0)
        {
            _logger.LogTrace(
                "Removing {Count} cross-references for movie collection {CollectionName} (Collection={CollectionID})",
                collectionXRefs.Count, collection?.EnglishTitle ?? string.Empty,
                collectionId
            );
            RepoFactory.TMDB_Collection_Movie.Delete(collectionXRefs);
        }

        PurgeImages(ForeignEntityType.Collection, collectionId, removeImageFiles);

        PurgeTitlesAndOverviews(ForeignEntityType.Collection, collectionId);

        if (collection != null)
        {
            _logger.LogTrace(
                "Removing movie collection {CollectionName} (Collection={CollectionID})",
                collection.EnglishTitle,
                collectionId
            );
            RepoFactory.TMDB_Collection.Delete(collection);
        }
    }

    #endregion

    #endregion

    #region Show

    #region Search

    public (List<SearchTv> Page, int TotalCount) SearchShows(string query, bool includeRestricted = false, int page = 1)
    {
        var results = _client.SearchTvShowAsync(query, page, includeRestricted)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        _logger.LogInformation("Got {Count} of {Results} results", results?.Results.Count ?? 0, results?.TotalResults ?? 0);
        return (results?.Results ?? new(), results?.TotalResults ?? 0);
    }

    #endregion

    #region Links

    public void AddShowLink(int animeId, int showId, bool additiveLink = true, bool isAutomatic = false)
    {
        // Remove all existing links.
        if (!additiveLink)
            RemoveAllShowLinks(animeId);

        // Add or update the link.
        _logger.LogInformation("Adding TMDB Show Link: AniDB (ID:{AnidbID}) → TvDB Show (ID:{TmdbID})", animeId, showId);
        var xref = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeAndTmdbShowIDs(animeId, showId) ??
            new(animeId, showId);
        xref.Source = isAutomatic ? CrossRefSource.Automatic : CrossRefSource.User;
        RepoFactory.CrossRef_AniDB_TMDB_Show.Save(xref);
    }

    public void RemoveShowLink(int animeId, int showId, bool purge = false, bool removeImageFiles = true)
    {
        var xref = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeAndTmdbShowIDs(animeId, showId);
        if (xref == null)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        var series = RepoFactory.AnimeSeries.GetByAnimeID(animeId);
        if (series != null && !series.IsTMDBAutoMatchingDisabled)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            RepoFactory.AnimeSeries.Save(series, false, true, true);
        }

        RemoveShowLink(xref, removeImageFiles, purge ? true : null);
    }

    public void RemoveAllShowLinks(int animeId, bool purge = false, bool removeImageFiles = true)
    {
        _logger.LogInformation("Removing All TMDB Show Links for: {AnimeID}", animeId);
        var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID(animeId);
        if (xrefs == null || xrefs.Count == 0)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        var series = RepoFactory.AnimeSeries.GetByAnimeID(animeId);
        if (series != null && !series.IsTMDBAutoMatchingDisabled)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            RepoFactory.AnimeSeries.Save(series, false, true, true);
        }

        foreach (var xref in xrefs)
            RemoveShowLink(xref, removeImageFiles, purge ? true : null);
    }

    private void RemoveShowLink(CrossRef_AniDB_TMDB_Show xref, bool removeImageFiles = true, bool? purge = null)
    {
        ResetPreferredImage(xref.AnidbAnimeID, ForeignEntityType.Show, xref.TmdbShowID);

        _logger.LogInformation("Removing TMDB Show Link: AniDB ({AnidbID}) → TMDB Show (ID:{TmdbID})", xref.AnidbAnimeID, xref.TmdbShowID);
        RepoFactory.CrossRef_AniDB_TMDB_Show.Delete(xref);

        var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetOnlyByAnidbAnimeAndTmdbShowIDs(xref.AnidbAnimeID, xref.TmdbShowID);
        _logger.LogInformation("Removing {XRefsCount} Show Episodes for AniDB Anime ({AnidbID})", xrefs.Count, xref.AnidbAnimeID);
        RepoFactory.CrossRef_AniDB_TMDB_Episode.Delete(xrefs);

        if (purge ?? RepoFactory.CrossRef_AniDB_TMDB_Show.GetByTmdbShowID(xref.TmdbShowID).Count == 0)
            _commandFactory.CreateAndSave<CommandRequest_TMDB_Show_Purge>(c =>
            {
                c.TmdbShowID = xref.TmdbShowID;
                c.RemoveImageFiles = removeImageFiles;
            });
    }

    public void ResetAllEpisodeLinks(int anidbAnimeId)
    {
        var showId = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID(anidbAnimeId)
            .FirstOrDefault()?.TmdbShowID;
        if (showId.HasValue)
        {
            var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbAnimeID(anidbAnimeId);
            var toSave = new List<CrossRef_AniDB_TMDB_Episode>();
            var toDelete = new List<CrossRef_AniDB_TMDB_Episode>();

            // Reset existing xrefs.
            var existingIDs = new HashSet<int>();
            foreach (var xref in xrefs)
            {
                if (existingIDs.Add(xref.AnidbEpisodeID))
                {
                    xref.TmdbEpisodeID = 0;
                    toSave.Add(xref);
                }
                else
                {
                    toDelete.Add(xref);
                }
            }

            // Add missing xrefs.
            var anidbEpisodesWithoutXrefs = RepoFactory.AniDB_Episode.GetByAnimeID(anidbAnimeId)
                .Where(episode => !existingIDs.Contains(episode.AniDB_EpisodeID) && episode.EpisodeType is (int)EpisodeType.Episode or (int)EpisodeType.Special);
            foreach (var anidbEpisode in anidbEpisodesWithoutXrefs)
                toSave.Add(new(anidbEpisode.AniDB_EpisodeID, anidbAnimeId, 0, showId.Value, MatchRating.UserVerified));

            // Save the changes.
            RepoFactory.CrossRef_AniDB_TMDB_Episode.Save(toSave);
            RepoFactory.CrossRef_AniDB_TMDB_Episode.Delete(toDelete);
        }
        else
        {
            // Remove all episode cross-references if no show is linked.
            var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbAnimeID(anidbAnimeId);
            RepoFactory.CrossRef_AniDB_TMDB_Episode.Delete(xrefs);
        }
    }

    public bool SetEpisodeLink(int anidbEpisodeId, int tmdbEpisodeId, bool additiveLink = true, int? index = null)
    {
        var anidbEpisode = RepoFactory.AniDB_Episode.GetByEpisodeID(anidbEpisodeId);
        if (anidbEpisode == null)
            return false;

        // Set an empty link.
        if (tmdbEpisodeId == 0)
        {
            var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbEpisodeID(anidbEpisodeId);
            var toSave = xrefs.Count > 0 ? xrefs[0] : new(anidbEpisodeId, anidbEpisode.AnimeID, 0, 0);
            toSave.TmdbShowID = 0;
            toSave.TmdbEpisodeID = 0;
            toSave.Ordering = 0;
            var toDelete = xrefs.Skip(1).ToList();
            RepoFactory.CrossRef_AniDB_TMDB_Episode.Save(toSave);
            RepoFactory.CrossRef_AniDB_TMDB_Episode.Delete(toDelete);

            return true;
        }

        var tmdbEpisode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(tmdbEpisodeId);
        if (tmdbEpisode == null)
            return false;

        // Add another link
        if (additiveLink)
        {
            var toSave = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbEpisodeAndTmdbEpisodeIDs(anidbEpisodeId, tmdbEpisodeId)
                ?? new(anidbEpisodeId, anidbEpisode.AnimeID, tmdbEpisodeId, tmdbEpisode.TmdbShowID);
            var existingAnidbLinks = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbEpisodeID(anidbEpisodeId).Count;
            var existingTmdbLinks = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByTmdbEpisodeID(tmdbEpisodeId).Count;
            if (toSave.CrossRef_AniDB_TMDB_EpisodeID == 0 && !index.HasValue)
                index = existingAnidbLinks > 0 ? existingAnidbLinks - 1 : existingTmdbLinks > 0 ? existingTmdbLinks - 1 : 0;
            if (index.HasValue)
                toSave.Ordering = index.Value;
            RepoFactory.CrossRef_AniDB_TMDB_Episode.Save(toSave);
        }
        else
        {
            var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbEpisodeID(anidbEpisodeId);
            var toSave = xrefs.Count > 0 ? xrefs[0] : new(anidbEpisodeId, anidbEpisode.AnimeID, tmdbEpisodeId, tmdbEpisode.TmdbShowID);
            toSave.TmdbShowID = tmdbEpisode.TmdbShowID;
            toSave.TmdbEpisodeID = tmdbEpisode.TmdbEpisodeID;
            toSave.Ordering = 0;
            var toDelete = xrefs.Skip(1).ToList();
            RepoFactory.CrossRef_AniDB_TMDB_Episode.Save(toSave);
            RepoFactory.CrossRef_AniDB_TMDB_Episode.Delete(toDelete);
        }

        return true;
    }

    #endregion

    #region Update

    public void UpdateAllShows(bool force = false, bool downloadImages = false)
    {
        var allXRefs = RepoFactory.CrossRef_AniDB_TMDB_Show.GetAll();
        _logger.LogInformation("Scheduling {Count} shows to be updated.", allXRefs.Count);
        foreach (var xref in allXRefs)
        {
            _commandFactory.CreateAndSave<CommandRequest_TMDB_Show_Update>(
                c =>
                {
                    c.TmdbShowID = xref.TmdbShowID;
                    c.ForceRefresh = force;
                    c.DownloadImages = downloadImages;
                }
            );
        }
    }

    public void ScheduleUpdateOfShow(int showId, bool forceRefresh = false, bool downloadImages = false, bool? downloadAlternateOrdering = null)
    {
        // Schedule the show info to be downloaded or updated.
        _commandFactory.CreateAndSave<CommandRequest_TMDB_Show_Update>(c =>
        {
            c.TmdbShowID = showId;
            c.ForceRefresh = forceRefresh;
            c.DownloadImages = downloadImages;
            c.DownloadAlternateOrdering = downloadAlternateOrdering;
        });
    }

    public async Task<bool> UpdateShow(int showId, bool forceRefresh = false, bool downloadImages = false, bool downloadAlternateOrdering = false)
    {
        // Abort if we're within a certain time frame as to not try and get us rate-limited.
        var tmdbShow = RepoFactory.TMDB_Show.GetByTmdbShowID(showId) ?? new(showId);
        if (!forceRefresh && tmdbShow.CreatedAt != tmdbShow.LastUpdatedAt && tmdbShow.LastUpdatedAt < DateTime.Now.AddHours(-1))
            return false;

        var show = await _client.GetTvShowAsync(showId, TvShowMethods.ContentRatings | TvShowMethods.Translations | TvShowMethods.EpisodeGroups, "en-US");
        if (show == null)
            return false;

        var updated = tmdbShow.Populate(show);
        updated |= UpdateTitlesAndOverviews(tmdbShow, show.Translations);
        updated |= UpdateCompanies(tmdbShow, show.ProductionCompanies);
        updated |= await UpdateShowSeasonsAndEpisodes(show, downloadImages, forceRefresh);
        if (downloadAlternateOrdering)
            updated |= await UpdateShowAlternateOrdering(show);
        if (updated)
        {
            tmdbShow.LastUpdatedAt = DateTime.Now;
            RepoFactory.TMDB_Show.Save(tmdbShow);
        }

        if (downloadImages)
            await DownloadShowImages(showId, forceRefresh);

        foreach (var xref in RepoFactory.CrossRef_AniDB_TMDB_Show.GetByTmdbShowID(showId))
            MatchAnidbToTmdbEpisodes(xref.AnidbAnimeID, xref.TmdbShowID, null, true, true);

        return updated;
    }

    private async Task<bool> UpdateShowSeasonsAndEpisodes(TvShow show, bool downloadImages, bool forceRefresh = false)
    {
        var existingSeasons = RepoFactory.TMDB_Season.GetByTmdbShowID(show.Id)
            .ToDictionary(season => season.Id);
        var seasonsToAdd = 0;
        var seasonsToSkip = new HashSet<int>();
        var seasonsToSave = new List<TMDB_Season>();

        var existingEpisodes = RepoFactory.TMDB_Episode.GetByTmdbShowID(show.Id)
            .ToDictionary(episode => episode.Id);
        var episodesToAdd = 0;
        var episodesToSkip = new HashSet<int>();
        var episodesToSave = new List<TMDB_Episode>();
        foreach (var reducedSeason in show.Seasons)
        {
            var season = await _client.GetTvSeasonAsync(show.Id, reducedSeason.SeasonNumber, TvSeasonMethods.Translations) ??
                throw new Exception($"Unable to fetch season {reducedSeason.SeasonNumber} for show \"{show.Name}\".");
            if (!existingSeasons.TryGetValue(reducedSeason.Id, out var tmdbSeason))
            {
                seasonsToAdd++;
                tmdbSeason = new(reducedSeason.Id);
            }

            var seasonUpdated = tmdbSeason.Populate(show, season);
            seasonUpdated |= UpdateTitlesAndOverviews(tmdbSeason, season.Translations);
            if (seasonUpdated)
            {
                tmdbSeason.LastUpdatedAt = DateTime.Now;
                seasonsToSave.Add(tmdbSeason);
            }

            foreach (var episode in season.Episodes)
            {
                if (!existingEpisodes.TryGetValue(episode.Id, out var tmdbEpisode))
                {
                    episodesToAdd++;
                    tmdbEpisode = new(episode.Id);
                }

                var credits = await _client.GetTvEpisodeCreditsAsync(show.Id, season.SeasonNumber, episode.EpisodeNumber);
                var episodeTranslations = await _client.GetTvEpisodeTranslationsAsync(show.Id, season.SeasonNumber, episode.EpisodeNumber);
                var episodeUpdated = tmdbEpisode.Populate(show, season, episode, episodeTranslations!);
                episodeUpdated |= UpdateTitlesAndOverviews(tmdbEpisode, episodeTranslations!);
                episodeUpdated |= await UpdateEpisodeCastAndCrew(tmdbEpisode, credits, downloadImages);
                if (episodeUpdated)
                {
                    tmdbEpisode.LastUpdatedAt = DateTime.Now;
                    episodesToSave.Add(tmdbEpisode);
                }

                if (downloadImages)
                    await DownloadEpisodeImages(tmdbEpisode.TmdbEpisodeID, tmdbEpisode.TmdbShowID, tmdbEpisode.SeasonNumber, tmdbEpisode.EpisodeNumber, forceRefresh);

                episodesToSkip.Add(tmdbEpisode.Id);
            }

            if (downloadImages)
                await DownloadSeasonImages(tmdbSeason.TmdbSeasonID, tmdbSeason.TmdbShowID, tmdbSeason.SeasonNumber, forceRefresh);

            seasonsToSkip.Add(tmdbSeason.Id);
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
        RepoFactory.TMDB_Season.Save(seasonsToSave);

        foreach (var season in seasonsToRemove)
            PurgeShowSeason(season);

        RepoFactory.TMDB_Season.Delete(seasonsToRemove);

        _logger.LogDebug(
            "Added/updated/removed/skipped {a}/{u}/{r}/{s} episodes for show {ShowTitle} (Show={ShowId})",
            episodesToAdd,
            episodesToSave.Count - episodesToAdd,
            episodesToRemove.Count,
            existingEpisodes.Count + episodesToAdd - episodesToRemove.Count - episodesToSave.Count,
            show.Name,
            show.Id);
        RepoFactory.TMDB_Episode.Save(episodesToSave);

        foreach (var episode in episodesToRemove)
            PurgeShowEpisode(episode);

        RepoFactory.TMDB_Episode.Delete(episodesToRemove);

        return seasonsToSave.Count > 0 || seasonsToRemove.Count > 0 || episodesToSave.Count > 0 || episodesToRemove.Count > 0;
    }

    private async Task<bool> UpdateShowAlternateOrdering(TvShow show)
    {
        _logger.LogDebug(
            "Checking {count} episode group collections to create alternate orderings for show {ShowTitle} (Show={ShowId})",
            show.EpisodeGroups.Results.Count,
            show.Name,
            show.Id);

        var existingOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbShowID(show.Id)
            .ToDictionary(ordering => ordering.Id);
        var orderingToAdd = 0;
        var orderingToSkip = new HashSet<string>();
        var orderingToSave = new List<TMDB_AlternateOrdering>();

        var existingSeasons = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbShowID(show.Id)
            .ToDictionary(season => season.Id);
        var seasonsToAdd = 0;
        var seasonsToSkip = new HashSet<string>();
        var seasonsToSave = new HashSet<TMDB_AlternateOrdering_Season>();

        var existingEpisodes = RepoFactory.TMDB_AlternateOrdering_Episode.GetByTmdbShowID(show.Id)
            .ToDictionary(episode => episode.Id);
        var episodesToAdd = 0;
        var episodesToSkip = new HashSet<string>();
        var episodesToSave = new List<TMDB_AlternateOrdering_Episode>();

        foreach (var reducedCollection in show.EpisodeGroups.Results)
        {
            // The object sent from the show endpoint doesn't have the groups,
            // we need to send another request for the full episode group
            // collection to get the groups.
            var collection = await _client.GetTvEpisodeGroupsAsync(reducedCollection.Id) ??
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

            var seasonNumberCount = 1;
            foreach (var episodeGroup in collection.Groups)
            {
                var seasonNumber = seasonNumberCount++;
                var episodeNumberCount = 1;

                if (!existingSeasons.TryGetValue(episodeGroup.Id, out var tmdbSeason))
                {
                    seasonsToAdd++;
                    tmdbSeason = new(episodeGroup.Id);
                }

                var seasonUpdated = tmdbSeason.Populate(episodeGroup, collection.Id, show.Id, seasonNumber);
                if (seasonUpdated)
                {
                    tmdbSeason.LastUpdatedAt = DateTime.Now;
                    seasonsToSave.Add(tmdbSeason);
                }

                foreach (var episode in episodeGroup.Episodes)
                {
                    if (!episode.Id.HasValue)
                        continue;

                    var episodeNumber = episodeNumberCount++;
                    var episodeId = episode.Id.Value;

                    if (!existingEpisodes.TryGetValue($"{episodeGroup.Id}:${episodeId}", out var tmdbEpisode))
                    {
                        episodesToAdd++;
                        tmdbEpisode = new(episodeGroup.Id, episodeId);
                    }

                    var episodeUpdated = tmdbEpisode.Populate(collection.Id, show.Id, seasonNumber, episodeNumber);
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

        RepoFactory.TMDB_AlternateOrdering.Save(orderingToSave);
        RepoFactory.TMDB_AlternateOrdering.Delete(orderingToRemove);

        RepoFactory.TMDB_AlternateOrdering_Season.Save(seasonsToSave);
        RepoFactory.TMDB_AlternateOrdering_Season.Delete(seasonsToRemove);

        RepoFactory.TMDB_AlternateOrdering_Episode.Save(episodesToSave);
        RepoFactory.TMDB_AlternateOrdering_Episode.Delete(episodesToRemove);

        return orderingToSave.Count > 0 ||
            orderingToRemove.Count > 0 ||
            seasonsToSave.Count > 0 ||
            seasonsToRemove.Count > 0 ||
            episodesToSave.Count > 0 ||
            episodesToRemove.Count > 0;
    }

    private async Task<bool> UpdateEpisodeCastAndCrew(TMDB_Episode tmdbEpisode, CreditsWithGuestStars credits, bool downloadImages)
    {
        var peopleToAdd = 0;
        var peopleToSave = new List<TMDB_Person>();
        var knownPeopleDict = new Dictionary<int, TMDB_Person>();

        var counter = 0;
        var castToAdd = 0;
        var castToKeep = new HashSet<string>();
        var castToSave = new List<TMDB_Episode_Cast>();
        var existingCastDict = RepoFactory.TMDB_Episode_Cast.GetByTmdbEpisodeID(tmdbEpisode.Id)
            .ToDictionary(cast => cast.TmdbCreditID);
        var guestOffset = credits.Cast.Count;
        foreach (var cast in credits.Cast.Concat(credits.GuestStars))
        {
            var ordering = counter++;
            var isGuestRole = ordering >= guestOffset;
            castToKeep.Add(cast.CreditId);
            if (!knownPeopleDict.TryGetValue(cast.Id, out var tmdbPerson))
            {
                var person = await _client.GetPersonAsync(cast.Id, PersonMethods.Translations) ??
                    throw new Exception($"Unable to get TMDB Person with id {cast.Id}. (Show={tmdbEpisode.TmdbShowID},Season={tmdbEpisode.TmdbSeasonID},Episode={tmdbEpisode.Id},Person={cast.Id})");

                tmdbPerson = RepoFactory.TMDB_Person.GetByTmdbPersonID(cast.Id);
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
        var existingCrewDict = RepoFactory.TMDB_Episode_Crew.GetByTmdbEpisodeID(tmdbEpisode.Id)
            .ToDictionary(crew => crew.TmdbCreditID);
        foreach (var crew in credits.Crew)
        {
            crewToKeep.Add(crew.CreditId);
            if (!knownPeopleDict.TryGetValue(crew.Id, out var tmdbPerson))
            {
                var person = await _client.GetPersonAsync(crew.Id, PersonMethods.Translations) ??
                    throw new Exception($"Unable to get TMDB Person with id {crew.Id}. (Show={tmdbEpisode.TmdbShowID},Season={tmdbEpisode.TmdbSeasonID},Episode={tmdbEpisode.Id},Person={crew.Id})");

                tmdbPerson = RepoFactory.TMDB_Person.GetByTmdbPersonID(crew.Id);
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

        RepoFactory.TMDB_Person.Save(peopleToSave);
        RepoFactory.TMDB_Episode_Cast.Save(castToSave);
        RepoFactory.TMDB_Episode_Crew.Save(crewToSave);
        RepoFactory.TMDB_Episode_Cast.Delete(castToRemove);
        RepoFactory.TMDB_Episode_Crew.Delete(crewToRemove);

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

    public async Task DownloadShowImages(int showId, bool forceDownload = false)
    {
        var settings = _settingsProvider.GetSettings();
        var images = await _client.GetTvShowImagesAsync(showId);
        if (settings.TMDB.AutoDownloadPosters)
            DownloadImagesByType(images.Posters, ImageEntityType.Poster, ForeignEntityType.Show, showId, settings.TMDB.MaxAutoBackdrops, forceDownload);
        if (settings.TMDB.AutoDownloadLogos)
            DownloadImagesByType(images.Logos, ImageEntityType.Logo, ForeignEntityType.Show, showId, settings.TMDB.MaxAutoBackdrops, forceDownload);
        if (settings.TMDB.AutoDownloadBackdrops)
            DownloadImagesByType(images.Backdrops, ImageEntityType.Backdrop, ForeignEntityType.Show, showId, settings.TMDB.MaxAutoBackdrops, forceDownload);
    }

    private async Task DownloadSeasonImages(int seasonId, int showId, int seasonNumber, bool forceDownload = false)
    {
        var settings = _settingsProvider.GetSettings();
        var images = await _client.GetTvSeasonImagesAsync(showId, seasonNumber);
        if (settings.TMDB.AutoDownloadPosters)
            DownloadImagesByType(images.Posters, ImageEntityType.Poster, ForeignEntityType.Season, seasonId, settings.TMDB.MaxAutoBackdrops, forceDownload);
    }

    private async Task DownloadEpisodeImages(int episodeId, int showId, int seasonNumber, int episodeNumber, bool forceDownload)
    {
        var settings = _settingsProvider.GetSettings();
        var images = await _client.GetTvEpisodeImagesAsync(showId, seasonNumber, episodeNumber);
        if (settings.TMDB.AutoDownloadThumbnails)
            DownloadImagesByType(images.Stills, ImageEntityType.Thumbnail, ForeignEntityType.Episode, episodeId, settings.TMDB.MaxAutoBackdrops, forceDownload);
    }

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> MatchAnidbToTmdbEpisodes(int anidbAnimeId, int tmdbShowId, int? tmdbSeasonId, bool useExisting = false, bool saveToDatabase = false)
    {
        var existing = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetAllByAnidbAnimeAndTmdbShowIDs(anidbAnimeId, tmdbShowId)
            .GroupBy(xref => xref.AnidbEpisodeID)
            .ToDictionary(grouped => grouped.Key, grouped => grouped.ToList());
        var toSkip = new HashSet<int>();
        var toSave = new List<CrossRef_AniDB_TMDB_Episode>();

        var animeSeries = RepoFactory.AnimeSeries.GetByAnimeID(anidbAnimeId);
        if (animeSeries == null)
            return new List<CrossRef_AniDB_TMDB_Episode>();

        var anidbEpisodes = RepoFactory.AniDB_Episode.GetByAnimeID(anidbAnimeId)
            .Where(episode => episode.EpisodeType is (int)EpisodeType.Episode or (int)EpisodeType.Special)
            .ToDictionary(episode => episode.EpisodeID);
        var tmdbEpisodes = RepoFactory.TMDB_Episode.GetByTmdbShowID(tmdbShowId)
            .Where(episode => episode.SeasonNumber == 0 || !tmdbSeasonId.HasValue || episode.TmdbSeasonID == tmdbSeasonId.Value)
            .ToList();
        var tmdbNormalEpisodes = tmdbEpisodes
            .Where(episode => episode.SeasonNumber != 0)
            .OrderBy(episode => episode.SeasonNumber)
            .ThenBy(episode => episode.EpisodeNumber)
            .ToList();
        var tmdbSpeicalEpisodes = tmdbEpisodes
            .Where(episode => episode.SeasonNumber == 0)
            .OrderBy(episode => episode.EpisodeNumber)
            .ToList();

        var crossReferences = useExisting
            ? existing
                .Where(pair => anidbEpisodes.ContainsKey(pair.Key))
                .SelectMany(pair => pair.Value)
                .ToList()
            : new();

        // Mapping logic
        foreach (var episode in anidbEpisodes.Values)
        {
            if (useExisting && existing.TryGetValue(episode.EpisodeID, out var existingLinks))
            {
                foreach (var link in existingLinks)
                    toSkip.Add(link.CrossRef_AniDB_TMDB_EpisodeID);
            }
            else
            {
                var isSpecial = episode.EpisodeType is (int)EpisodeType.Special;
                var episodeList = isSpecial ? tmdbSpeicalEpisodes : tmdbNormalEpisodes;
                var crossRef = TryFindAnidbAndTmdbMatch(episode, episodeList, isSpecial);
                if (crossRef.TmdbEpisodeID != 0)
                {
                    var index = episodeList.FindIndex(episode => episode.TmdbEpisodeID == crossRef.TmdbEpisodeID);
                    if (index != -1)
                        episodeList.RemoveAt(index);
                }
                toSave.Add(crossRef);
            }
        }

        // Save state to db, remove old links if needed
        if (saveToDatabase)
        {
            // Remove the anidb episodes that does not overlap with our show.
            var toRemove = existing.Values
                .SelectMany(list => list)
                .Where(xref => anidbEpisodes.ContainsKey(xref.AnidbEpisodeID) && !toSkip.Contains(xref.CrossRef_AniDB_TMDB_EpisodeID))
                .ToList();

            _logger.LogDebug(
                "Added/removed/skipped {a}/{r}/{s} anidb/tmdb episode cross-references for show {ShowTitle} (Anime={AnimeId},Show={ShowId})",
                toSave.Count,
                toRemove.Count,
                existing.Count + toSave.Count - toRemove.Count - toSave.Count,
                animeSeries.GetSeriesName(),
                anidbAnimeId,
                tmdbShowId);
            RepoFactory.CrossRef_AniDB_TMDB_Episode.Save(toSave);
            RepoFactory.CrossRef_AniDB_TMDB_Episode.Delete(toRemove);
        }

        return crossReferences;
    }

    private static CrossRef_AniDB_TMDB_Episode TryFindAnidbAndTmdbMatch(AniDB_Episode anidbEpisode, IReadOnlyList<TMDB_Episode> tmdbEpisodes, bool isSpecial)
    {
        var anidbDate = anidbEpisode.GetAirDateAsDateOnly();
        var anidbTitles = RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(anidbEpisode.EpisodeID, TitleLanguage.English)
            .Where(title => !title.Title.Trim().Equals($"Episode {anidbEpisode.EpisodeNumber}", StringComparison.InvariantCultureIgnoreCase))
            .ToList();

        var airdateProbability = tmdbEpisodes
            .Select(episode => (episode, probablility: CalculateAirDateProbability(anidbDate, episode.AiredAt)))
            .Where(result => result.probablility != 0)
            .OrderByDescending(result => result.probablility)
            .ToList();
        var titleSearchResults = anidbTitles.Count > 0 ? tmdbEpisodes
            .Select(episode => anidbTitles.Search(episode.EnglishTitle, title => new string[] { title.Title }, true, 1).FirstOrDefault()?.Map(episode))
            .OfType<SeriesSearch.SearchResult<TMDB_Episode>>()
            .OrderBy(result => result)
            .ToList() : new();

        // title first, then date
        if (isSpecial)
        {
            if (titleSearchResults.Count > 0)
            {
                var tmdbEpisode = titleSearchResults[0]!.Result;
                var dateAndTitleMatches = airdateProbability.Any(result => result.episode == tmdbEpisode);
                var rating = dateAndTitleMatches ? MatchRating.DateAndTitleMatches : MatchRating.TitleMatches;
                return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, tmdbEpisode.TmdbEpisodeID, tmdbEpisode.TmdbShowID, rating);
            }

            if (airdateProbability.Count > 0)
            {
                var tmdbEpisode = airdateProbability[0]!.episode;
                return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, tmdbEpisode.TmdbEpisodeID, tmdbEpisode.TmdbShowID, MatchRating.DateMatches);
            }
        }
        // date first, then title
        else
        {
            if (airdateProbability.Count > 0)
            {
                var tmdbEpisode = airdateProbability[0]!.episode;
                var dateAndTitleMatches = titleSearchResults.Any(result => result.Result == tmdbEpisode);
                var rating = dateAndTitleMatches ? MatchRating.DateAndTitleMatches : MatchRating.DateMatches;
                return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, tmdbEpisode.TmdbEpisodeID, tmdbEpisode.TmdbShowID, rating);
            }

            if (titleSearchResults.Count > 0)
            {
                var tmdbEpisode = titleSearchResults[0]!.Result;
                return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, tmdbEpisode.TmdbEpisodeID, tmdbEpisode.TmdbShowID, MatchRating.TitleMatches);
            }
        }

        if (tmdbEpisodes.Count > 0)
            return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, tmdbEpisodes[0].TmdbEpisodeID, tmdbEpisodes[0].TmdbShowID, MatchRating.FirstAvailable);

        return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, 0, 0, MatchRating.SarahJessicaParker);
    }

    private static double CalculateAirDateProbability(DateOnly? firstDate, DateOnly? secondDate, int maxDifferenceInDays = 2)
    {
        if (!firstDate.HasValue || !secondDate.HasValue)
            return 0;

        var difference = Math.Abs(secondDate.Value.DayNumber - firstDate.Value.DayNumber);
        if (difference == 0)
            return 1;

        if (difference <= maxDifferenceInDays)
            return (maxDifferenceInDays - difference) / (double)maxDifferenceInDays;

        return 0;
    }

    #endregion

    #region Purge

    public void PurgeAllUnusedShows()
    {
        var allShows = RepoFactory.TMDB_Show.GetAll().Select(show => show.TmdbShowID)
            .Concat(RepoFactory.TMDB_Image.GetAll().Where(image => image.TmdbShowID.HasValue).Select(image => image.TmdbShowID!.Value))
            .Concat(RepoFactory.CrossRef_AniDB_TMDB_Show.GetAll().Select(xref => xref.TmdbShowID))
            .Concat(RepoFactory.TMDB_Company_Entity.GetAll().Where(x => x.TmdbEntityType == ForeignEntityType.Show).Select(x => x.TmdbEntityID))
            .Concat(RepoFactory.TMDB_Show_Network.GetAll().Select(x => x.TmdbShowID))
            .Concat(RepoFactory.TMDB_Season.GetAll().Select(x => x.TmdbShowID))
            .Concat(RepoFactory.TMDB_Episode.GetAll().Select(x => x.TmdbShowID))
            .Concat(RepoFactory.TMDB_AlternateOrdering.GetAll().Select(ordering => ordering.TmdbShowID))
            .Concat(RepoFactory.TMDB_AlternateOrdering_Season.GetAll().Select(season => season.TmdbShowID))
            .Concat(RepoFactory.TMDB_AlternateOrdering_Episode.GetAll().Select(episode => episode.TmdbShowID))
            .ToHashSet();
        var toKeep = RepoFactory.CrossRef_AniDB_TMDB_Show.GetAll()
            .Select(xref => xref.TmdbShowID)
            .ToHashSet();
        var toBePurged = allShows
            .Except(toKeep)
            .ToHashSet();

        _logger.LogInformation("Scheduling {Count} out of {AllCount} shows to be purged.", toBePurged.Count, allShows.Count);
        foreach (var showID in toBePurged)
            _commandFactory.CreateAndSave<CommandRequest_TMDB_Show_Purge>(c => c.TmdbShowID = showID);
    }

    public bool PurgeShow(int showId, bool removeImageFiles = true)
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showId);
        var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByTmdbShowID(showId);
        if (xrefs.Count > 0)
            foreach (var xref in xrefs)
                RemoveShowLink(xref, removeImageFiles, false);

        PurgeImages(ForeignEntityType.Show, showId, removeImageFiles);

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
            RepoFactory.TMDB_Show.Delete(show);
        }

        return false;
    }

    private void PurgeShowCompanies(int showId, bool removeImageFiles = true)
    {
        var xrefsToRemove = RepoFactory.TMDB_Company_Entity.GetByTmdbEntityTypeAndID(ForeignEntityType.Show, showId);
        foreach (var xref in xrefsToRemove)
        {
            // Delete xref or purge company.
            var xrefs = RepoFactory.TMDB_Company_Entity.GetByTmdbCompanyID(xref.TmdbCompanyID);
            if (xrefs.Count > 1)
                RepoFactory.TMDB_Company_Entity.Delete(xref);
            else
                PurgeCompany(xref.TmdbCompanyID, removeImageFiles);
        }
    }

    private void PurgeShowNetworks(int showId, bool removeImageFiles = true)
    {
        var xrefsToRemove = RepoFactory.TMDB_Show_Network.GetByTmdbShowID(showId);
        foreach (var xref in xrefsToRemove)
        {
            // Delete xref or purge company.
            var xrefs = RepoFactory.TMDB_Show_Network.GetByTmdbNetworkID(xref.TmdbNetworkID);
            if (xrefs.Count > 1)
                RepoFactory.TMDB_Show_Network.Delete(xref);
            else
                PurgeShowNetwork(xref.TmdbNetworkID, removeImageFiles);
        }
    }

    private void PurgeShowNetwork(int networkId, bool removeImageFiles = true)
    {
        var tmdbNetwork = RepoFactory.TMDB_Network.GetByTmdbNetworkID(networkId);
        if (tmdbNetwork != null)
        {
            _logger.LogDebug("Removing TMDB Network (Network={NetworkId})", networkId);
            RepoFactory.TMDB_Network.Delete(tmdbNetwork);
        }

        var images = RepoFactory.TMDB_Image.GetByTmdbCompanyID(networkId);
        if (images.Count > 0)
            foreach (var image in images)
                PurgeImage(image, ForeignEntityType.Company, removeImageFiles);

        var xrefs = RepoFactory.TMDB_Show_Network.GetByTmdbNetworkID(networkId);
        if (xrefs.Count > 0)
        {
            _logger.LogDebug("Removing {count} cross-references for TMDB Network (Network={NetworkId})", xrefs.Count, networkId);
            RepoFactory.TMDB_Show_Network.Delete(xrefs);
        }
    }

    private void PurgeShowEpisodes(int showId, bool removeImageFiles = true)
    {
        var episodesToRemove = RepoFactory.TMDB_Episode.GetByTmdbShowID(showId);

        _logger.LogDebug(
            "Removing {count} episodes for show (Show={ShowId})",
            episodesToRemove.Count,
            showId
        );
        foreach (var episode in episodesToRemove)
            PurgeShowEpisode(episode, removeImageFiles);

        RepoFactory.TMDB_Episode.Delete(episodesToRemove);
    }

    private void PurgeShowEpisode(TMDB_Episode episode, bool removeImageFiles = true)
    {
        PurgeImages(ForeignEntityType.Episode, episode.Id, removeImageFiles);

        PurgeTitlesAndOverviews(ForeignEntityType.Episode, episode.Id);
    }

    private void PurgeShowSeasons(int showId, bool removeImageFiles = true)
    {
        var seasonsToRemove = RepoFactory.TMDB_Season.GetByTmdbShowID(showId);

        _logger.LogDebug(
            "Removing {count} seasons for show (Show={ShowId})",
            seasonsToRemove.Count,
            showId
        );
        foreach (var season in seasonsToRemove)
            PurgeShowSeason(season, removeImageFiles);

        RepoFactory.TMDB_Season.Delete(seasonsToRemove);
    }

    private void PurgeShowSeason(TMDB_Season season, bool removeImageFiles = true)
    {
        PurgeImages(ForeignEntityType.Season, season.Id, removeImageFiles);

        PurgeTitlesAndOverviews(ForeignEntityType.Season, season.Id);
    }

    private void PurgeShowCastAndCrew(int showId, bool removeImageFiles = true)
    {
        var castMembers = RepoFactory.TMDB_Episode_Cast.GetByTmdbShowID(showId);
        var crewMembers = RepoFactory.TMDB_Episode_Crew.GetByTmdbShowID(showId);

        RepoFactory.TMDB_Episode_Cast.Delete(castMembers);
        RepoFactory.TMDB_Episode_Crew.Delete(crewMembers);

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
        var episodes = RepoFactory.TMDB_AlternateOrdering_Episode.GetByTmdbShowID(showId);
        var seasons = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbShowID(showId);
        var orderings = RepoFactory.TMDB_AlternateOrdering.GetByTmdbShowID(showId);

        _logger.LogDebug("Removing {EpisodeCount} episodes and {SeasonCount} seasons across {OrderingCount} alternate orderings for show. (Show={ShowId})", episodes.Count, seasons.Count, orderings.Count, showId);
        RepoFactory.TMDB_AlternateOrdering_Episode.Delete(episodes);
        RepoFactory.TMDB_AlternateOrdering_Season.Delete(seasons);
        RepoFactory.TMDB_AlternateOrdering.Delete(orderings);
    }

    #endregion

    #endregion

    #region Shared

    #region Image

    private void DownloadImageByType(string filePath, ImageEntityType type, ForeignEntityType foreignType, int foreignId, bool forceDownload = false)
    {
        var image = RepoFactory.TMDB_Image.GetByRemoteFileNameAndType(filePath, type) ?? new(filePath, type);
        image.Populate(foreignType, foreignId);
        RepoFactory.TMDB_Image.Save(image);

        _commandFactory.CreateAndSave<CommandRequest_DownloadImage>(c =>
        {
            c.EntityID = image.TMDB_ImageID;
            c.DataSourceEnum = DataSourceType.TMDB;
            c.ForceDownload = forceDownload;
        });
    }

    private void DownloadImagesByType(IReadOnlyList<ImageData> images, ImageEntityType type, ForeignEntityType foreignType, int foreignId, int maxCount, bool forceDownload = false)
    {
        var count = 0;
        foreach (var imageData in images)
        {
            if (count >= maxCount)
                break;

            var image = RepoFactory.TMDB_Image.GetByRemoteFileNameAndType(imageData.FilePath, type) ?? new(imageData.FilePath, type);
            image.Populate(imageData, foreignType, foreignId);
            RepoFactory.TMDB_Image.Save(image);

            var path = image.LocalPath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                count++;
        }

        foreach (var image in RepoFactory.TMDB_Image.GetByForeignIDAndType(foreignId, foreignType, type))
        {
            var path = image.LocalPath;
            if (count < maxCount)
            {
                // Clean up outdated entries.
                if (string.IsNullOrEmpty(path))
                {
                    RepoFactory.TMDB_Image.Delete(image.TMDB_ImageID);
                    continue;
                }

                // Skip downloading if it already exists.
                if (File.Exists(path))
                {
                    count++;
                    continue;
                }

                // Scheduled the image to be downloaded.
                _commandFactory.CreateAndSave<CommandRequest_DownloadImage>(c =>
                {
                    c.EntityID = image.TMDB_ImageID;
                    c.DataSourceEnum = DataSourceType.TMDB;
                    c.ForceDownload = forceDownload;
                });
                count++;
            }
            // Keep it if it's already downloaded, otherwise remove the metadata.
            else if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                RepoFactory.TMDB_Image.Delete(image.TMDB_ImageID);
            }
        }
    }

    private void PurgeImages(ForeignEntityType foreignType, int foreignId, bool removeImageFiles)
    {
        var imagesToRemove = RepoFactory.TMDB_Image.GetByForeignID(foreignId, foreignType);

        _logger.LogDebug(
            "Removing {count} images for {type} with id {EntityId}",
            imagesToRemove.Count,
            foreignType.ToString().ToLowerInvariant(),
            foreignId);
        foreach (var image in imagesToRemove)
            PurgeImage(image, foreignType, removeImageFiles);
    }

    private static void PurgeImage(TMDB_Image image, ForeignEntityType foreignType, bool removeFile)
    {
        // Skip the operation if th flag is not set.
        if (!image.ForeignType.HasFlag(foreignType))
            return;

        // Disable the flag.
        image.ForeignType &= ~foreignType;

        // Only delete the image metadata and/or file if all references were removed.
        if (image.ForeignType == ForeignEntityType.None)
        {
            if (removeFile && !string.IsNullOrEmpty(image.LocalPath) && File.Exists(image.LocalPath))
                File.Delete(image.LocalPath);

            RepoFactory.TMDB_Image.Delete(image.TMDB_ImageID);
        }
        // Remove the ID since we're keeping the metadata a little bit longer.
        else
        {
            switch (foreignType)
            {
                case ForeignEntityType.Movie:
                    image.TmdbMovieID = null;
                    break;
                case ForeignEntityType.Episode:
                    image.TmdbEpisodeID = null;
                    break;
                case ForeignEntityType.Season:
                    image.TmdbSeasonID = null;
                    break;
                case ForeignEntityType.Show:
                    image.TmdbShowID = null;
                    break;
                case ForeignEntityType.Collection:
                    image.TmdbCollectionID = null;
                    break;
            }
        }
    }

    private void ResetPreferredImage(int anidbAnimeId, ForeignEntityType foreignType, int foreignId)
    {
        var images = RepoFactory.AniDB_Anime_PreferredImage.GetByAnimeID(anidbAnimeId);
        foreach (var defaultImage in images)
        {
            if (defaultImage.ImageSource == DataSourceType.TMDB)
            {
                var image = RepoFactory.TMDB_Image.GetByID(defaultImage.ImageID);
                if (image == null)
                {
                    _logger.LogTrace("Removing preferred image for anime {AnimeId} because the preferred image could not be found.", anidbAnimeId);
                    RepoFactory.AniDB_Anime_PreferredImage.Delete(defaultImage);
                }
                else if (image.ForeignType.HasFlag(foreignType) && image.GetForeignID(foreignType) == foreignId)
                {
                    _logger.LogTrace("Removing preferred image for anime {AnimeId} because it belongs to now TMDB {Type} {Id}", anidbAnimeId, foreignType.ToString(), foreignId);
                    RepoFactory.AniDB_Anime_PreferredImage.Delete(defaultImage);
                }
            }
        }
    }

    #endregion

    #region Titles & Overviews

    /// <summary>
    /// Updates the titles and overviews for the <paramref name="tmdbEntity"/>
    /// using the translation data available in the <paramref name="translations"/>.
    /// </summary>
    /// <param name="tmdbEntity">The local TMDB Entity to update titles and overviews for.</param>
    /// <param name="translations">The translations container returned from the API.</param>
    /// <returns>A boolean indicating if any changes were made to the titles and/or overviews.</returns>
    private bool UpdateTitlesAndOverviews(IEntityMetadata tmdbEntity, TranslationsContainer translations)
    {
        var existingOverviews = RepoFactory.TMDB_Overview.GetByParentTypeAndID(tmdbEntity.Type, tmdbEntity.Id);
        var existingTitles = RepoFactory.TMDB_Title.GetByParentTypeAndID(tmdbEntity.Type, tmdbEntity.Id);
        var overviewsToAdd = 0;
        var overviewsToSkip = new HashSet<int>();
        var overviewsToSave = new List<TMDB_Overview>();
        var titlesToAdd = 0;
        var titlesToSkip = new HashSet<int>();
        var titlesToSave = new List<TMDB_Title>();
        foreach (var translation in translations.Translations)
        {
            var languageCode = translation.Iso_639_1?.ToLowerInvariant();
            var countryCode = translation.Iso_3166_1?.ToUpperInvariant();

            var currentTitle = translation.Data.Name ?? string.Empty;
            if (!string.IsNullOrEmpty(tmdbEntity.OriginalLanguageCode) && languageCode == tmdbEntity.OriginalLanguageCode)
                currentTitle = tmdbEntity.OriginalTitle ?? translation.Data.Name ?? string.Empty;
            else if (languageCode == "en" && countryCode == "US")
                currentTitle = tmdbEntity.EnglishTitle ?? translation.Data.Name ?? string.Empty;
            var existingTitle = existingTitles.FirstOrDefault(title => title.LanguageCode == languageCode && title.CountryCode == countryCode);
            if (!string.IsNullOrEmpty(currentTitle) && !(
                // Make sure the "translation" is not just the English Title or
                (languageCode != "en" && languageCode != "US" && string.Equals(tmdbEntity.EnglishTitle, currentTitle, StringComparison.InvariantCultureIgnoreCase)) ||
                // the Original Title.
                (!string.IsNullOrEmpty(tmdbEntity.OriginalLanguageCode) && languageCode != tmdbEntity.OriginalLanguageCode && string.Equals(tmdbEntity.OriginalTitle, currentTitle, StringComparison.InvariantCultureIgnoreCase))
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

            var currentOverview = translation.Data.Overview ?? string.Empty;
            if (languageCode == "en" && countryCode == "US")
                currentOverview = tmdbEntity.EnglishOverview ?? translation.Data.Overview ?? string.Empty;
            var existingOverview = existingOverviews.FirstOrDefault(overview => overview.LanguageCode == languageCode && overview.CountryCode == countryCode);
            if (!string.IsNullOrEmpty(currentOverview))
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
            tmdbEntity.OriginalTitle,
            tmdbEntity.Type.ToString(),
            tmdbEntity.Id);
        RepoFactory.TMDB_Overview.Save(overviewsToSave);
        RepoFactory.TMDB_Overview.Delete(overviewsToRemove);
        RepoFactory.TMDB_Title.Save(titlesToSave);
        RepoFactory.TMDB_Title.Delete(titlesToRemove);

        return overviewsToSave.Count > 0 ||
            overviewsToRemove.Count > 0 ||
            titlesToSave.Count > 0 ||
            titlesToRemove.Count > 0;
    }

    private void PurgeTitlesAndOverviews(ForeignEntityType foreignType, int foreignId)
    {
        var overviewsToRemove = RepoFactory.TMDB_Overview.GetByParentTypeAndID(foreignType, foreignId);
        var titlesToRemove = RepoFactory.TMDB_Title.GetByParentTypeAndID(foreignType, foreignId);

        _logger.LogDebug(
            "Removing {tr} titles and {or} overviews for {type} with id {EntityId}",
            titlesToRemove.Count,
            overviewsToRemove.Count,
            foreignType.ToString().ToLowerInvariant(),
            foreignId);
        RepoFactory.TMDB_Overview.Delete(overviewsToRemove);
        RepoFactory.TMDB_Title.Delete(titlesToRemove);
    }

    #endregion

    #region Companies

    private bool UpdateCompanies(IEntityMetadata tmdbEntity, List<ProductionCompany> companies)
    {
        var existingXrefs = RepoFactory.TMDB_Company_Entity.GetByTmdbEntityTypeAndID(tmdbEntity.Type, tmdbEntity.Id)
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

            UpdateCompany(company);
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

        RepoFactory.TMDB_Company_Entity.Save(xrefsToSave);
        foreach (var xref in xrefsToRemove)
        {
            // Delete xref or purge company.
            var xrefs = RepoFactory.TMDB_Company_Entity.GetByTmdbCompanyID(xref.TmdbCompanyID);
            if (xrefs.Count > 1)
                RepoFactory.TMDB_Company_Entity.Delete(xref);
            else
                PurgeCompany(xref.TmdbCompanyID);
        }


        return false;
    }

    private void UpdateCompany(ProductionCompany company)
    {
        var tmdbCompany = RepoFactory.TMDB_Company.GetByTmdbCompanyID(company.Id) ?? new(company.Id);
        var updated = tmdbCompany.Populate(company);
        if (updated)
        {
            _logger.LogDebug("Updating TMDB Company (Company={CompanyId})", company.Id);
            RepoFactory.TMDB_Company.Save(tmdbCompany);
        }

        if (!string.IsNullOrEmpty(company.LogoPath))
            DownloadImageByType(company.LogoPath, ImageEntityType.Logo, ForeignEntityType.Company, company.Id);
    }

    private void PurgeCompany(int companyId, bool removeImageFiles = true)
    {
        var tmdbCompany = RepoFactory.TMDB_Company.GetByTmdbCompanyID(companyId);
        if (tmdbCompany != null)
        {
            _logger.LogDebug("Removing TMDB Company (Company={CompanyId})", companyId);
            RepoFactory.TMDB_Company.Delete(tmdbCompany);
        }

        var images = RepoFactory.TMDB_Image.GetByTmdbCompanyID(companyId);
        if (images.Count > 0)
            foreach (var image in images)
                PurgeImage(image, ForeignEntityType.Company, removeImageFiles);

        var xrefs = RepoFactory.TMDB_Company_Entity.GetByTmdbCompanyID(companyId);
        if (xrefs.Count > 0)
        {
            _logger.LogDebug("Removing {count} cross-references for TMDB Company (Company={CompanyId})", xrefs.Count, companyId);
            RepoFactory.TMDB_Company_Entity.Delete(xrefs);
        }
    }

    #endregion

    #region People

    public async Task DownloadPersonImages(int personId, bool forceDownload = false)
    {
        var settings = _settingsProvider.GetSettings();
        var images = await _client.GetPersonImagesAsync(personId);
        if (settings.TMDB.AutoDownloadStaffImages)
            DownloadImagesByType(images.Profiles, ImageEntityType.Person, ForeignEntityType.Person, personId, settings.TMDB.MaxAutoStaffImages, forceDownload);
    }

    private void PurgePerson(int personId, bool removeImageFiles = true)
    {
        var person = RepoFactory.TMDB_Person.GetByTmdbPersonID(personId);
        if (person != null)
        {
            _logger.LogDebug("Removing TMDB Person (Person={PersonId})", personId);
            RepoFactory.TMDB_Person.Delete(person);
        }

        var images = RepoFactory.TMDB_Image.GetByTmdbPersonID(personId);
        if (images.Count > 0)
            foreach (var image in images)
                PurgeImage(image, ForeignEntityType.Person, removeImageFiles);

        var movieCast = RepoFactory.TMDB_Movie_Cast.GetByTmdbPersonID(personId);
        if (movieCast.Count > 0)
        {
            _logger.LogDebug("Removing {count} movie cast roles for TMDB Person (Person={PersonId})", movieCast.Count, personId);
            RepoFactory.TMDB_Movie_Cast.Delete(movieCast);
        }

        var movieCrew = RepoFactory.TMDB_Movie_Crew.GetByTmdbPersonID(personId);
        if (movieCrew.Count > 0)
        {
            _logger.LogDebug("Removing {count} movie crew roles for TMDB Person (Person={PersonId})", movieCrew.Count, personId);
            RepoFactory.TMDB_Movie_Crew.Delete(movieCrew);
        }

        var episodeCast = RepoFactory.TMDB_Episode_Cast.GetByTmdbPersonID(personId);
        if (episodeCast.Count > 0)
        {
            _logger.LogDebug("Removing {count} show cast roles for TMDB Person (Person={PersonId})", episodeCast.Count, personId);
            RepoFactory.TMDB_Episode_Cast.Delete(episodeCast);
        }

        var episodeCrew = RepoFactory.TMDB_Episode_Crew.GetByTmdbPersonID(personId);
        if (episodeCrew.Count > 0)
        {
            _logger.LogDebug("Removing {count} show crew roles for TMDB Person (Person={PersonId})", episodeCrew.Count, personId);
            RepoFactory.TMDB_Episode_Crew.Delete(episodeCrew);
        }
    }

    private static bool IsPersonLinkedToOtherEntities(int tmdbPersonId)
    {
        var movieCastLinks = RepoFactory.TMDB_Movie_Cast.GetByTmdbPersonID(tmdbPersonId);
        if (movieCastLinks.Any())
            return true;

        var movieCrewLinks = RepoFactory.TMDB_Movie_Crew.GetByTmdbPersonID(tmdbPersonId);
        if (movieCrewLinks.Any())
            return true;

        var episodeCastLinks = RepoFactory.TMDB_Episode_Cast.GetByTmdbPersonID(tmdbPersonId);
        if (episodeCastLinks.Any())
            return true;

        var episodeCrewLinks = RepoFactory.TMDB_Episode_Crew.GetByTmdbPersonID(tmdbPersonId);
        if (episodeCrewLinks.Any())
            return true;

        return false;
    }
    #endregion

    #endregion
}
