using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Models.Enums;
using Shoko.Server.Commands;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB.Search;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using TMDbLib.Client;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.TvShows;

using MovieCredits = TMDbLib.Objects.Movies.Credits;
using ShowCredits = TMDbLib.Objects.TvShows.Credits;

namespace Shoko.Server.Providers.TMDB;

public class TMDBHelper
{
    private readonly ILogger<TMDBHelper> _logger;

    private readonly ICommandRequestFactory _commandFactory;

    private readonly ISettingsProvider _settingsProvider;

    public readonly TMDBOfflineSearch OfflineSearch;

    private readonly TMDbClient _client;

    private static string _imageServerUrl = null;

    public static string ImageServerUrl =>
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

    public List<Movie> SearchMovies(string query)
    {
        var results = _client.SearchMovie(query);

        _logger.LogInformation("Got {Count} of {Results} results", results.Results.Count, results.TotalResults);
        return results.Results
            .Select(result => _client.GetMovie(result.Id))
            .ToList();
    }

    #endregion

    #region Links

    public void AddMovieLink(int animeId, int movieId, int? episodeId = null, bool additiveLink = false, bool isAutomatic = false, bool forceRefresh = false)
    {
        // Remove all existing links.
        if (!additiveLink)
            RemoveAllMovieLinks(animeId);

        // Add or update the link.
        _logger.LogInformation("Adding TMDB Movie Link: AniDB (ID:{AnidbID}) → TvDB Movie (ID:{TmdbID})", animeId, movieId);
        var xref = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeAndTmdbMovieIDs(animeId, movieId) ??
            new(animeId, movieId);
        if (episodeId.HasValue)
            xref.AnidbEpisodeID = episodeId;
        xref.Source = isAutomatic ? CrossRefSource.Automatic : CrossRefSource.User;
        RepoFactory.CrossRef_AniDB_TMDB_Movie.Save(xref);

        // Schedule the movie info to be downloaded or updated.
        _commandFactory.CreateAndSave<CommandRequest_TMDB_Movie_Update>(c =>
        {
            c.TmdbMovieID = movieId;
            c.ForceRefresh = forceRefresh;
            c.DownloadImages = true;
        });
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

    public async Task<bool> UpdateMovie(int movieId, bool forceRefresh = false, bool downloadImages = false, bool downloadCollections = false)
    {
        // Abort if we're within a certain time frame as to not try and get us rate-limited.
        var tmdbMovie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieId) ?? new();
        if (!forceRefresh && tmdbMovie.CreatedAt != tmdbMovie.LastUpdatedAt && tmdbMovie.LastUpdatedAt < DateTime.Now.AddHours(-1))
            return false;

        // Abort if we couldn't find the movie by id.
        var movie = await _client.GetMovieAsync(movieId, "en", null, MovieMethods.Translations | MovieMethods.Credits);
        if (movie == null)
            return false;

        var updated = tmdbMovie.Populate(movie);
        updated |= UpdateTitlesAndOverviews(tmdbMovie, movie.Translations);
        updated |= UpdateCompanies(tmdbMovie, movie.ProductionCompanies);
        updated |= UpdateMovieCastAndCast(tmdbMovie, movie.Credits);
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

    private bool UpdateMovieCastAndCast(TMDB_Movie tmdbMovie, MovieCredits credits)
    {
        // TODO: Add cast / crew

        return false;
    }

    private async Task UpdateMovieCollections(Movie movie)
    {
        var collectionId = movie.BelongsToCollection?.Id;
        if (collectionId.HasValue)
        {
            var movieXRefs = RepoFactory.TMDB_Collection_Movie.GetByTmdbCollectionID(collectionId.Value);
            var tmdbCollection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(collectionId.Value) ?? new(collectionId.Value);
            var collection = await _client.GetCollectionAsync(collectionId.Value, TMDbLib.Objects.Collections.CollectionMethods.Images);
            if (collection == null)
            {
                PurgeMovieCollection(collectionId.Value);
                return;
            }

            var updated = tmdbCollection.Populate(collection);
            // TODO: Waiting for https://github.com/Jellyfin/TMDbLib/pull/446 to be merged to uncomment the next line.
            updated |= UpdateTitlesAndOverviews(tmdbCollection, null); // collection.Translations);

            var xrefsToAdd = 0;
            var xrefsToSave = new List<TMDB_Collection_Movie>();
            var xrefsToRemove = movieXRefs.Where(xref => collection.Parts.Any(part => xref.TmdbMovieID == part.Id)).ToList();
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
        var allMovies = RepoFactory.MovieDb_Movie.GetAll().Select(movie => movie.MovieId)
            .Concat(RepoFactory.TMDB_Image.GetAll().Where(image => image.TmdbMovieID.HasValue).Select(image => image.TmdbMovieID.Value))
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
            _logger.LogTrace("Removing movie {MovieName} ({MovieID})", movie.OriginalTitle, movie.Id);
            RepoFactory.TMDB_Movie.Delete(movie);
        }

        PurgeMovieImages(movieId, removeImageFiles);

        PurgeMovieCompanies(movieId, removeImageFiles);

        CleanupMovieCollection(movieId);

        PurgeTitlesAndOverviews(ForeignEntityType.Movie, movieId);
    }

    private static void PurgeMovieImages(int movieId, bool removeImageFiles = true)
    {
        var images = RepoFactory.TMDB_Image.GetByTmdbMovieID(movieId);
        if (images != null & images.Count > 0)
            foreach (var image in images)
                PurgeImage(image, ForeignEntityType.Movie, removeImageFiles);
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
        var images = RepoFactory.TMDB_Image.GetByTmdbCollectionID(collectionId);
        if (images != null & images.Count > 0)
            foreach (var image in images)
                PurgeImage(image, ForeignEntityType.Collection, removeImageFiles);

        var collection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(collectionId);
        if (collection != null)
        {
            _logger.LogTrace(
                "Removing movie collection {MovieName} ({MovieID})",
                collection.EnglishTitle,
                collectionId
            );
            RepoFactory.TMDB_Collection.Delete(collection);
        }

        var collectionXRefs = RepoFactory.TMDB_Collection_Movie.GetByTmdbCollectionID(collectionId);
        if (collectionXRefs.Count > 0)
        {
            _logger.LogTrace(
                "Removing {Count} cross-references for movie collection {CollectionName} ({MovieID})",
                collectionXRefs.Count, collection?.EnglishTitle ?? string.Empty,
                collectionId
            );
            RepoFactory.TMDB_Collection_Movie.Delete(collectionXRefs);
        }

        PurgeTitlesAndOverviews(ForeignEntityType.Collection, collectionId);
    }

    #endregion

    #endregion

    #region Show

    #region Search

    public List<TvShow> SearchShows(string query)
    {
        // TODO: Implement search after finalising the search model.
        return default;
    }

    #endregion

    #region Links

    public void AddShowLink(int animeId, int showId, int? seasonId = null, bool additiveLink = true, bool isAutomatic = false, bool forceRefresh = false)
    {
        // Remove all existing links.
        if (!additiveLink)
            RemoveAllShowLinks(animeId);

        // Add or update the link.
        _logger.LogInformation("Adding TMDB Show Link: AniDB (ID:{AnidbID}) → TvDB Show (ID:{TmdbID})", animeId, showId);
        var xref = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeAndTmdbShowIDs(animeId, showId) ??
            new(animeId, showId);
        if (seasonId.HasValue)
            xref.TmdbSeasonID = seasonId;
        xref.Source = isAutomatic ? CrossRefSource.Automatic : CrossRefSource.User;
        RepoFactory.CrossRef_AniDB_TMDB_Show.Save(xref);

        // Schedule the movie info to be downloaded or updated.
        _commandFactory.CreateAndSave<CommandRequest_TMDB_Show_Update>(c =>
        {
            c.TmdbShowID = showId;
            c.ForceRefresh = true;
            c.DownloadImages = true;
        });
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
        if (xref.TmdbSeasonID.HasValue)
            ResetPreferredImage(xref.AnidbAnimeID, ForeignEntityType.Season, xref.TmdbSeasonID.Value);

        _logger.LogInformation("Removing TMDB Show Link: AniDB ({AnidbID}) → TMDB Show (ID:{TmdbID})", xref.AnidbAnimeID, xref.TmdbShowID);
        RepoFactory.CrossRef_AniDB_TMDB_Show.Delete(xref);

        var anidbEpisodes = RepoFactory.AniDB_Episode.GetByAnimeID(xref.AnidbAnimeID);
        var tmdbEpisodes = RepoFactory.TMDB_Episode.GetByTmdbShowID(xref.TmdbShowID)
            .Select(episode => episode.TmdbEpisodeID)
            .ToHashSet();
        var xrefs = anidbEpisodes
            .SelectMany(episode => RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbEpisodeID(episode.AniDB_EpisodeID).Where(xref => tmdbEpisodes.Contains(xref.TmdbEpisodeID)))
            .ToList();
        _logger.LogInformation("Removing {XRefsCount} Show Episodes for AniDB Anime ({AnidbID})", xrefs.Count, xref.AnidbAnimeID);
        RepoFactory.CrossRef_AniDB_TMDB_Episode.Delete(xrefs);

        if (purge ?? RepoFactory.CrossRef_AniDB_TMDB_Show.GetByTmdbShowID(xref.TmdbShowID).Count == 0)
            _commandFactory.CreateAndSave<CommandRequest_TMDB_Show_Purge>(c =>
            {
                c.TmdbShowID = xref.TmdbShowID;
                c.RemoveImageFiles = removeImageFiles;
            });
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

    public async Task UpdateShow(int showId, bool force = false, bool downloadImages = false, bool downloadEpisodeGroups = false)
    {
        // TODO: Abort if we're within a certain time frame as to not try and get us rate-limited.

        var show = await _client.GetTvShowAsync(showId);

        // TODO: Update show.

        await Task.WhenAll(
            UpdateShowTitlesAndOverviews(show),
            UpdateShowEpisodes(show),
            UpdateShowSeasons(show),
            downloadEpisodeGroups ? UpdateShowEpisodeGroups(show) : Task.CompletedTask,
            downloadImages ? DownloadShowImages(showId) : Task.CompletedTask
        );
    }

    private async Task UpdateShowTitlesAndOverviews(TvShow show)
    {
        var translations = await _client.GetTvShowTranslationsAsync(show.Id);

        // TODO: Add/update/remove show titles.
    }

    private async Task UpdateShowEpisodes(TvShow show)
    {
        // TODO: Update TMDB episodes, check for xrefs, auto-add xrefs that does not exist, etc.
    }

    private async Task UpdateShowSeasons(TvShow show)
    {
        // TODO: Update TMDB seasons.
    }

    private async Task UpdateShowEpisodeGroups(TvShow show)
    {
        // TODO: Update TMDB episode groups.
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

    #endregion

    #region Purge

    public void PurgeAllUnusedShows()
    {
        // TODO: Implement this logic once the show tables are added and the repositories are set up.
        var allShows = new HashSet<int>();
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
        var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByTmdbShowID(showId);
        if (xrefs.Count > 0)
            foreach (var xref in xrefs)
                RemoveShowLink(xref, removeImageFiles, false);

        PurgeShowImages(showId, removeImageFiles);

        PurgeShowCompanies(showId, removeImageFiles);

        PurgeShowEpisodes(showId);

        PurgeShowSeasons(showId);

        PurgeShowEpisodeGroups(showId);

        // TODO: Remove show.

        return false;
    }

    private static void PurgeShowImages(int showId, bool removeFiles = true)
    {
        var images = RepoFactory.TMDB_Image.GetByTmdbShowID(showId);
        if (images != null & images.Count > 0)
            foreach (var image in images)
                PurgeImage(image, ForeignEntityType.Movie, removeFiles);
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

    private static void PurgeShowEpisodes(int showId, bool removeImageFiles = true)
    {
        // TODO: Remove Episodes and their images.
    }

    private static void PurgeShowSeasons(int showId)
    {
        // TODO: Remove Seasons.
    }

    private static void PurgeShowEpisodeGroups(int showId)
    {
        // TODO: Remove all episode groups.
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
    private bool UpdateTitlesAndOverviews(IEntityMetatadata tmdbEntity, TranslationsContainer translations)
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

            var currentTitle = translation.Name ?? string.Empty;
            if (!string.IsNullOrEmpty(tmdbEntity.OriginalLanguageCode) && languageCode == tmdbEntity.OriginalLanguageCode)
                currentTitle = tmdbEntity.OriginalTitle ?? translation.Name ?? string.Empty;
            else if (languageCode == "en" && countryCode == "US")
                currentTitle = tmdbEntity.EnglishTitle ?? translation.Name ?? string.Empty;
            var existingTitle = existingTitles.FirstOrDefault(title => title.LanguageCode == languageCode && title.CountryCode == countryCode);
            if (!string.IsNullOrEmpty(currentTitle))
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
            "Added/updated/removed/skipped {ta}/{tu}/{tr}/{ts} titles and {oa}/{ou}/{or}/{os} overviews for {type} {EntityTitle} (Id={EntityId})",
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
            "Removed {tr} titles and {or} overviews for {type} with id {EntityId}",
            titlesToRemove.Count,
            overviewsToRemove.Count,
            foreignType.ToString().ToLowerInvariant(),
            foreignId);
        RepoFactory.TMDB_Overview.Delete(overviewsToRemove);
        RepoFactory.TMDB_Title.Delete(titlesToRemove);
    }

    #endregion

    #region Companies


    private bool UpdateCompanies(IEntityMetatadata tmdbEntity, List<ProductionCompany> companies)
    {
        var existingXrefs = RepoFactory.TMDB_Company_Entity.GetByTmdbEntityTypeAndID(tmdbEntity.Type, tmdbEntity.Id);
        var xrefsToAdd = 0;
        var xrefsToSkip = new HashSet<int>();
        var xrefsToSave = new List<TMDB_Company_Entity>();
        var indexCounter = 0;
        foreach (var company in companies)
        {
            var currentIndex = indexCounter++;
            var existingXref = existingXrefs.FirstOrDefault(xref => xref.TmdbCompanyID == company.Id);
            if (existingXref == null)
            {
                xrefsToAdd++;
                xrefsToSave.Add(new(company.Id, tmdbEntity.Type, tmdbEntity.Id, currentIndex, tmdbEntity.ReleasedAt));
            }
            else
            {
                if (existingXref.Index != currentIndex || existingXref.ReleasedAt != tmdbEntity.ReleasedAt)
                {
                    existingXref.Index = currentIndex;
                    existingXref.ReleasedAt = tmdbEntity.ReleasedAt;
                    xrefsToSave.Add(existingXref);
                }
                xrefsToSkip.Add(existingXref.TMDB_Company_EntityID);
            }

            UpdateCompany(company);
        }
        var xrefsToRemove = existingXrefs.ExceptBy(xrefsToSkip, o => o.TMDB_Company_EntityID).ToList();

        _logger.LogDebug(
            "Added/updated/removed/skipped {oa}/{ou}/{or}/{os} company cross-references for {type} {EntityTitle} (Id={EntityId})",
            xrefsToAdd,
            xrefsToSave.Count - xrefsToAdd,
            xrefsToRemove.Count,
            xrefsToSkip.Count + xrefsToAdd - xrefsToSave.Count,
            tmdbEntity.Type.ToString().ToLowerInvariant(),
            tmdbEntity.OriginalTitle,
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
            _logger.LogDebug("");
            RepoFactory.TMDB_Company.Save(tmdbCompany);
        }

        DownloadImageByType(company.LogoPath, ImageEntityType.Logo, ForeignEntityType.Company, company.Id);
    }

    private void PurgeCompany(int companyId, bool removeImageFiles = true)
    {
        var tmdbCompany = RepoFactory.TMDB_Company.GetByTmdbCompanyID(companyId);
        if (tmdbCompany != null)
        {
            _logger.LogDebug("");
            RepoFactory.TMDB_Company.Delete(tmdbCompany);
        }

        var images = RepoFactory.TMDB_Image.GetByTmdbCompanyID(companyId);
        if (images != null & images.Count > 0)
            foreach (var image in images)
                PurgeImage(image, ForeignEntityType.Company, removeImageFiles);

        var xrefs = RepoFactory.TMDB_Company_Entity.GetByTmdbCompanyID(companyId);
        if (xrefs.Count > 0)
        {
            _logger.LogDebug("");
            RepoFactory.TMDB_Company_Entity.Delete(xrefs);
        }
    }

    #endregion

    #endregion
}
