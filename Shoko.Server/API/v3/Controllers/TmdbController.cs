using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Image.Options;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.AniDB;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.API.v3.Models.TMDB;
using Shoko.Server.API.v3.Models.TMDB.Input;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using File = Shoko.Server.API.v3.Models.Shoko.File;
using TitleLanguage = Shoko.Abstractions.Metadata.Enums.TitleLanguage;

#pragma warning disable CA1822
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public partial class TmdbController(
    ISettingsProvider settingsProvider,
    ILogger<TmdbController> _logger,
    IMetadataLinkingService _linkingService,
    IMetadataRefreshService _metadataRefreshService,
    IMetadataPurgeService _metadataPurgeService,
    IMetadataCrossReferenceTransferService _crossReferenceTransferService,
    IMetadataOrderingService _orderingService,
    IImageManager _imageManager,
    IMetadataService _metadataService,
    ISuspensionService _suspensionService
) : BaseController(settingsProvider)
{
    // A fast 503 with Retry-After while suspended; queues the job first unless the caller waits for it.
    // Not the safety net: the queue already holds back every job of a suspended provider.
    private async Task<ActionResult?> TryQueueWhenPaused(Func<Task> queue, string jobDescription, bool immediate)
    {
        var status = SourceSuspension.For(_suspensionService, MetadataSource.TMDB);
        if (!status.IsSuspended) return null;
        var seconds = (int)(status.GetRemainingTime()?.TotalSeconds ?? 0);
        if (immediate)
        {
            _logger.LogInformation("TMDB is currently suspended. {Job} was requested immediately and has been refused; retry in approximately {Seconds} second(s).", jobDescription, seconds);
        }
        else
        {
            _logger.LogInformation("TMDB is currently suspended. {Job} has been queued and will start in approximately {Seconds} second(s).", jobDescription, seconds);
            await queue();
        }
        Response.Headers.RetryAfter = seconds.ToString();
        return StatusCode(503);
    }

    /// <summary>
    ///   Queues a TMDB entry's refresh, or runs it at once, answering 503 while
    ///   TMDB is suspended.
    /// </summary>
    /// <param name="entry">The show, movie or collection.</param>
    /// <param name="force">Whether to refresh it however recently it was.</param>
    /// <param name="options">What to fetch.</param>
    /// <param name="description">What is refreshed, for the log.</param>
    /// <param name="immediate">Whether to run it at once and wait for it.</param>
    /// <returns>200 when it ran, 204 when it was queued, or 503 while TMDB is suspended.</returns>
    private async Task<ActionResult> RefreshTmdbEntry(MetadataGuid entry, bool force, MetadataRefreshOptions options, string description, bool immediate)
    {
        if (await TryQueueWhenPaused(() => _metadataRefreshService.RefreshEntry(entry, force, options, prioritize: true), description, immediate) is { } paused)
            return paused;

        if (immediate)
        {
            await _metadataRefreshService.RefreshEntry(entry, force, options, immediate: true);
            return Ok();
        }

        await _metadataRefreshService.RefreshEntry(entry, force, options);
        return NoContent();
    }

    /// <summary>
    ///   Queues the image download of a TMDB entry, or runs it at once,
    ///   answering 503 while TMDB is suspended.
    /// </summary>
    /// <param name="entry">The show or movie.</param>
    /// <param name="force">Whether to download the images again even when they are there.</param>
    /// <param name="description">What is downloaded, for the log.</param>
    /// <param name="immediate">Whether to run it at once and wait for it.</param>
    /// <returns>200 when it ran, 204 when it was queued, or 503 while TMDB is suspended.</returns>
    private async Task<ActionResult> DownloadTmdbEntryImages(MetadataGuid entry, bool force, string description, bool immediate)
    {
        if (await TryQueueWhenPaused(() => _metadataRefreshService.DownloadImages(entry, force, prioritize: true), description, immediate) is { } paused)
            return paused;

        if (immediate)
        {
            await _metadataRefreshService.DownloadImages(entry, force, immediate: true);
            return Ok();
        }

        await _metadataRefreshService.DownloadImages(entry, force);
        return NoContent();
    }

    /// <summary>
    ///   The identifier of a TMDB show.
    /// </summary>
    /// <param name="showID">The TMDB show ID.</param>
    /// <returns>The identifier.</returns>
    private static MetadataGuid ShowEntry(int showID)
        => new(MetadataSource.TMDB, MetadataEntityType.Series, showID.ToString());

    /// <summary>
    ///   The identifier of a TMDB movie.
    /// </summary>
    /// <param name="movieID">The TMDB movie ID.</param>
    /// <returns>The identifier.</returns>
    private static MetadataGuid MovieEntry(int movieID)
        => new(MetadataSource.TMDB, MetadataEntityType.Movie, movieID.ToString());

    /// <summary>
    ///   What a remote search of TMDB asks for.
    /// </summary>
    /// <param name="query">The text to search for.</param>
    /// <param name="includeRestricted">Whether to include restricted entries.</param>
    /// <param name="year">The year, or <c>0</c> for any.</param>
    /// <param name="page">The page, from <c>1</c>.</param>
    /// <param name="pageSize">The page size, <c>0</c> for only the total.</param>
    /// <returns>The options.</returns>
    private static MetadataSearchOptions SearchOptions(string query, bool includeRestricted, int year, int page, int pageSize)
        => new() { Query = query, IncludeRestricted = includeRestricted, Year = year > 0 ? year : null, Page = page, PageSize = pageSize };

    /// <summary>
    ///   Wait out a running refresh or purge of a TMDB show.
    /// </summary>
    /// <param name="showID">The TMDB show ID.</param>
    /// <returns><c>true</c> when there was one, so a copy read before may be stale.</returns>
    private bool WaitForShowUpdate(int showID)
        => _metadataRefreshService.WaitForRefresh(ShowEntry(showID)).GetAwaiter().GetResult();

    /// <summary>
    ///   Wait out a running refresh or purge of a TMDB movie.
    /// </summary>
    /// <param name="movieID">The TMDB movie ID.</param>
    /// <returns><c>true</c> when there was one, so a copy read before may be stale.</returns>
    private bool WaitForMovieUpdate(int movieID)
        => _metadataRefreshService.WaitForRefresh(MovieEntry(movieID)).GetAwaiter().GetResult();

    /// <summary>
    ///   Answers for a TMDB collection that is not stored, after asking the
    ///   metadata service for it, which queues its fetch when TMDB's
    ///   collection provider is enabled and a linked movie names it.
    /// </summary>
    /// <param name="collectionID">The TMDB collection ID.</param>
    /// <returns>404, as a later request finds the collection once it is fetched.</returns>
    private NotFoundObjectResult CollectionMissing(int collectionID)
    {
        if (collectionID > 0)
            _metadataService.GetCollection(new(MetadataSource.TMDB, MetadataEntityType.Collection, collectionID.ToString()));

        return NotFound(MovieCollectionNotFound);
    }

    /// <summary>
    ///   Wait out a running refresh or purge of a TMDB collection.
    /// </summary>
    /// <param name="collectionID">The TMDB collection ID.</param>
    /// <returns><c>true</c> when there was one, so a copy read before may be stale.</returns>
    private bool WaitForCollectionUpdate(int collectionID)
        => _metadataRefreshService.WaitForRefresh(new(MetadataSource.TMDB, MetadataEntityType.Collection, collectionID.ToString())).GetAwaiter().GetResult();

    #region Movies

    #region Constants

    internal const string MovieNotFound = "A TMDB.Movie by the given `movieID` was not found.";

    #endregion

    #region Basics

    /// <summary>
    ///   The languages whose titles a TMDB search matches: the series title
    ///   languages, and English.
    /// </summary>
    /// <returns>The languages.</returns>
    private HashSet<TitleLanguage> SearchTitleLanguages()
        => SettingsProvider.GetSettings()
            .Language.SeriesTitleLanguageOrder
            .Select(lang => lang.GetTitleLanguage())
            .Concat([TitleLanguage.English])
            .ToHashSet();

    /// <summary>
    /// List all locally available tmdb movies.
    /// </summary>
    /// <param name="search"></param>
    /// <param name="fuzzy"></param>
    /// <param name="include"></param>
    /// <param name="restricted"></param>
    /// <param name="video"></param>
    /// <param name="pageSize"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    [HttpGet("Movie")]
    public ActionResult<ListResult<TmdbMovie>> GetTmdbMovies(
        [FromQuery] string? search = null,
        [FromQuery] bool fuzzy = true,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbMovie.IncludeDetails>? include = null,
        [FromQuery] IncludeOnlyFilter restricted = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter video = IncludeOnlyFilter.True,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var movies = TmdbCompatibility.GetMovies()
            .AsParallel()
            .Where(movie =>
            {
                if (restricted != IncludeOnlyFilter.True)
                {
                    var includeRestricted = restricted == IncludeOnlyFilter.Only;
                    var isRestricted = movie.IsRestricted;
                    if (isRestricted != includeRestricted)
                        return false;
                }

                if (video != IncludeOnlyFilter.True)
                {
                    var includeVideo = video == IncludeOnlyFilter.Only;
                    var isVideo = movie.IsVideo;
                    if (isVideo != includeVideo)
                        return false;
                }

                return true;
            });
        if (!string.IsNullOrWhiteSpace(search))
        {
            var languages = SearchTitleLanguages();
            return movies
                .Search(
                    search,
                    movie => movie.GetAllTitles()
                        .WhereInLanguages(languages)
                        .Select(title => title.Value)
                        .Append(movie.EnglishTitle)
                        .Append(movie.OriginalTitle)
                        .Distinct()
                        .ToList(),
                    fuzzy
                )
                .ToListResult(searchResult =>
                {
                    var movie = searchResult.Result;
                    if (WaitForMovieUpdate(movie.Id))
                        movie = TmdbCompatibility.GetMovie(movie.Id) ?? movie;
                    return new TmdbMovie(movie, include?.CombineFlags());
                }, page, pageSize);
        }

        return movies
            .OrderBy(movie => movie.EnglishTitle)
            .ThenBy(movie => movie.TmdbMovieID)
            .ToListResult(movie =>
            {
                if (WaitForMovieUpdate(movie.Id))
                    movie = TmdbCompatibility.GetMovie(movie.Id) ?? movie;
                return new TmdbMovie(movie, include?.CombineFlags());
            }, page, pageSize);
    }

    [HttpPost("Movie/Bulk")]
    public ActionResult<List<TmdbMovie>> BulkGetTmdbMoviesByMovieIDs([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] TmdbBulkFetchBody<TmdbMovie.IncludeDetails> body) =>
        body.IDs
            .Select(movieID => movieID <= 0 ? null : TmdbCompatibility.GetMovie(movieID))
            .WhereNotNull()
            .Select(movie =>
            {
                if (WaitForMovieUpdate(movie.Id))
                    movie = TmdbCompatibility.GetMovie(movie.Id) ?? movie;
                return new TmdbMovie(movie, body.Include?.CombineFlags(), body.Language);
            })
            .ToList();

    /// <summary>
    /// Get the local metadata for a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <param name="include"></param>
    /// <param name="language"></param>
    /// <returns></returns>
    [HttpGet("Movie/{movieID}")]
    public ActionResult<TmdbMovie> GetTmdbMovieByMovieID(
        [FromRoute] int movieID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbMovie.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is not null && WaitForMovieUpdate(movieID))
            movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return new TmdbMovie(movie, include?.CombineFlags(), language);
    }

    /// <summary>
    /// Remove the local copy of the metadata for a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("Movie/{movieID}")]
    public async Task<ActionResult> RemoveTmdbMovieByMovieID([FromRoute] int movieID)
    {
        await _metadataPurgeService.PurgeEntry(MovieEntry(movieID), force: true);

        return NoContent();
    }

    [HttpGet("Movie/{movieID}/Titles")]
    public ActionResult<IReadOnlyList<Title>> GetTitlesForTmdbMovieByMovieID(
        [FromRoute] int movieID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is not null && WaitForMovieUpdate(movieID))
            movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        var preferredTitle = movie.GetPreferredTitle();
        return new(movie.GetAllTitles().ToTitleDto(movie.EnglishTitle, preferredTitle, language));
    }

    [HttpGet("Movie/{movieID}/Overviews")]
    public ActionResult<IReadOnlyList<Overview>> GetOverviewsForTmdbMovieByMovieID(
        [FromRoute] int movieID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is not null && WaitForMovieUpdate(movieID))
            movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        var preferredOverview = movie.GetPreferredOverview();
        return new(movie.GetAllOverviews().ToOverviewDto(movie.EnglishOverview, preferredOverview, language));
    }

    /// <summary>
    /// Get every image for the TMDB movie, grouped by image type.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID</param>
    /// <param name="includeDisabled">Include disabled images.</param>
    /// <param name="includeUndesired">Include images that are not marked as desired for download.</param>
    /// <param name="includeRemoteUrl">Whether to hand out a URL for fetching each image from its source. Defaults to only doing so for images the server does not hold locally.</param>
    /// <param name="language">Filter the images down to these languages.</param>
    /// <returns>Every image for the movie, grouped by image type.</returns>
    [HttpGet("Movie/{movieID}/Images")]
    public ActionResult<Images> GetImagesForTmdbMovieByMovieID(
        [FromRoute] int movieID,
        [FromQuery] bool includeDisabled = false,
        [FromQuery] bool includeUndesired = false,
        [FromQuery] RemoteUrlInclusion includeRemoteUrl = RemoteUrlInclusion.WhenUnavailable,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is not null && WaitForMovieUpdate(movieID))
            movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        var options = new ImageFilteringOptions { IsEnabled = includeDisabled ? null : true, IsDesired = includeUndesired ? null : true };
        return ((IWithImages)movie).GetImages(options)
            .ToDto(language, includeRemoteUrl: includeRemoteUrl, remoteUrlTemplate: _imageManager.GetTemplateUrlForSource)
            .WithCrossReferences(_imageManager.GetCrossReferencesForImageList(movie, options));
    }

    [HttpGet("Movie/{movieID}/Cast")]
    public ActionResult<IReadOnlyList<Role>> GetCastForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is not null && WaitForMovieUpdate(movieID))
            movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.TmdbCast
            .Select(Role.FromTmdb)
            .WhereNotNull()
            .ToList();
    }

    [HttpGet("Movie/{movieID}/Crew")]
    public ActionResult<IReadOnlyList<Role>> GetCrewForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is not null && WaitForMovieUpdate(movieID))
            movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.TmdbCrew
            .Select(Role.FromTmdb)
            .WhereNotNull()
            .ToList();
    }

    [HttpGet("Movie/{movieID}/CrossReferences")]
    public ActionResult<IReadOnlyList<TmdbMovie.CrossReference>> GetCrossReferencesForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is not null && WaitForMovieUpdate(movieID))
            movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.CrossReferences
            .Select(xref => new TmdbMovie.CrossReference(xref))
            .ToList();
    }

    [HttpGet("Movie/{movieID}/FileCrossReferences")]
    public ActionResult<IReadOnlyList<FileCrossReference>> GetFileCrossReferencesForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return FileCrossReference.From(movie.FileCrossReferences);
    }

    [HttpGet("Movie/{movieID}/Studios")]
    public ActionResult<IReadOnlyList<Studio>> GetStudiosForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is not null && WaitForMovieUpdate(movieID))
            movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.TmdbStudios
            .Select(company => new Studio(company))
            .ToList();
    }

    [HttpGet("Movie/{movieID}/ContentRatings")]
    public ActionResult<IReadOnlyList<ContentRating>> GetContentRatingsForTmdbMovieByMovieID(
        [FromRoute] int movieID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is not null && WaitForMovieUpdate(movieID))
            movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return new(movie.TmdbContentRatings.ToDto(language));
    }

    [HttpGet("Movie/{movieID}/Keywords")]
    public ActionResult<IReadOnlyList<string>> GetKeywordsForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is not null && WaitForMovieUpdate(movieID))
            movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.Keywords.ToList();
    }

    [HttpGet("Movie/{movieID}/ProductionCountries")]
    public ActionResult<IReadOnlyDictionary<string, string>> GetProductionCountriesForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is not null && WaitForMovieUpdate(movieID))
            movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.TmdbProductionCountries.ToDictionary();
    }

    [HttpGet("Movie/{movieID}/YearlySeasons")]
    public ActionResult<IReadOnlyList<SeasonWithYear>> GetYearlySeasonsForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is not null && WaitForMovieUpdate(movieID))
            movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.YearlySeasons.ToV3Dto();
    }

    #endregion

    #region Same-Source Linked Entries

    [HttpGet("Movie/{movieID}/Collection")]
    public ActionResult<TmdbMovie.Collection> GetTmdbMovieCollectionByMovieID(
        [FromRoute] int movieID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbMovie.Collection.IncludeDetails>? include = null
    )
    {
        var movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is not null && WaitForMovieUpdate(movieID))
            movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        var movieCollection = movie.TmdbCollection;
        if (movieCollection is null)
        {
            if (movie.TmdbCollectionID is { } collectionID)
                CollectionMissing(collectionID);
            return NotFound(MovieCollectionByMovieIDNotFound);
        }

        return new TmdbMovie.Collection(movieCollection, include?.CombineFlags());
    }

    #endregion

    #region Cross-Source Linked Entries

    /// <summary>
    /// Get all AniDB series linked to a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <returns></returns>
    [HttpGet("Movie/{movieID}/AniDB/Anime")]
    public ActionResult<List<AnidbAnime>> GetAniDBAnimeByTmdbMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.CrossReferences
            .Select(xref => xref.AnidbAnime)
            .WhereNotNull()
            .Select(anime => new AnidbAnime(anime))
            .ToList();
    }

    /// <summary>
    /// Get all AniDB episodes linked to a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <returns></returns>
    [HttpGet("Movie/{movieID}/AniDB/Episode")]
    public ActionResult<List<AnidbEpisode>> GetAniDBEpisodesByTmdbMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.CrossReferences
            .Select(xref => xref.AnidbEpisode)
            .WhereNotNull()
            .Select(episode => new AnidbEpisode(episode))
            .ToList();
    }

    /// <summary>
    /// Get all Shoko series linked to a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <param name="randomImages">Randomize images shown for the <see cref="Series"/>.</param>
    /// <param name="includeDataFrom">Include data from the selected sources: AniDB, TMDB, or any metadata source a plugin registered, by value, alias or old spelling, whose linked entries are added under <c>Sources</c>.</param>
    /// <returns></returns>
    [HttpGet("Movie/{movieID}/Shoko/Series")]
    public ActionResult<List<Series>> GetShokoSeriesByTmdbMovieID(
        [FromRoute] int movieID,
        [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(MetadataSourceSetModelBinder))] HashSet<MetadataSource>? includeDataFrom = null
    )
    {
        var movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.CrossReferences
            .Select(xref => xref.AnimeSeries)
            .WhereNotNull()
            .Select(series => new Series(series, User.JMMUserID, randomImages, includeDataFrom))
            .ToList();
    }

    /// <summary>
    /// Get all Shoko episodes linked to a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <param name="includeDataFrom">Include data from the selected sources: AniDB, TMDB, or any metadata source a plugin registered, by value, alias or old spelling, whose linked entries are added under <c>Sources</c>.</param>
    /// <returns></returns>
    [HttpGet("Movie/{movieID}/Shoko/Episode")]
    public ActionResult<List<Episode>> GetShokoEpisodesByTmdbMovieID(
        [FromRoute] int movieID,
        [FromQuery, ModelBinder(typeof(MetadataSourceSetModelBinder))] HashSet<MetadataSource>? includeDataFrom = null
    )
    {
        var movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.CrossReferences
            .Select(xref => xref.AnimeEpisode)
            .WhereNotNull()
            .Select(episode => new Episode(HttpContext, episode, includeDataFrom))
            .ToList();
    }

    /// <summary>
    /// Get all files linked to a TMDB Movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <param name="exclude">Exclude items of certain types</param>
    /// <param name="include_only">Filter to only include items of certain types</param>
    /// <param name="releaseProviders">Filter to only include files from certain release providers. Append <c>!</c> to the provider name to exclude the files</param>
    /// <param name="sortOrder">Sort ordering. Attach '-' at the start to reverse the order of the criteria.</param>
    /// <returns></returns>
    [HttpGet("Movie/{movieID}/Shoko/File")]
    public ActionResult<ListResult<File>> GetShokoFilesByMovieID(
        [FromRoute] int movieID,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[]? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileExcludeTypes[]? exclude = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileIncludeOnlyType[]? include_only = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? releaseProviders = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? sortOrder = null
    )
    {
        var movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        var videoLocals = movie.CrossReferences
            .Select(xref => xref.AnimeEpisode)
            .WhereNotNull()
            .SelectMany(xref => xref.VideoLocals)
            .DistinctBy(video => video.VideoLocalID);
        return ModelHelper.FilterFiles(videoLocals, User, pageSize, page, include, exclude, include_only, releaseProviders, sortOrder);
    }

    #endregion

    #region Actions

    /// <summary>
    /// Refresh or download the metadata for a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <param name="body">Body containing options for refreshing or downloading metadata.</param>
    /// <returns>
    /// If <paramref name="body.Immediate"/> is <c>true</c>, returns an <see cref="OkResult"/>,
    /// otherwise returns a <see cref="NoContentResult"/>.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("Movie/{movieID}/Action/Refresh")]
    public async Task<ActionResult> RefreshTmdbMovieByMovieID(
        [FromRoute] int movieID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] TmdbRefreshMovieBody body
    )
    {
        if (body.SkipIfExists)
        {
            var movie = TmdbCompatibility.GetMovie(movieID);
            if (movie is not null)
                return Ok();
        }

        return await RefreshTmdbEntry(
            new(MetadataSource.TMDB, MetadataEntityType.Movie, movieID.ToString()),
            body.Force,
            new()
            {
                DownloadImages = body.DownloadImages,
                Reason = MetadataRefreshReason.Requested,
            },
            "Movie refresh",
            body.Immediate
        );
    }

    /// <summary>
    /// Download images for a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <param name="body">Body containing options for downloading images.</param>
    /// <returns>
    /// If <paramref name="body.Immediate"/> is <c>true</c>, returns an <see cref="OkResult"/>,
    /// otherwise returns a <see cref="NoContentResult"/>.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("Movie/{movieID}/Action/DownloadImages")]
    public async Task<ActionResult> DownloadImagesForTmdbMovieByMovieID(
        [FromRoute] int movieID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] TmdbDownloadImagesBody body
    )
    {
        var movie = TmdbCompatibility.GetMovie(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return await DownloadTmdbEntryImages(new(MetadataSource.TMDB, MetadataEntityType.Movie, movieID.ToString()), body.Force, "Movie image download", body.Immediate);
    }

    #endregion

    #region Online (Search / Bulk / Single)

    /// <summary>
    /// Search TMDB for movies using the offline or online search.
    /// </summary>
    /// <param name="query">Query to search for.</param>
    /// <param name="includeRestricted">Include restricted movies.</param>
    /// <param name="year">First aired year.</param>
    /// <param name="pageSize">The page size. Set to 0 to only grab the total.</param>
    /// <param name="page">The page index.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("Movie/Online/Search")]
    public async Task<ListResult<Search.RemoteSearchMovie>> SearchOnlineForTmdbMovies(
        [FromQuery] string query,
        [FromQuery] bool includeRestricted = false,
        [FromQuery, Range(0, int.MaxValue)] int year = 0,
        [FromQuery, Range(0, 100)] int pageSize = 6,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var (pageView, totalMovies) = await _linkingService.SearchMovies(MetadataSource.TMDB, SearchOptions(query, includeRestricted, year, page, pageSize), HttpContext.RequestAborted);
        return new ListResult<Search.RemoteSearchMovie>(totalMovies, pageView.Select(a => new Search.RemoteSearchMovie(a)));
    }

    /// <summary>
    /// Search for multiple TMDB movies by their IDs.
    /// </summary>
    /// <remarks>
    /// If any of the IDs are not found, a <see cref="ValidationProblemDetails"/> is returned.
    /// </remarks>
    /// <param name="body">Body containing the IDs of the movies to search for.</param>
    /// <returns>
    /// A list of <see cref="Search.RemoteSearchMovie"/> containing the search results.
    /// The order of the returned movies is determined by the order of the IDs in <paramref name="body"/>.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("Movie/Online/Bulk")]
    public async Task<ActionResult<List<Search.RemoteSearchMovie>>> SearchBulkForTmdbMovies(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] TmdbBulkSearchBody body
    )
    {
        // We don't care if the inputs are non-unique, but we don't want to double fetch,
        // so we do a distinct here, then at the end we map back to the original order.
        var uniqueIds = body.IDs.Distinct().ToList();
        var movieDict = uniqueIds
            .Select(id => id <= 0 ? null : TmdbCompatibility.GetMovie(id))
            .WhereNotNull()
            .Select(movie => new Search.RemoteSearchMovie(movie))
            .ToDictionary(movie => movie.ID);
        foreach (var id in uniqueIds.Except(movieDict.Keys))
        {
            if (id <= 0 || await _linkingService.LookupMovie(MovieEntry(id), HttpContext.RequestAborted) is not { } movie)
                continue;

            movieDict[id] = new Search.RemoteSearchMovie(movie);
        }

        var unknownMovies = uniqueIds.Except(movieDict.Keys).ToList();
        if (unknownMovies.Count > 0)
        {
            foreach (var id in unknownMovies)
                ModelState.AddModelError(nameof(body.IDs), $"Movie with id '{id}' not found.");

            return ValidationProblem(ModelState);
        }

        return body.IDs
            .Select(id => movieDict[id])
            .ToList();
    }

    /// <summary>
    /// Search TMDB for a movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <returns>
    /// If the movie is already in the database, returns the local copy.
    /// Otherwise, returns the remote copy from TMDB.
    /// If the movie is not found on TMDB, returns 404.
    /// </returns>
    [HttpGet("Movie/Online/{movieID}")]
    public async Task<ActionResult<Search.RemoteSearchMovie>> SearchOnlineForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        if (TmdbCompatibility.GetMovie(movieID) is { } localMovie)
            return new Search.RemoteSearchMovie(localMovie);

        if (movieID <= 0 || await _linkingService.LookupMovie(MovieEntry(movieID), HttpContext.RequestAborted) is not { } remoteMovie)
            return NotFound("Movie not found on TMDB.");

        return new Search.RemoteSearchMovie(remoteMovie);
    }

    #endregion

    #endregion

    #region Movie Collection

    #region Constants

    internal const string MovieCollectionNotFound = "A TMDB.MovieCollection by the given `collectionID` was not found.";

    internal const string MovieCollectionByMovieIDNotFound = "A TMDB.MovieCollection by the given `movieID` was not found.";

    #endregion

    #region Basics

    [HttpGet("Movie/Collection")]
    public ActionResult<ListResult<TmdbMovie.Collection>> GetMovieCollections(
        [FromQuery] string search,
        [FromQuery] bool fuzzy = true,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbMovie.Collection.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var languages = SearchTitleLanguages();
            return TmdbCompatibility.GetCollections()
                .Search(
                    search,
                    collection => collection.GetAllTitles()
                        .WhereInLanguages(languages)
                        .Select(title => title.Value)
                        .Append(collection.EnglishTitle)
                        .Distinct()
                        .ToList(),
                    fuzzy
                )
                .ToListResult(searchResult =>
            {
                var movieCollection = searchResult.Result;
                if (WaitForCollectionUpdate(movieCollection.Id))
                    movieCollection = TmdbCompatibility.GetCollection(movieCollection.Id) ?? movieCollection;
                return new TmdbMovie.Collection(movieCollection, include?.CombineFlags(), language);
            }, page, pageSize);
        }

        return TmdbCompatibility.GetCollections()
            .ToListResult(movieCollection =>
            {
                if (WaitForCollectionUpdate(movieCollection.Id))
                    movieCollection = TmdbCompatibility.GetCollection(movieCollection.Id) ?? movieCollection;
                return new TmdbMovie.Collection(movieCollection, include?.CombineFlags(), language);
            }, page, pageSize);
    }

    [HttpGet("Movie/Collection/{collectionID}")]
    public ActionResult<TmdbMovie.Collection> GetMovieCollectionByCollectionID(
        [FromRoute] int collectionID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbMovie.Collection.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var collection = TmdbCompatibility.GetCollection(collectionID);
        if (collection is not null && WaitForCollectionUpdate(collection.Id))
            collection = TmdbCompatibility.GetCollection(collection.Id);
        if (collection is null)
            return CollectionMissing(collectionID);

        return new TmdbMovie.Collection(collection, include?.CombineFlags(), language);
    }

    [HttpGet("Movie/Collection/{collectionID}/Titles")]
    public ActionResult<IReadOnlyList<Title>> GetTitlesForMovieCollectionByCollectionID(
        [FromRoute] int collectionID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var collection = TmdbCompatibility.GetCollection(collectionID);
        if (collection is not null && WaitForCollectionUpdate(collection.Id))
            collection = TmdbCompatibility.GetCollection(collection.Id);
        if (collection is null)
            return CollectionMissing(collectionID);

        var preferredTitle = collection.GetPreferredTitle();
        return new(collection.GetAllTitles().ToTitleDto(collection.EnglishTitle, preferredTitle, language));
    }

    [HttpGet("Movie/Collection/{collectionID}/Overviews")]
    public ActionResult<IReadOnlyList<Overview>> GetOverviewsForMovieCollectionByCollectionID(
        [FromRoute] int collectionID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var collection = TmdbCompatibility.GetCollection(collectionID);
        if (collection is not null && WaitForCollectionUpdate(collection.Id))
            collection = TmdbCompatibility.GetCollection(collection.Id);
        if (collection is null)
            return CollectionMissing(collectionID);

        var preferredOverview = collection.GetPreferredOverview();
        return new(collection.GetAllOverviews().ToOverviewDto(collection.EnglishOverview, preferredOverview, language));
    }

    /// <summary>
    /// Get every image for the TMDB movie collection, grouped by image type.
    /// </summary>
    /// <param name="collectionID">TMDB Collection ID</param>
    /// <param name="includeDisabled">Include disabled images.</param>
    /// <param name="includeUndesired">Include images that are not marked as desired for download.</param>
    /// <param name="includeRemoteUrl">Whether to hand out a URL for fetching each image from its source. Defaults to only doing so for images the server does not hold locally.</param>
    /// <param name="language">Filter the images down to these languages.</param>
    /// <returns>Every image for the movie collection, grouped by image type.</returns>
    [HttpGet("Movie/Collection/{collectionID}/Images")]
    public ActionResult<Images> GetImagesForMovieCollectionByCollectionID(
        [FromRoute] int collectionID,
        [FromQuery] bool includeDisabled = false,
        [FromQuery] bool includeUndesired = false,
        [FromQuery] RemoteUrlInclusion includeRemoteUrl = RemoteUrlInclusion.WhenUnavailable,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var collection = TmdbCompatibility.GetCollection(collectionID);
        if (collection is not null && WaitForCollectionUpdate(collection.Id))
            collection = TmdbCompatibility.GetCollection(collection.Id);
        if (collection is null)
            return CollectionMissing(collectionID);

        var options = new ImageFilteringOptions { IsEnabled = includeDisabled ? null : true, IsDesired = includeUndesired ? null : true };
        return ((IWithImages)collection).GetImages(options)
            .ToDto(language, includeRemoteUrl: includeRemoteUrl, remoteUrlTemplate: _imageManager.GetTemplateUrlForSource)
            .WithCrossReferences(_imageManager.GetCrossReferencesForImageList(collection, options));
    }

    #endregion

    #region Same-Source Linked Entries

    [HttpGet("Movie/Collection/{collectionID}/Movie")]
    public ActionResult<List<TmdbMovie>> GetMoviesForMovieCollectionByCollectionID(
        [FromRoute] int collectionID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbMovie.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var collection = TmdbCompatibility.GetCollection(collectionID);
        if (collection is not null && WaitForCollectionUpdate(collection.Id))
            collection = TmdbCompatibility.GetCollection(collection.Id);
        if (collection is null)
            return CollectionMissing(collectionID);

        return collection.GetTmdbMovies()
            .Select(movie =>
            {
                if (WaitForMovieUpdate(movie.Id))
                    movie = TmdbCompatibility.GetMovie(movie.Id) ?? movie;
                return new TmdbMovie(movie, include?.CombineFlags(), language);
            })
            .ToList();
    }

    #endregion

    #endregion

    #region Shows

    #region Constants

    internal const string AlternateOrderingIdRegex = @"^(?:[0-9]{1,23}|[a-f0-9]{24}|default)$";

    internal const string AlternateOrderingDisabled = "default";

    internal const string ShowNotFound = "A TMDB.Show by the given `showID` was not found.";

    internal const string ShowNotFoundBySeasonID = "A TMDB.Show by the given `seasonID` was not found";

    internal const string ShowNotFoundByOrderingID = "A TMDB.Show by the given `orderingID` was not found";

    internal const string ShowNotFoundByEpisodeID = "A TMDB.Show by the given `episodeID` was not found";

    #endregion

    #region Basics

    /// <summary>
    /// List all locally available tmdb shows.
    /// </summary>
    /// <param name="search"></param>
    /// <param name="fuzzy"></param>
    /// <param name="include"></param>
    /// <param name="language"></param>
    /// <param name="restricted"></param>
    /// <param name="pageSize"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    [HttpGet("Show")]
    public ActionResult<ListResult<TmdbShow>> GetTmdbShows(
        [FromQuery] string? search = null,
        [FromQuery] bool fuzzy = true,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbShow.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery] IncludeOnlyFilter restricted = IncludeOnlyFilter.True,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var shows = TmdbCompatibility.GetShows()
            .AsParallel()
            .Where(show =>
            {
                if (restricted != IncludeOnlyFilter.True)
                {
                    var includeRestricted = restricted == IncludeOnlyFilter.Only;
                    var isRestricted = show.IsRestricted;
                    if (isRestricted != includeRestricted)
                        return false;
                }

                return true;
            });
        if (!string.IsNullOrWhiteSpace(search))
        {
            var languages = SearchTitleLanguages();
            return shows
                .Search(
                    search,
                    show => show.GetAllTitles()
                        .WhereInLanguages(languages)
                        .Select(title => title.Value)
                        .Append(show.EnglishTitle)
                        .Append(show.OriginalTitle)
                        .Distinct()
                        .ToList(),
                    fuzzy
                )
                .ToListResult(searchResult =>
                {
                    var show = searchResult.Result;
                    if (WaitForShowUpdate(show.Id))
                        show = TmdbCompatibility.GetShow(show.Id) ?? show;

                    var alternateOrdering = (TmdbCompatibility.AlternateOrdering?)null;
                    if (!string.IsNullOrWhiteSpace(show.PreferredAlternateOrderingID))
                        alternateOrdering = TmdbCompatibility.GetAlternateOrdering(show.PreferredAlternateOrderingID);

                    return new TmdbShow(show, alternateOrdering, include?.CombineFlags(), language);
                }, page, pageSize);
        }

        return shows
            .OrderBy(show => show.EnglishTitle)
            .ThenBy(show => show.TmdbShowID)
            .ToListResult(show =>
            {
                if (WaitForShowUpdate(show.Id))
                    show = TmdbCompatibility.GetShow(show.Id) ?? show;

                var alternateOrdering = (TmdbCompatibility.AlternateOrdering?)null;
                if (!string.IsNullOrWhiteSpace(show.PreferredAlternateOrderingID))
                    alternateOrdering = TmdbCompatibility.GetAlternateOrdering(show.PreferredAlternateOrderingID);

                return new TmdbShow(show, alternateOrdering, include?.CombineFlags(), language);
            }, page, pageSize);
    }

    [HttpPost("Show/Bulk")]
    public ActionResult<List<TmdbShow>> BulkGetTmdbShowsByShowIDs([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] TmdbBulkFetchBody<TmdbShow.IncludeDetails> body) =>
        body.IDs
            .Select(showID => showID <= 0 ? null : TmdbCompatibility.GetShow(showID))
            .WhereNotNull()
            .Select(show =>
            {
                if (WaitForShowUpdate(show.Id))
                    show = TmdbCompatibility.GetShow(show.Id) ?? show;

                var alternateOrdering = (TmdbCompatibility.AlternateOrdering?)null;
                if (!string.IsNullOrWhiteSpace(show.PreferredAlternateOrderingID))
                    alternateOrdering = TmdbCompatibility.GetAlternateOrdering(show.PreferredAlternateOrderingID);

                return new TmdbShow(show, alternateOrdering, body.Include?.CombineFlags(), body.Language);
            })
            .ToList();

    /// <summary>
    /// Get the local metadata for a TMDB show.
    /// </summary>
    /// <returns></returns>
    [HttpGet("Show/{showID}")]
    public ActionResult<TmdbShow> GetTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbShow.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        if (string.Equals(AlternateOrderingDisabled, alternateOrderingID, StringComparison.OrdinalIgnoreCase))
            alternateOrderingID = show.Id.ToString();

        if (string.IsNullOrEmpty(alternateOrderingID) && !string.IsNullOrWhiteSpace(show.PreferredAlternateOrderingID))
            alternateOrderingID = show.PreferredAlternateOrderingID;

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            if (alternateOrderingID.Length == SeasonIdHexLength)
            {
                var alternateOrdering = TmdbCompatibility.GetAlternateOrdering(alternateOrderingID);
                if (alternateOrdering is null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                    return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

                return new TmdbShow(show, alternateOrdering, include?.CombineFlags(), language);
            }

            if (alternateOrderingID != show.Id.ToString())
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");
        }

        return new TmdbShow(show, include?.CombineFlags());
    }

    /// <summary>
    /// Remove the local copy of the metadata for a TMDB show.
    /// </summary>
    /// <param name="showID">TMDB Movie ID.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("Show/{showID}")]
    public async Task<ActionResult> RemoveTmdbShowByShowID([FromRoute] int showID)
    {
        await _metadataPurgeService.PurgeEntry(ShowEntry(showID), force: true);

        return NoContent();
    }

    [HttpGet("Show/{showID}/Titles")]
    public ActionResult<IReadOnlyList<Title>> GetTitlesForTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        var preferredTitle = show.GetPreferredTitle();
        return new(show.GetAllTitles().ToTitleDto(show.EnglishTitle, preferredTitle, language));
    }

    [HttpGet("Show/{showID}/Overviews")]
    public ActionResult<IReadOnlyList<Overview>> GetOverviewsForTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        var preferredOverview = show.GetPreferredOverview();
        return new(show.GetAllOverviews().ToOverviewDto(show.EnglishOverview, preferredOverview, language));
    }

    /// <summary>
    /// Get every image for the TMDB show, grouped by image type.
    /// </summary>
    /// <param name="showID">TMDB Show ID</param>
    /// <param name="includeDisabled">Include disabled images.</param>
    /// <param name="includeUndesired">Include images that are not marked as desired for download.</param>
    /// <param name="includeRemoteUrl">Whether to hand out a URL for fetching each image from its source. Defaults to only doing so for images the server does not hold locally.</param>
    /// <param name="language">Filter the images down to these languages.</param>
    /// <returns>Every image for the show, grouped by image type.</returns>
    [HttpGet("Show/{showID}/Images")]
    public ActionResult<Images> GetImagesForTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery] bool includeDisabled = false,
        [FromQuery] bool includeUndesired = false,
        [FromQuery] RemoteUrlInclusion includeRemoteUrl = RemoteUrlInclusion.WhenUnavailable,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        var options = new ImageFilteringOptions { IsEnabled = includeDisabled ? null : true, IsDesired = includeUndesired ? null : true };
        return ((IWithImages)show).GetImages(options)
            .ToDto(language, includeRemoteUrl: includeRemoteUrl, remoteUrlTemplate: _imageManager.GetTemplateUrlForSource)
            .WithCrossReferences(_imageManager.GetCrossReferencesForImageList(show, options));
    }

    [HttpGet("Show/{showID}/Ordering")]
    public ActionResult<IReadOnlyList<TmdbShow.OrderingInformation>> GetOrderingForTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        if (string.Equals(AlternateOrderingDisabled, alternateOrderingID, StringComparison.OrdinalIgnoreCase))
            alternateOrderingID = show.Id.ToString();

        if (string.IsNullOrEmpty(alternateOrderingID) && !string.IsNullOrWhiteSpace(show.PreferredAlternateOrderingID))
            alternateOrderingID = show.PreferredAlternateOrderingID;

        if (!string.IsNullOrWhiteSpace(alternateOrderingID) && alternateOrderingID.Length != SeasonIdHexLength && alternateOrderingID != show.Id.ToString())
            return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

        var alternateOrdering = (TmdbCompatibility.AlternateOrdering?)null;
        if (!string.IsNullOrWhiteSpace(alternateOrderingID) && alternateOrderingID != show.Id.ToString())
        {
            alternateOrdering = !string.IsNullOrWhiteSpace(alternateOrderingID) ? TmdbCompatibility.GetAlternateOrdering(alternateOrderingID) : null;
            if (alternateOrdering is null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");
        }

        var ordering = new List<TmdbShow.OrderingInformation>
        {
            new(show, alternateOrdering),
        };
        var preferredOrderingID = show.PreferredAlternateOrderingID;
        foreach (var altOrder in show.TmdbAlternateOrdering)
            ordering.Add(new(altOrder, preferredOrderingID, alternateOrdering));
        return ordering
            .OrderByDescending(o => o.IsDefault)
            .ThenBy(o => o.OrderingType)
            .ThenBy(o => o.OrderingName)
            .ToList();
    }

    /// <summary>
    /// Choose the ordering to use for a TMDB show, or go back to its default
    /// one. Only the choice changes; the orderings stay TMDB's own.
    /// </summary>
    /// <remarks>
    /// Superseded by <c>POST /api/v3/Metadata/tmdb/Series/{showID}/Orderings/SetPreferred</c>,
    /// which takes a full ordering ID and works for every source.
    /// </remarks>
    /// <param name="showID">TMDB Show ID.</param>
    /// <param name="body">The ordering to choose.</param>
    /// <returns>Nothing.</returns>
    [Authorize("admin")]
    [Obsolete("Use POST /api/v3/Metadata/tmdb/Series/{showID}/Orderings/SetPreferred instead.")]
    [HttpPost("Show/{showID}/Ordering/SetPreferred")]
    public ActionResult SetPreferredTmdbShowOrdering(
        [FromRoute] int showID,
        [FromBody] TmdbSetPreferredOrderingBody body
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        if (!string.IsNullOrWhiteSpace(body.AlternateOrderingID) && body.AlternateOrderingID.Length == SeasonIdHexLength)
        {
            var alternateOrdering = TmdbCompatibility.GetAlternateOrdering(body.AlternateOrderingID);
            if (alternateOrdering is null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                return ValidationProblem("Invalid Alternate Ordering ID for show.", nameof(body.AlternateOrderingID));

            _orderingService.SetPreferredOrdering(((IMetadata)show).ID, alternateOrdering.Ordering.ID);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(body.AlternateOrderingID) || (body.AlternateOrderingID != show.Id.ToString() && body.AlternateOrderingID != AlternateOrderingDisabled))
                return ValidationProblem("Invalid Alternate Ordering ID for show.", nameof(body.AlternateOrderingID));

            _orderingService.SetPreferredOrdering(((IMetadata)show).ID, null);
        }

        return Ok();
    }

    [HttpGet("Show/{showID}/CrossReferences")]
    public ActionResult<IReadOnlyList<TmdbShow.CrossReference>> GetCrossReferencesForTmdbShowByShowID(
        [FromRoute] int showID
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return show.CrossReferences
            .Select(xref => new TmdbShow.CrossReference(xref))
            .OrderBy(xref => xref.AnidbAnimeID)
            .ToList();
    }

    [HttpGet("Show/{showID}/Cast")]
    public ActionResult<IReadOnlyList<Role>> GetCastForTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        if (string.Equals(AlternateOrderingDisabled, alternateOrderingID, StringComparison.OrdinalIgnoreCase))
            alternateOrderingID = show.Id.ToString();

        if (string.IsNullOrEmpty(alternateOrderingID) && !string.IsNullOrWhiteSpace(show.PreferredAlternateOrderingID))
            alternateOrderingID = show.PreferredAlternateOrderingID;

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            if (alternateOrderingID.Length == SeasonIdHexLength)
            {
                var alternateOrdering = TmdbCompatibility.GetAlternateOrdering(alternateOrderingID);
                if (alternateOrdering is null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                    return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

                return alternateOrdering.Cast
                    .Select(Role.FromTmdb)
                    .WhereNotNull()
                    .ToList();
            }

            if (alternateOrderingID != show.Id.ToString())
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");
        }

        return show.TmdbCast
            .Select(Role.FromTmdb)
            .WhereNotNull()
            .ToList();
    }

    [HttpGet("Show/{showID}/Crew")]
    public ActionResult<IReadOnlyList<Role>> GetCrewForTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        if (string.Equals(AlternateOrderingDisabled, alternateOrderingID, StringComparison.OrdinalIgnoreCase))
            alternateOrderingID = show.Id.ToString();

        if (string.IsNullOrEmpty(alternateOrderingID) && !string.IsNullOrWhiteSpace(show.PreferredAlternateOrderingID))
            alternateOrderingID = show.PreferredAlternateOrderingID;

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            if (alternateOrderingID.Length == SeasonIdHexLength)
            {
                var alternateOrdering = TmdbCompatibility.GetAlternateOrdering(alternateOrderingID);
                if (alternateOrdering is null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                    return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

                return alternateOrdering.Crew
                    .Select(Role.FromTmdb)
                    .WhereNotNull()
                    .ToList();
            }

            if (alternateOrderingID != show.Id.ToString())
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");
        }

        return show.TmdbCrew
            .Select(Role.FromTmdb)
            .WhereNotNull()
            .ToList();
    }

    [HttpGet("Show/{showID}/Studios")]
    public ActionResult<IReadOnlyList<Studio>> GetStudiosForTmdbShowByShowID(
        [FromRoute] int showID
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return show.TmdbStudios
            .Select(company => new Studio(company))
            .ToList();
    }

    [HttpGet("Show/{showID}/Networks")]
    public ActionResult<IReadOnlyList<Network>> GetNetworksForTmdbShowByShowID(
        [FromRoute] int showID
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return show.TmdbNetworks
            .Select(network => new Network(network))
            .ToList();
    }

    [HttpGet("Show/{showID}/ContentRatings")]
    public ActionResult<IReadOnlyList<ContentRating>> GetContentRatingsForTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return new(show.TmdbContentRatings.ToDto(language));
    }

    [HttpGet("Show/{showID}/Keywords")]
    public ActionResult<IReadOnlyList<string>> GetKeywordsForTmdbShowByShowID(
        [FromRoute] int showID
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return show.Keywords.ToList();
    }

    [HttpGet("Show/{showID}/ProductionCountries")]
    public ActionResult<IReadOnlyDictionary<string, string>> GetProductionCountriesForTmdbShowByShowID(
        [FromRoute] int showID
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return show.TmdbProductionCountries.ToDictionary();
    }

    [HttpGet("Show/{showID}/YearlySeasons")]
    public ActionResult<IReadOnlyList<SeasonWithYear>> GetYearlySeasonsForTmdbShowByShowID(
        [FromRoute] int showID
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return show.YearlySeasons.ToV3Dto();
    }

    [HttpGet("Show/{showID}/DaysOfWeek")]
    public ActionResult<IReadOnlyList<string>> GetDaysOfWeekForTmdbShowByShowID(
        [FromRoute] int showID
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return show.TmdbEpisodes
            .Select(e => e.AiredAt?.DayOfWeek.ToString())
            .WhereNotNullOrDefault()
            .Distinct()
            .Order()
            .ToList();
    }

    #endregion

    #region Same-Source Linked Entries

    [HttpGet("Show/{showID}/Season")]
    public ActionResult<ListResult<TmdbSeason>> GetTmdbSeasonsByTmdbShowID(
        [FromRoute] int showID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbSeason.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null,
        [FromQuery, Range(0, 100)] int pageSize = 25,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        if (string.Equals(AlternateOrderingDisabled, alternateOrderingID, StringComparison.OrdinalIgnoreCase))
            alternateOrderingID = show.Id.ToString();

        if (string.IsNullOrEmpty(alternateOrderingID) && !string.IsNullOrWhiteSpace(show.PreferredAlternateOrderingID))
            alternateOrderingID = show.PreferredAlternateOrderingID;

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            if (alternateOrderingID.Length == SeasonIdHexLength)
            {
                var alternateOrdering = TmdbCompatibility.GetAlternateOrdering(alternateOrderingID);
                if (alternateOrdering is null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                    return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

                return alternateOrdering.Seasons
                    .ToListResult(season => new TmdbSeason(season, include?.CombineFlags()), page, pageSize);
            }

            if (alternateOrderingID != show.Id.ToString())
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");
        }

        return show.TmdbSeasons
            .ToListResult(season => new TmdbSeason(season, include?.CombineFlags(), language), page, pageSize);
    }


    [GeneratedRegex(@"^\s*(?=[SsEe#])(?:(?<isSpecial>[Ss]pecial(?:s|\s*(?<specialNumber>\d+))?)|(?:[Ss](?<seasonNumber>\d+))?((?=[Ee])\s+)?(?:[Ee#](?<episodeNumber>\d+))?)", RegexOptions.Compiled)]
    private static partial Regex SeasonEpisodeRegex();

    /// <summary>
    /// Get the episodes for a TMDB show.
    /// </summary>
    /// <param name="showID">The ID of the show.</param>
    /// <param name="include">The optional details to include in the response.</param>
    /// <param name="language">The optional language to use for the episode titles.</param>
    /// <param name="includeHidden">Whether or not to include hidden episodes.</param>
    /// <param name="alternateOrderingID">The optional ID of an alternate ordering.</param>
    /// <param name="pageSize">The number of entries to return per page.</param>
    /// <param name="page">The page of entries to return.</param>
    /// <param name="search">The optional search string to filter the results by. A leading <c>S1</c>, <c>E2</c>, <c>#2</c> or <c>Special 3</c> narrows to that season or episode, and whatever follows is searched against the titles.</param>
    /// <param name="fuzzy">Whether or not to search fuzzily.</param>
    /// <param name="includeSpecialsInSeasons">
    /// Whether an alternate ordering also lists a special in the regular group holding it, numbering that group's episodes by their place, as
    /// TMDB does. Off, the special is only listed among the specials, and the regular group numbers its episodes without it.
    /// </param>
    /// <returns>The list of episodes.</returns>
    [HttpGet("Show/{showID}/Episode")]
    public ActionResult<ListResult<TmdbEpisode>> GetTmdbEpisodesByTmdbShowID(
        [FromRoute] int showID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbEpisode.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery] IncludeOnlyFilter includeHidden = IncludeOnlyFilter.False,
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] string? search = null,
        [FromQuery] bool fuzzy = false,
        [FromQuery] bool includeSpecialsInSeasons = true
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        if (string.Equals(AlternateOrderingDisabled, alternateOrderingID, StringComparison.OrdinalIgnoreCase))
            alternateOrderingID = show.Id.ToString();

        if (string.IsNullOrEmpty(alternateOrderingID) && !string.IsNullOrWhiteSpace(show.PreferredAlternateOrderingID))
            alternateOrderingID = show.PreferredAlternateOrderingID;

        int? seasonNumber = null;
        int? episodeNumber = null;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var match = SeasonEpisodeRegex().Match(search);
            if (match.Success)
            {
                if (match.Groups["isSpecial"].Success)
                {
                    seasonNumber = 0;
                    if (match.Groups["specialNumber"].Success)
                        episodeNumber = int.Parse(match.Groups["specialNumber"].Value);
                }
                else
                {
                    if (match.Groups["seasonNumber"].Success)
                        seasonNumber = int.Parse(match.Groups["seasonNumber"].Value);
                    if (match.Groups["episodeNumber"].Success)
                        episodeNumber = int.Parse(match.Groups["episodeNumber"].Value);
                }
                search = search[match.Length..].Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            if (alternateOrderingID.Length == SeasonIdHexLength)
            {
                var alternateOrdering = TmdbCompatibility.GetAlternateOrdering(alternateOrderingID);
                if (alternateOrdering is null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                    return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

                var altEpisodes = alternateOrdering.GetEpisodes(includeSpecialsInSeasons)
                    .Select(ordering => (ordering, episode: ordering.TmdbEpisode))
                    .Where(tuple => tuple.episode is not null)
                    .OfType<(TmdbCompatibility.AlternateOrderingEpisode ordering, Metadata_Episode episode)>();
                if (includeHidden is not IncludeOnlyFilter.True)
                {
                    var shouldHideHidden = includeHidden is IncludeOnlyFilter.False;
                    altEpisodes = altEpisodes.Where(t => t.episode.IsHidden != shouldHideHidden);
                }
                if (seasonNumber is not null && episodeNumber is not null)
                    altEpisodes = altEpisodes.Where(t => t.episode.SeasonNumber == seasonNumber && t.episode.EpisodeNumber == episodeNumber);
                else if (seasonNumber is not null)
                    altEpisodes = altEpisodes.Where(t => t.episode.SeasonNumber == seasonNumber);
                else if (episodeNumber is not null)
                    altEpisodes = altEpisodes.Where(t => t.episode.EpisodeNumber == episodeNumber);
                if (!string.IsNullOrWhiteSpace(search))
                    altEpisodes = altEpisodes.Search(search, t => t.episode.GetAllPreferredTitles(t.ordering).Select(t => t.Value), fuzzy).Select(r => r.Result);
                return altEpisodes
                    .ToListResult(
                        t => new TmdbEpisode(show, t.episode, t.ordering, include?.CombineFlags(), language, includeSpecialsInSeasons),
                        page,
                        pageSize
                    );
            }

            if (alternateOrderingID != show.Id.ToString())
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");
        }

        IEnumerable<Metadata_Episode> episodes = show.TmdbEpisodes;
        if (includeHidden is not IncludeOnlyFilter.True)
        {
            var shouldHideHidden = includeHidden is IncludeOnlyFilter.False;
            episodes = episodes.Where(e => e.IsHidden != shouldHideHidden);
        }
        if (seasonNumber is not null && episodeNumber is not null)
            episodes = episodes.Where(e => e.SeasonNumber == seasonNumber && e.EpisodeNumber == episodeNumber);
        else if (seasonNumber is not null)
            episodes = episodes.Where(e => e.SeasonNumber == seasonNumber);
        else if (episodeNumber is not null)
            episodes = episodes.Where(e => e.EpisodeNumber == episodeNumber);
        if (!string.IsNullOrWhiteSpace(search))
            episodes = episodes.Search(search, ep => ep.GetAllPreferredTitles().Select(t => t.Value), fuzzy).Select(r => r.Result);
        return episodes.ToListResult(e => new TmdbEpisode(show, e, include?.CombineFlags(), language, includeSpecialsInSeasons), page, pageSize);
    }

    /// <summary>
    /// Get all episode cross-references for the specified TMDB show.
    /// </summary>
    /// <param name="showID">The TMDB show ID.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <returns>The list of episode cross-references.</returns>
    [HttpGet("Show/{showID}/Episode/CrossReferences")]
    public ActionResult<ListResult<TmdbEpisode.CrossReference>> GetTmdbEpisodeCrossReferencesByTmdbShowID(
        [FromRoute] int showID,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return show.EpisodeCrossReferences
            .ToListResult(x => new TmdbEpisode.CrossReference(x), page, pageSize);
    }

    /// <summary>
    /// Shows all existing episode cross-references for a TMDB Show grouped by
    /// their corresponding cross-reference groups.
    /// </summary>
    /// <param name="showID">The TMDB Show ID.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <returns>The list of grouped episode cross-references.</returns>
    [HttpGet("Show/{showID}/Episode/CrossReferences/EpisodeGroups")]
    public ActionResult<ListResult<List<TmdbEpisode.CrossReference>>> GetGroupedTmdbEpisodeCrossReferencesByTmdbShowID(
        [FromRoute] int showID,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return show.EpisodeCrossReferences
            .GroupByCrossReferenceType()
            .ToListResult(list => list.Select((xref, index) => new TmdbEpisode.CrossReference(xref, index)).ToList(), page, pageSize);
    }

    #endregion

    #region Cross-Source Linked Entries

    /// <summary>
    /// Get all AniDB series linked to a TMDB show.
    /// </summary>
    /// <param name="showID">TMDB Show ID.</param>
    /// <returns></returns>
    [HttpGet("Show/{showID}/AniDB/Anime")]
    public ActionResult<List<AnidbAnime>> GetAnidbAnimeByTmdbShowID(
        [FromRoute] int showID
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return show.CrossReferences
            .Select(xref => xref.AnidbAnime)
            .WhereNotNull()
            .Select(anime => new AnidbAnime(anime))
            .ToList();
    }

    /// <summary>
    /// Get all Shoko series linked to a TMDB show.
    /// </summary>
    /// <param name="showID">TMDB Show ID.</param>
    /// <param name="randomImages">Randomize images shown for the <see cref="Series"/>.</param>
    /// <param name="includeDataFrom">Include data from the selected sources: AniDB, TMDB, or any metadata source a plugin registered, by value, alias or old spelling, whose linked entries are added under <c>Sources</c>.</param>
    /// <returns></returns>
    [HttpGet("Show/{showID}/Shoko/Series")]
    public ActionResult<List<Series>> GetShokoSeriesByTmdbShowID(
        [FromRoute] int showID,
        [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(MetadataSourceSetModelBinder))] HashSet<MetadataSource>? includeDataFrom = null
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return show.CrossReferences
            .Select(xref => xref.AnimeSeries)
            .WhereNotNull()
            .Select(series => new Series(series, User.JMMUserID, randomImages, includeDataFrom))
            .ToList();
    }

    /// <summary>
    /// Get all files linked to a TMDB Show.
    /// </summary>
    /// <param name="showID">TMDB Show ID.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <param name="exclude">Exclude items of certain types</param>
    /// <param name="include_only">Filter to only include items of certain types</param>
    /// <param name="releaseProviders">Filter to only include files from certain release providers. Append <c>!</c> to the provider name to exclude the files</param>
    /// <param name="sortOrder">Sort ordering. Attach '-' at the start to reverse the order of the criteria.</param>
    /// <returns></returns>
    [HttpGet("Show/{showID}/Shoko/File")]
    public ActionResult<ListResult<File>> GetShokoFilesByTmdbShowID(
        [FromRoute] int showID,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[]? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileExcludeTypes[]? exclude = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileIncludeOnlyType[]? include_only = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? releaseProviders = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? sortOrder = null
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is not null && WaitForShowUpdate(show.Id))
            show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        var videoLocals = show.EpisodeCrossReferences
            .Select(xref => xref.AnimeEpisode)
            .WhereNotNull()
            .SelectMany(xref => xref.VideoLocals)
            .DistinctBy(video => video.VideoLocalID);
        return ModelHelper.FilterFiles(videoLocals, User, pageSize, page, include, exclude, include_only, releaseProviders, sortOrder);
    }

    #endregion

    #region Actions

    /// <summary>
    /// Refresh or download the metadata for a TMDB show.
    /// </summary>
    /// <param name="showID">TMDB Show ID.</param>
    /// <param name="body">Body containing options for refreshing or downloading metadata.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("Show/{showID}/Action/Refresh")]
    public async Task<ActionResult> RefreshTmdbShowByShowID(
        [FromRoute] int showID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] TmdbRefreshShowBody body
    )
    {
        // If we want quick results, we're already running an update, and we already have episodes to use, then
        // just return early. This is answered entirely from local state, so it must run before the suspension check
        // below — it never touches TMDB and shouldn't be refused just because TMDB itself is unavailable.
        if (body.Immediate && body.QuickRefresh && _metadataRefreshService.IsRefreshing(ShowEntry(showID)) && TmdbCompatibility.GetShow(showID)?.TmdbEpisodes.Count > 0)
            return Ok();

        // QuickRefresh is only meaningful for a synchronous, immediate caller waiting on the
        // result — a queued/background refresh always does the full job.
        var isQuickRefresh = body.Immediate && body.QuickRefresh;
        return await RefreshTmdbEntry(
            new(MetadataSource.TMDB, MetadataEntityType.Series, showID.ToString()),
            !isQuickRefresh && body.Force,
            new()
            {
                QuickRefresh = isQuickRefresh,
                DownloadImages = body.DownloadImages,
                DownloadAlternateOrdering = body.DownloadAlternateOrdering,
                Reason = MetadataRefreshReason.Requested,
            },
            "Show refresh",
            body.Immediate
        );
    }

    /// <summary>
    /// Download images for a TMDB show.
    /// </summary>
    /// <param name="showID">TMDB Show ID.</param>
    /// <param name="body">Body containing options for downloading images.</param>
    /// <returns>
    /// If <paramref name="body.Immediate"/> is <c>true</c>, returns an <see cref="OkResult"/>,
    /// otherwise returns a <see cref="NoContentResult"/>.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("Show/{showID}/Action/DownloadImages")]
    public async Task<ActionResult> DownloadImagesForTmdbShowByShowID(
        [FromRoute] int showID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] TmdbDownloadImagesBody body
    )
    {
        var show = TmdbCompatibility.GetShow(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return await DownloadTmdbEntryImages(new(MetadataSource.TMDB, MetadataEntityType.Series, showID.ToString()), body.Force, "Show image download", body.Immediate);
    }

    #endregion

    #region Online (Search / Bulk / Single)

    /// <summary>
    /// Search TMDB for shows using the online search.
    /// </summary>
    /// <param name="query">Query to search for.</param>
    /// <param name="includeRestricted">Include restricted shows.</param>
    /// <param name="year">First aired year.</param>
    /// <param name="pageSize">The page size. Set to 0 to only grab the total.</param>
    /// <param name="page">The page index.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("Show/Online/Search")]
    public async Task<ListResult<Search.RemoteSearchShow>> SearchOnlineForTmdbShows(
        [FromQuery] string query,
        [FromQuery] bool includeRestricted = false,
        [FromQuery, Range(0, int.MaxValue)] int year = 0,
        [FromQuery, Range(0, 100)] int pageSize = 6,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var (pageView, totalShows) = await _linkingService.SearchSeries(MetadataSource.TMDB, SearchOptions(query, includeRestricted, year, page, pageSize), HttpContext.RequestAborted);
        return new ListResult<Search.RemoteSearchShow>(totalShows, pageView.Select(a => new Search.RemoteSearchShow(a)));
    }

    /// <summary>
    /// Search for multiple TMDB shows by their IDs.
    /// </summary>
    /// <remarks>
    /// If any of the IDs are not found, a <see cref="ValidationProblemDetails"/> is returned.
    /// </remarks>
    /// <param name="body">Body containing the IDs of the shows to search for.</param>
    /// <returns>
    /// A list of <see cref="Search.RemoteSearchShow"/> containing the search results.
    /// The order of the returned shows is determined by the order of the IDs in <paramref name="body"/>.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("Show/Online/Bulk")]
    public async Task<ActionResult<List<Search.RemoteSearchShow>>> SearchBulkForTmdbShows(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] TmdbBulkSearchBody body
    )
    {
        // We don't care if the inputs are non-unique, but we don't want to double fetch,
        // so we do a distinct here, then at the end we map back to the original order.
        var uniqueIds = body.IDs.Distinct().ToList();
        var showDict = uniqueIds
            .Select(id => id <= 0 ? null : TmdbCompatibility.GetShow(id))
            .WhereNotNull()
            .Select(show => new Search.RemoteSearchShow(show))
            .ToDictionary(show => show.ID);
        foreach (var id in uniqueIds.Except(showDict.Keys))
        {
            if (id <= 0 || await _linkingService.LookupSeries(ShowEntry(id), HttpContext.RequestAborted) is not { } show)
                continue;

            showDict[id] = new Search.RemoteSearchShow(show);
        }

        var unknownShows = uniqueIds.Except(showDict.Keys).ToList();
        if (unknownShows.Count > 0)
        {
            foreach (var id in unknownShows)
                ModelState.AddModelError(nameof(body.IDs), $"Show with id '{id}' not found.");

            return ValidationProblem(ModelState);
        }

        return body.IDs
            .Select(id => showDict[id])
            .ToList();
    }

    /// <summary>
    /// Search TMDB for a show.
    /// </summary>
    /// <param name="showID">TMDB Show ID.</param>
    /// <returns>
    /// If the show is already in the database, returns the local copy.
    /// Otherwise, returns the remote copy from TMDB.
    /// If the show is not found on TMDB, returns 404.
    /// </returns>
    [HttpGet("Show/Online/{showID}")]
    public async Task<ActionResult<Search.RemoteSearchShow>> SearchOnlineForTmdbShowByShowID(
        [FromRoute] int showID
    )
    {
        if (TmdbCompatibility.GetShow(showID) is { } localShow)
            return new Search.RemoteSearchShow(localShow);

        if (showID <= 0 || await _linkingService.LookupSeries(ShowEntry(showID), HttpContext.RequestAborted) is not { } remoteShow)
            return NotFound("Show not found on TMDB.");

        return new Search.RemoteSearchShow(remoteShow);
    }

    #endregion

    #endregion

    #region Seasons

    #region Constants

    internal const int SeasonIdHexLength = 24;

    internal const string SeasonIdRegex = @"^(?:[0-9]{1,23}|[a-f0-9]{24})$";

    internal const string SeasonNotFound = "A TMDB.Season by the given `seasonID` was not found.";

    internal const string SeasonNotFoundByEpisodeID = "A TMDB.Season by the given `episodeID` was not found.";

    #endregion

    #region Basics

    [HttpGet("Season/{seasonID}")]
    public ActionResult<TmdbSeason> GetTmdbSeasonBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbSeason.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is not null && WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return new TmdbSeason(altOrderSeason, include?.CombineFlags());
        }

        var seasonId = int.Parse(seasonID);
        var season = TmdbCompatibility.GetSeason(seasonId);
        if (season is not null && WaitForShowUpdate(season.TmdbShowID))
            season = TmdbCompatibility.GetSeason(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        return new TmdbSeason(season, include?.CombineFlags(), language);
    }

    [HttpGet("Season/{seasonID}/Titles")]
    public ActionResult<IReadOnlyList<Title>> GetTitlesForTmdbSeasonBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is not null && WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            var altPreferredTitle = altOrderSeason.GetPreferredTitle();
            return new(altOrderSeason.GetAllTitles().ToTitleDto(altOrderSeason.EnglishTitle, altPreferredTitle, language));
        }

        var seasonId = int.Parse(seasonID);
        var season = TmdbCompatibility.GetSeason(seasonId);
        if (season is not null && WaitForShowUpdate(season.TmdbShowID))
            season = TmdbCompatibility.GetSeason(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        var preferredTitle = season.GetPreferredTitle();
        return new(season.GetAllTitles().ToTitleDto(season.EnglishTitle, preferredTitle, language));
    }

    [HttpGet("Season/{seasonID}/Overviews")]
    public ActionResult<IReadOnlyList<Overview>> GetOverviewsForTmdbSeasonBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is not null && WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return new List<Overview>();
        }

        var seasonId = int.Parse(seasonID);
        var season = TmdbCompatibility.GetSeason(seasonId);
        if (season is not null && WaitForShowUpdate(season.TmdbShowID))
            season = TmdbCompatibility.GetSeason(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        var preferredOverview = season.GetPreferredOverview();
        return new(season.GetAllOverviews().ToOverviewDto(season.EnglishOverview, preferredOverview, language));
    }

    /// <summary>
    /// Get every image for the TMDB season, grouped by image type.
    /// </summary>
    /// <param name="seasonID">TMDB Season ID</param>
    /// <param name="includeDisabled">Include disabled images.</param>
    /// <param name="includeUndesired">Include images that are not marked as desired for download.</param>
    /// <param name="includeRemoteUrl">Whether to hand out a URL for fetching each image from its source. Defaults to only doing so for images the server does not hold locally.</param>
    /// <param name="language">Filter the images down to these languages.</param>
    /// <returns>Every image for the season, grouped by image type.</returns>
    [HttpGet("Season/{seasonID}/Images")]
    public ActionResult<Images> GetImagesForTmdbSeasonBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID,
        [FromQuery] bool includeDisabled = false,
        [FromQuery] bool includeUndesired = false,
        [FromQuery] RemoteUrlInclusion includeRemoteUrl = RemoteUrlInclusion.WhenUnavailable,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var options = new ImageFilteringOptions { IsEnabled = includeDisabled ? null : true, IsDesired = includeUndesired ? null : true };
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is not null && WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return ((IWithImages)altOrderSeason.Season).GetImages(options)
                .ToDto(language, includeRemoteUrl: includeRemoteUrl, remoteUrlTemplate: _imageManager.GetTemplateUrlForSource)
                .WithCrossReferences(_imageManager.GetCrossReferencesForImageList(altOrderSeason.Season, options));
        }

        var seasonId = int.Parse(seasonID);
        var season = TmdbCompatibility.GetSeason(seasonId);
        if (season is not null && WaitForShowUpdate(season.TmdbShowID))
            season = TmdbCompatibility.GetSeason(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        return ((IWithImages)season).GetImages(options)
            .ToDto(language, includeRemoteUrl: includeRemoteUrl, remoteUrlTemplate: _imageManager.GetTemplateUrlForSource)
            .WithCrossReferences(_imageManager.GetCrossReferencesForImageList(season, options));
    }

    [HttpGet("Season/{seasonID}/Cast")]
    public ActionResult<IReadOnlyList<Role>> GetCastForTmdbSeasonBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is not null && WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return altOrderSeason.Cast
                .Select(Role.FromTmdb)
                .WhereNotNull()
                .ToList();
        }

        var seasonId = int.Parse(seasonID);
        var season = TmdbCompatibility.GetSeason(seasonId);
        if (season is not null && WaitForShowUpdate(season.TmdbShowID))
            season = TmdbCompatibility.GetSeason(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        return season.TmdbCast
            .Select(Role.FromTmdb)
            .WhereNotNull()
            .ToList();
    }

    [HttpGet("Season/{seasonID}/Crew")]
    public ActionResult<IReadOnlyList<Role>> GetCrewForTmdbSeasonBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is not null && WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return altOrderSeason.Crew
                .Select(Role.FromTmdb)
                .WhereNotNull()
                .ToList();
        }

        var seasonId = int.Parse(seasonID);
        var season = TmdbCompatibility.GetSeason(seasonId);
        if (season is not null && WaitForShowUpdate(season.TmdbShowID))
            season = TmdbCompatibility.GetSeason(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        return season.TmdbCrew
            .Select(Role.FromTmdb)
            .WhereNotNull()
            .ToList();
    }

    [HttpGet("Season/{seasonID}/YearlySeasons")]
    public ActionResult<IReadOnlyList<SeasonWithYear>> GetYearlySeasonsForTmdbSeasonBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is not null && WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return altOrderSeason.YearlySeasons.ToV3Dto();
        }

        var seasonId = int.Parse(seasonID);
        var season = TmdbCompatibility.GetSeason(seasonId);
        if (season is not null && WaitForShowUpdate(season.TmdbShowID))
            season = TmdbCompatibility.GetSeason(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        return season.YearlySeasons.ToV3Dto();
    }

    [HttpGet("Season/{seasonID}/DaysOfWeek")]
    public ActionResult<IReadOnlyList<string>> GetDaysOfWeekForTmdbSeasonBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is not null && WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return altOrderSeason.ListedEpisodes
                .Select(e => e.AiredAt?.DayOfWeek.ToString())
                .WhereNotNullOrDefault()
                .Distinct()
                .Order()
                .ToList();
        }

        var seasonId = int.Parse(seasonID);
        var season = TmdbCompatibility.GetSeason(seasonId);
        if (season is not null && WaitForShowUpdate(season.TmdbShowID))
            season = TmdbCompatibility.GetSeason(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        return season.TmdbEpisodes
            .Select(e => e.AiredAt?.DayOfWeek.ToString())
            .WhereNotNullOrDefault()
            .Distinct()
            .Order()
            .ToList();
    }

    #endregion

    #region Same-Source Linked Entries

    [HttpGet("Season/{seasonID}/Show")]
    public ActionResult<TmdbShow> GetTmdbShowBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbShow.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is not null && WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);
            var altOrder = altOrderSeason.TmdbAlternateOrdering;
            var altShow = altOrder?.TmdbShow;
            if (altShow is null)
                return NotFound(ShowNotFoundBySeasonID);

            return new TmdbShow(altShow, altOrder, include?.CombineFlags(), language);
        }

        var seasonId = int.Parse(seasonID);
        var season = TmdbCompatibility.GetSeason(seasonId);
        if (season is not null && WaitForShowUpdate(season.TmdbShowID))
            season = TmdbCompatibility.GetSeason(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        var show = season.TmdbShow;
        if (show is null)
            return NotFound(ShowNotFoundBySeasonID);

        return new TmdbShow(show, include?.CombineFlags(), language);
    }

    [HttpGet("Season/{seasonID}/Episode")]
    public ActionResult<ListResult<TmdbEpisode>> GetTmdbEpisodesBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbEpisode.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery] IncludeOnlyFilter includeHidden = IncludeOnlyFilter.False,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool includeSpecialsInSeasons = true
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is not null && WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            var altShow = altOrderSeason.TmdbShow;
            if (altShow is null)
                return NotFound(ShowNotFoundBySeasonID);

            var altEpisodes = altOrderSeason.GetEpisodes(includeSpecialsInSeasons)
                .Select(ordering => (ordering, episode: ordering.TmdbEpisode))
                .Where(tuple => tuple.episode is not null)
                .OfType<(TmdbCompatibility.AlternateOrderingEpisode ordering, Metadata_Episode episode)>();
            if (includeHidden is not IncludeOnlyFilter.True)
            {
                var shouldHideHidden = includeHidden is IncludeOnlyFilter.False;
                altEpisodes = altEpisodes.Where(t => t.episode.IsHidden != shouldHideHidden);
            }
            return altEpisodes
                .ToListResult(
                    t => new TmdbEpisode(altShow, t.episode, t.ordering, include?.CombineFlags(), language, includeSpecialsInSeasons),
                    page,
                    pageSize
                );
        }

        var seasonId = int.Parse(seasonID);
        var season = TmdbCompatibility.GetSeason(seasonId);
        if (season is not null && WaitForShowUpdate(season.TmdbShowID))
            season = TmdbCompatibility.GetSeason(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        var show = season.TmdbShow;
        if (show is null)
            return NotFound(ShowNotFoundBySeasonID);

        IEnumerable<Metadata_Episode> episodes = season.TmdbEpisodes;
        if (includeHidden is not IncludeOnlyFilter.True)
        {
            var shouldHideHidden = includeHidden is IncludeOnlyFilter.False;
            episodes = episodes.Where(e => e.IsHidden != shouldHideHidden);
        }
        return episodes
            .ToListResult(e => new TmdbEpisode(show, e, include?.CombineFlags(), language, includeSpecialsInSeasons), page, pageSize);
    }

    #endregion

    #region Cross-Source Linked Entries

    [HttpGet("Season/{seasonID}/AniDB/Anime")]
    public ActionResult<List<AnidbAnime>> GetAniDBAnimeBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is not null && WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return altOrderSeason.ListedEpisodes
                .SelectMany(episode => episode.CrossReferences)
                .DistinctBy(xref => xref.AnidbAnimeID)
                .Select(xref => xref.AnidbAnime)
                .WhereNotNull()
                .Select(anime => new AnidbAnime(anime))
                .ToList();
        }

        var seasonId = int.Parse(seasonID);
        var season = TmdbCompatibility.GetSeason(seasonId);
        if (season is not null && WaitForShowUpdate(season.TmdbShowID))
            season = TmdbCompatibility.GetSeason(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        return season.TmdbEpisodes
            .SelectMany(episode => episode.CrossReferences)
            .DistinctBy(xref => xref.AnidbAnimeID)
            .Select(xref => xref.AnidbAnime)
            .WhereNotNull()
            .Select(anime => new AnidbAnime(anime))
            .ToList();
    }

    [HttpGet("Season/{seasonID}/Shoko/Series")]
    public ActionResult<List<Series>> GetShokoSeriesBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID,
        [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(MetadataSourceSetModelBinder))] HashSet<MetadataSource>? includeDataFrom = null
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is not null && WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return altOrderSeason.ListedEpisodes
                .SelectMany(episode => episode.CrossReferences)
                .DistinctBy(xref => xref.AnidbAnimeID)
                .Select(xref => xref.AnimeSeries)
                .WhereNotNull()
                .Select(series => new Series(series, User.JMMUserID, randomImages, includeDataFrom))
                .ToList();
        }

        var seasonId = int.Parse(seasonID);
        var season = TmdbCompatibility.GetSeason(seasonId);
        if (season is not null && WaitForShowUpdate(season.TmdbShowID))
            season = TmdbCompatibility.GetSeason(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        return season.TmdbEpisodes
            .SelectMany(episode => episode.CrossReferences)
            .DistinctBy(xref => xref.AnidbAnimeID)
            .Select(xref => xref.AnimeSeries)
            .WhereNotNull()
            .Select(series => new Series(series, User.JMMUserID, randomImages, includeDataFrom))
            .ToList();
    }

    /// <summary>
    /// Get all files linked to a TMDB Season.
    /// </summary>
    /// <param name="seasonID">TMDB Season ID.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <param name="exclude">Exclude items of certain types</param>
    /// <param name="include_only">Filter to only include items of certain types</param>
    /// <param name="releaseProviders">Filter to only include files from certain release providers. Append <c>!</c> to the provider name to exclude the files</param>
    /// <param name="sortOrder">Sort ordering. Attach '-' at the start to reverse the order of the criteria.</param>
    /// <returns></returns>
    [HttpGet("Season/{seasonID}/Shoko/File")]
    public ActionResult<ListResult<File>> GetShokoFilesBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[]? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileExcludeTypes[]? exclude = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileIncludeOnlyType[]? include_only = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? releaseProviders = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? sortOrder = null
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is not null && WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = TmdbCompatibility.GetAlternateOrderingSeason(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            var videoLocals1 = altOrderSeason.ListedEpisodes
                .SelectMany(episode => episode.CrossReferences)
                .Select(xref => xref.AnimeEpisode)
                .WhereNotNull()
                .SelectMany(xref => xref.VideoLocals)
                .DistinctBy(video => video.VideoLocalID);
            return ModelHelper.FilterFiles(videoLocals1, User, pageSize, page, include, exclude, include_only, releaseProviders, sortOrder);
        }

        var seasonId = int.Parse(seasonID);
        var season = TmdbCompatibility.GetSeason(seasonId);
        if (season is not null && WaitForShowUpdate(season.TmdbShowID))
            season = TmdbCompatibility.GetSeason(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        var videoLocals0 = season.TmdbEpisodes
            .SelectMany(episode => episode.CrossReferences)
            .Select(xref => xref.AnimeEpisode)
            .WhereNotNull()
            .SelectMany(xref => xref.VideoLocals)
            .DistinctBy(video => video.VideoLocalID);
        return ModelHelper.FilterFiles(videoLocals0, User, pageSize, page, include, exclude, include_only, releaseProviders, sortOrder);
    }

    #endregion

    #endregion

    #region Episodes

    #region Constants

    internal const string EpisodeNotFound = "A TMDB.Episode by the given `episodeID` was not found.";

    #endregion

    #region Basics

    [HttpPost("Episode/Bulk")]
    public ActionResult<List<TmdbEpisode>> BulkGetTmdbEpisodesByEpisodeIDs([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] TmdbBulkFetchBody<TmdbEpisode.IncludeDetails> body) =>
        body.IDs
            .Select(episodeID => episodeID <= 0 ? null : TmdbCompatibility.GetEpisode(episodeID))
            .WhereNotNull()
            .GroupBy(episode => episode.TmdbShowID)
            .SelectMany(group =>
            {
                var show = group.First().TmdbShow
                    ?? throw new Exception(ShowNotFoundByEpisodeID);
                if (WaitForShowUpdate(show.Id))
                    show = TmdbCompatibility.GetShow(show.Id)
                        ?? throw new Exception(ShowNotFoundByEpisodeID);

                return group.Select(episode =>
                {
                    var alternateOrderingEpisode = !string.IsNullOrEmpty(show.PreferredAlternateOrderingID)
                        ? TmdbCompatibility.GetAlternateOrderingEpisode(show.PreferredAlternateOrderingID, episode.TmdbEpisodeID)
                        : null;
                    return new TmdbEpisode(show, episode, alternateOrderingEpisode, body.Include?.CombineFlags(), body.Language);
                });
            })
            .ToList();

    [HttpGet("Episode/{episodeID}")]
    public ActionResult<TmdbEpisode> GetTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbEpisode.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null,
        [FromQuery] bool includeSpecialsInSeasons = true
    )
    {
        var episode = TmdbCompatibility.GetEpisode(episodeID);
        if (episode is not null && WaitForShowUpdate(episode.TmdbShowID))
            episode = TmdbCompatibility.GetEpisode(episode.TmdbEpisodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        var show = episode.TmdbShow;
        if (show is null)
            return NotFound(ShowNotFoundByEpisodeID);

        if (string.Equals(AlternateOrderingDisabled, alternateOrderingID, StringComparison.OrdinalIgnoreCase))
            alternateOrderingID = show.Id.ToString();

        if (string.IsNullOrEmpty(alternateOrderingID) && !string.IsNullOrWhiteSpace(show.PreferredAlternateOrderingID))
            alternateOrderingID = show.PreferredAlternateOrderingID;

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            if (alternateOrderingID.Length == SeasonIdHexLength)
            {
                var alternateOrderingEpisode = TmdbCompatibility.GetAlternateOrderingEpisode(alternateOrderingID, episodeID, includeSpecialsInSeasons);
                if (alternateOrderingEpisode is null)
                    return ValidationProblem("Invalid alternateOrderingID for episode.", "alternateOrderingID");

                return new TmdbEpisode(show, episode, alternateOrderingEpisode, include?.CombineFlags(), language, includeSpecialsInSeasons);
            }

            if (alternateOrderingID != episode.TmdbShowID.ToString())
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");
        }

        return new TmdbEpisode(show, episode, include?.CombineFlags(), language, includeSpecialsInSeasons);
    }

    [HttpGet("Episode/{episodeID}/Titles")]
    public ActionResult<IReadOnlyList<Title>> GetTitlesForTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var episode = TmdbCompatibility.GetEpisode(episodeID);
        if (episode is not null && WaitForShowUpdate(episode.TmdbShowID))
            episode = TmdbCompatibility.GetEpisode(episode.TmdbEpisodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        var preferredTitle = episode.GetPreferredTitle();
        return new(episode.GetAllTitles().ToTitleDto(episode.GetEnglishTitle().Value, preferredTitle, language));
    }

    [HttpGet("Episode/{episodeID}/Overviews")]
    public ActionResult<IReadOnlyList<Overview>> GetOverviewsForTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var episode = TmdbCompatibility.GetEpisode(episodeID);
        if (episode is not null && WaitForShowUpdate(episode.TmdbShowID))
            episode = TmdbCompatibility.GetEpisode(episode.TmdbEpisodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        var preferredOverview = episode.GetPreferredOverview();
        return new(episode.GetAllOverviews().ToOverviewDto(episode.EnglishOverview, preferredOverview, language));
    }

    [HttpGet("Episode/{episodeID}/Ordering")]
    public ActionResult<IReadOnlyList<TmdbEpisode.OrderingInformation>> GetOrderingForTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null,
        [FromQuery] bool includeSpecialsInSeasons = true
    )
    {
        var episode = TmdbCompatibility.GetEpisode(episodeID);
        if (episode is not null && WaitForShowUpdate(episode.TmdbShowID))
            episode = TmdbCompatibility.GetEpisode(episode.TmdbEpisodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        var show = episode.TmdbShow;
        if (show is null)
            return NotFound(ShowNotFoundByEpisodeID);

        if (string.Equals(AlternateOrderingDisabled, alternateOrderingID, StringComparison.OrdinalIgnoreCase))
            alternateOrderingID = show.Id.ToString();

        if (string.IsNullOrEmpty(alternateOrderingID) && !string.IsNullOrWhiteSpace(show.PreferredAlternateOrderingID))
            alternateOrderingID = show.PreferredAlternateOrderingID;

        if (!string.IsNullOrWhiteSpace(alternateOrderingID) && alternateOrderingID.Length != SeasonIdHexLength && alternateOrderingID != show.Id.ToString())
            return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

        var alternateOrderingEpisode = (TmdbCompatibility.AlternateOrderingEpisode?)null;
        if (!string.IsNullOrWhiteSpace(alternateOrderingID) && alternateOrderingID != show.Id.ToString())
        {
            alternateOrderingEpisode = TmdbCompatibility.GetAlternateOrderingEpisode(alternateOrderingID, episodeID, includeSpecialsInSeasons);
            if (alternateOrderingEpisode is null || alternateOrderingEpisode.TmdbShowID != show.TmdbShowID)
                return ValidationProblem("Invalid alternateOrderingID for episode.", "alternateOrderingID");
        }

        var ordering = new List<TmdbEpisode.OrderingInformation>
        {
            new(episode, show.PreferredAlternateOrderingID is null, alternateOrderingEpisode),
        };
        foreach (var altOrderEp in episode.GetTmdbAlternateOrderingEpisodes(includeSpecialsInSeasons))
            ordering.Add(new(altOrderEp, show.PreferredAlternateOrderingID, alternateOrderingEpisode));

        return ordering
            .OrderByDescending(o => o.IsDefault)
            .ThenBy(o => o.OrderingType)
            .ThenBy(o => o.OrderingName)
            .ToList();
    }

    /// <summary>
    /// Get every image for the TMDB episode, grouped by image type.
    /// </summary>
    /// <param name="episodeID">TMDB Episode ID</param>
    /// <param name="includeDisabled">Include disabled images.</param>
    /// <param name="includeUndesired">Include images that are not marked as desired for download.</param>
    /// <param name="includeRemoteUrl">Whether to hand out a URL for fetching each image from its source. Defaults to only doing so for images the server does not hold locally.</param>
    /// <param name="language">Filter the images down to these languages.</param>
    /// <returns>Every image for the episode, grouped by image type.</returns>
    [HttpGet("Episode/{episodeID}/Images")]
    public ActionResult<Images> GetImagesForTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery] bool includeDisabled = false,
        [FromQuery] bool includeUndesired = false,
        [FromQuery] RemoteUrlInclusion includeRemoteUrl = RemoteUrlInclusion.WhenUnavailable,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var episode = TmdbCompatibility.GetEpisode(episodeID);
        if (episode is not null && WaitForShowUpdate(episode.TmdbShowID))
            episode = TmdbCompatibility.GetEpisode(episode.TmdbEpisodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        var options = new ImageFilteringOptions { IsEnabled = includeDisabled ? null : true, IsDesired = includeUndesired ? null : true };
        return ((IWithImages)episode).GetImages(options)
            .ToDto(language, includeRemoteUrl: includeRemoteUrl, remoteUrlTemplate: _imageManager.GetTemplateUrlForSource)
            .WithCrossReferences(_imageManager.GetCrossReferencesForImageList(episode, options));
    }

    [HttpGet("Episode/{episodeID}/Cast")]
    public ActionResult<IReadOnlyList<Role>> GetCastForTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID
    )
    {
        var episode = TmdbCompatibility.GetEpisode(episodeID);
        if (episode is not null && WaitForShowUpdate(episode.TmdbShowID))
            episode = TmdbCompatibility.GetEpisode(episode.TmdbEpisodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        return episode.TmdbCast
            .Select(Role.FromTmdb)
            .WhereNotNull()
            .ToList();
    }

    [HttpGet("Episode/{episodeID}/Crew")]
    public ActionResult<IReadOnlyList<Role>> GetCrewForTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID
    )
    {
        var episode = TmdbCompatibility.GetEpisode(episodeID);
        if (episode is not null && WaitForShowUpdate(episode.TmdbShowID))
            episode = TmdbCompatibility.GetEpisode(episode.TmdbEpisodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        return episode.TmdbCrew
            .Select(Role.FromTmdb)
            .WhereNotNull()
            .ToList();
    }

    [HttpGet("Episode/{episodeID}/CrossReferences")]
    public ActionResult<IReadOnlyList<TmdbEpisode.CrossReference>> GetCrossReferencesForTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID
    )
    {
        var episode = TmdbCompatibility.GetEpisode(episodeID);
        if (episode is not null && WaitForShowUpdate(episode.TmdbShowID))
            episode = TmdbCompatibility.GetEpisode(episode.TmdbEpisodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        return episode.CrossReferences
            .Select(xref => new TmdbEpisode.CrossReference(xref))
            .ToList();
    }

    [HttpGet("Episode/{episodeID}/FileCrossReferences")]
    public ActionResult<IReadOnlyList<FileCrossReference>> GetFileCrossReferencesForTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID
    )
    {
        var episode = TmdbCompatibility.GetEpisode(episodeID);
        if (episode is not null && WaitForShowUpdate(episode.TmdbShowID))
            episode = TmdbCompatibility.GetEpisode(episode.TmdbEpisodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        return FileCrossReference.From(episode.FileCrossReferences);
    }

    #endregion

    #region Actions

    /// <summary>
    /// Hide or show a TMDB episode.
    /// </summary>
    /// <param name="episodeID">TMDB Episode ID.</param>
    /// <param name="body">Whether to hide the episode. Hides it when left out.</param>
    /// <returns>Nothing.</returns>
    [Authorize("admin")]
    [HttpPost("Episode/{episodeID}/Action/SetHiddenState")]
    public ActionResult SetHiddenStateForTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] TmdbEpisode.SetHiddenStateForTmdbEpisodeByEpisodeIDRequestBody? body
    )
    {
        var episode = TmdbCompatibility.GetEpisode(episodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        _orderingService.SetEpisodeHidden(((IMetadata)episode).ID, body?.Value ?? true);

        return Ok();
    }

    #endregion

    #region Same-Source Linked Entries

    [HttpGet("Episode/{episodeID}/Show")]
    public ActionResult<TmdbShow> GetTmdbShowByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbShow.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null
    )
    {
        var episode = TmdbCompatibility.GetEpisode(episodeID);
        if (episode is not null && WaitForShowUpdate(episode.TmdbShowID))
            episode = TmdbCompatibility.GetEpisode(episode.TmdbEpisodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        var show = episode.TmdbShow;
        if (show is null)
            return NotFound(ShowNotFoundByEpisodeID);

        if (string.Equals(AlternateOrderingDisabled, alternateOrderingID, StringComparison.OrdinalIgnoreCase))
            alternateOrderingID = show.Id.ToString();

        if (string.IsNullOrEmpty(alternateOrderingID) && !string.IsNullOrWhiteSpace(show.PreferredAlternateOrderingID))
            alternateOrderingID = show.PreferredAlternateOrderingID;

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            if (alternateOrderingID.Length == SeasonIdHexLength)
            {
                var alternateOrdering = TmdbCompatibility.GetAlternateOrdering(alternateOrderingID);
                if (alternateOrdering is null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                    return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

                return new TmdbShow(show, alternateOrdering, include?.CombineFlags(), language);
            }

            if (alternateOrderingID != episode.TmdbShowID.ToString())
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");
        }

        return new TmdbShow(show, include?.CombineFlags(), language);
    }

    [HttpGet("Episode/{episodeID}/Season")]
    public ActionResult<TmdbSeason> GetTmdbSeasonByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbSeason.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null
    )
    {
        var episode = TmdbCompatibility.GetEpisode(episodeID);
        if (episode is not null && WaitForShowUpdate(episode.TmdbShowID))
            episode = TmdbCompatibility.GetEpisode(episode.TmdbEpisodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        var show = episode.TmdbShow;
        if (show is null)
            return NotFound(ShowNotFoundByEpisodeID);

        if (string.Equals(AlternateOrderingDisabled, alternateOrderingID, StringComparison.OrdinalIgnoreCase))
            alternateOrderingID = show.Id.ToString();

        if (string.IsNullOrEmpty(alternateOrderingID) && !string.IsNullOrWhiteSpace(show.PreferredAlternateOrderingID))
            alternateOrderingID = show.PreferredAlternateOrderingID;

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            if (alternateOrderingID.Length == SeasonIdHexLength)
            {
                var alternateOrderingEpisode = TmdbCompatibility.GetAlternateOrderingEpisode(alternateOrderingID, episodeID);
                var altOrderSeason = alternateOrderingEpisode?.TmdbAlternateOrderingSeason;
                if (altOrderSeason is null)
                    return NotFound(SeasonNotFoundByEpisodeID);

                return new TmdbSeason(altOrderSeason, include?.CombineFlags());
            }

            if (alternateOrderingID != episode.TmdbShowID.ToString())
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");
        }

        var season = episode.TmdbSeason;
        if (season is null)
            return NotFound(SeasonNotFoundByEpisodeID);

        return new TmdbSeason(season, include?.CombineFlags(), language);
    }

    #endregion

    #region Cross-Source Linked Entries

    [HttpGet("Episode/{episodeID}/AniDB/Anime")]
    public ActionResult<List<AnidbAnime>> GetAniDBAnimeByEpisodeID(
        [FromRoute] int episodeID
    )
    {
        var episode = TmdbCompatibility.GetEpisode(episodeID);
        if (episode is not null && WaitForShowUpdate(episode.TmdbShowID))
            episode = TmdbCompatibility.GetEpisode(episode.TmdbEpisodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        return episode.CrossReferences
            .DistinctBy(xref => xref.AnidbAnimeID)
            .Select(xref => xref.AnidbAnime)
            .WhereNotNull()
            .Select(anime => new AnidbAnime(anime))
            .ToList();
    }

    [HttpGet("Episode/{episodeID}/Anidb/Episode")]
    public ActionResult<List<AnidbEpisode>> GetAniDBEpisodeByEpisodeID(
        [FromRoute] int episodeID
    )
    {
        var episode = TmdbCompatibility.GetEpisode(episodeID);
        if (episode is not null && WaitForShowUpdate(episode.TmdbShowID))
            episode = TmdbCompatibility.GetEpisode(episode.TmdbEpisodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        return episode.CrossReferences
            .DistinctBy(xref => xref.AnidbAnimeID)
            .Select(xref => xref.AnidbEpisode)
            .WhereNotNull()
            .Select(anidbEpisode => new AnidbEpisode(anidbEpisode))
            .ToList();
    }

    [HttpGet("Episode/{episodeID}/Shoko/Series")]
    public ActionResult<List<Series>> GetShokoSeriesByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(MetadataSourceSetModelBinder))] HashSet<MetadataSource>? includeDataFrom = null
    )
    {
        var episode = TmdbCompatibility.GetEpisode(episodeID);
        if (episode is not null && WaitForShowUpdate(episode.TmdbShowID))
            episode = TmdbCompatibility.GetEpisode(episode.TmdbEpisodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        return episode.CrossReferences
            .DistinctBy(xref => xref.AnidbAnimeID)
            .Select(xref => xref.AnimeSeries)
            .WhereNotNull()
            .Select(shokoSeries => new Series(shokoSeries, User.JMMUserID, randomImages, includeDataFrom))
            .ToList();
    }

    [HttpGet("Episode/{episodeID}/Shoko/Episode")]
    public ActionResult<List<Episode>> GetShokoEpisodesByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery, ModelBinder(typeof(MetadataSourceSetModelBinder))] HashSet<MetadataSource>? includeDataFrom = null
    )
    {
        var episode = TmdbCompatibility.GetEpisode(episodeID);
        if (episode is not null && WaitForShowUpdate(episode.TmdbShowID))
            episode = TmdbCompatibility.GetEpisode(episode.TmdbEpisodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        return episode.CrossReferences
            .DistinctBy(xref => xref.AnidbEpisodeID)
            .Select(xref => xref.AnimeEpisode)
            .WhereNotNull()
            .Select(shokoEpisode => new Episode(HttpContext, shokoEpisode, includeDataFrom))
            .ToList();
    }

    /// <summary>
    /// Get all files linked to a TMDB Episode.
    /// </summary>
    /// <param name="episodeID">TMDB Episode ID.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <param name="exclude">Exclude items of certain types</param>
    /// <param name="include_only">Filter to only include items of certain types</param>
    /// <param name="releaseProviders">Filter to only include files from certain release providers. Append <c>!</c> to the provider name to exclude the files</param>
    /// <param name="sortOrder">Sort ordering. Attach '-' at the start to reverse the order of the criteria.</param>
    /// <returns></returns>
    [HttpGet("Episode/{episodeID}/Shoko/File")]
    public ActionResult<ListResult<File>> GetShokoFilesByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[]? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileExcludeTypes[]? exclude = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileIncludeOnlyType[]? include_only = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? releaseProviders = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? sortOrder = null
    )
    {
        var episode = TmdbCompatibility.GetEpisode(episodeID);
        if (episode is not null && WaitForShowUpdate(episode.TmdbShowID))
            episode = TmdbCompatibility.GetEpisode(episode.TmdbEpisodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        var videoLocals = episode.CrossReferences
            .Select(xref => xref.AnimeEpisode)
            .WhereNotNull()
            .SelectMany(xref => xref.VideoLocals)
            .DistinctBy(video => video.VideoLocalID);
        return ModelHelper.FilterFiles(videoLocals, User, pageSize, page, include, exclude, include_only, releaseProviders, sortOrder);
    }

    #endregion

    #endregion

    #region Export / Import

    [Flags]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum CrossReferenceExportType
    {
        None = 0,
        Movie = 1,
        Show = 2,
        Episode = 4,
    }

    /// <summary>
    /// Export all or selected AniDB/TMDB cross-references in the specified sections.
    /// </summary>
    /// <param name="body">Optional. Export options.</param>
    /// <returns>The cross-reference file.</returns>
    [Authorize("admin")]
    [HttpPost("Export")]
    public ActionResult ExportCrossReferences(
        [FromBody] TmdbExportBody? body
    )
    {
        body ??= new();
        var text = _crossReferenceTransferService.Export(MetadataSource.TMDB, body.ToOptions());
        var bytes = Encoding.UTF8.GetBytes(text);
        return File(bytes, "text/csv", "anidb_tmdb_xrefs.csv");
    }

    /// <summary>
    /// Import a cross-reference CSV file in the same format we export.
    /// </summary>
    /// <remarks>
    /// This will take care of creating/updating all cross-reference entries
    /// for everything we can export, be it movie cross-references, episode
    /// cross-references, or anything we might add in the future. If we can
    /// export it then we can import it!
    /// </remarks>
    /// <param name="file">The CSV file to import.</param>
    /// <param name="removeExisting">Remove existing cross-references for the same AniDB episodes.</param>
    /// <param name="addMissingMovies">Add missing movies.</param>
    /// <param name="addMissingShows">Add missing shows.</param>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpPost("Import")]
    public async Task<ActionResult> ImportMovieCrossReferences(
        IFormFile file,
        [FromQuery] bool removeExisting = true,
        [FromQuery] bool addMissingMovies = true,
        [FromQuery] bool addMissingShows = true
    )
    {
        if (file is null || file.Length == 0)
            ModelState.AddModelError("Body", "Body cannot be empty.");

        if (file is not null && file.Name != "file")
            ModelState.AddModelError("Body", "Invalid field name for import file");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        using var stream = new StreamReader(file!.OpenReadStream(), Encoding.UTF8, true);
        var result = await _crossReferenceTransferService.Import(
            MetadataSource.TMDB,
            stream,
            new()
            {
                RemoveExisting = removeExisting,
                AddMissingMovies = addMissingMovies,
                AddMissingSeries = addMissingShows,
            },
            HttpContext.RequestAborted
        );
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("Body", error.Message);
            return ValidationProblem(ModelState);
        }

        return NoContent();
    }

    #endregion
}
