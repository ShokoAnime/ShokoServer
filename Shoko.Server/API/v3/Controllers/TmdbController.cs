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
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.AniDB;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.API.v3.Models.TMDB;
using Shoko.Server.API.v3.Models.TMDB.Input;
using Shoko.Server.Extensions;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using AbstractAnimeType = Shoko.Plugin.Abstractions.DataModels.AnimeType;
using CrossRefSource = Shoko.Models.Enums.CrossRefSource;
using DataSource = Shoko.Server.API.v3.Models.Common.DataSource;
using File = Shoko.Server.API.v3.Models.Shoko.File;
using InternalEpisodeType = Shoko.Models.Enums.EpisodeType;
using MatchRating = Shoko.Models.Enums.MatchRating;

#pragma warning disable CA1822
#nullable enable
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public partial class TmdbController : BaseController
{
    private readonly ILogger<TmdbController> _logger;

    private readonly TmdbSearchService _tmdbSearchService;

    private readonly TmdbMetadataService _tmdbMetadataService;

    public TmdbController(ISettingsProvider settingsProvider, ILogger<TmdbController> logger, TmdbSearchService tmdbSearchService, TmdbMetadataService tmdbService) : base(settingsProvider)
    {
        _logger = logger;
        _tmdbSearchService = tmdbSearchService;
        _tmdbMetadataService = tmdbService;
    }

    #region Movies

    #region Constants

    internal const string MovieNotFound = "A TMDB.Movie by the given `movieID` was not found.";

    #endregion

    #region Basics

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
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        var movies = RepoFactory.TMDB_Movie.GetAll()
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
        if (hasSearch)
        {
            var languages = SettingsProvider.GetSettings()
                .Language.DescriptionLanguageOrder
                .Select(lang => lang.GetTitleLanguage())
                .Concat([TitleLanguage.English])
                .ToHashSet();
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
                    if (_tmdbMetadataService.WaitForMovieUpdate(movie.Id))
                        movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movie.Id) ?? movie;
                    return new TmdbMovie(movie, include?.CombineFlags());
                }, page, pageSize);
        }

        return movies
            .OrderBy(movie => movie.EnglishTitle)
            .ThenBy(movie => movie.TmdbMovieID)
            .ToListResult(movie =>
            {
                if (_tmdbMetadataService.WaitForMovieUpdate(movie.Id))
                    movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movie.Id) ?? movie;
                return new TmdbMovie(movie, include?.CombineFlags());
            }, page, pageSize);
    }

    [HttpPost("Movie/Bulk")]
    public ActionResult<List<TmdbMovie>> BulkGetTmdbMoviesByMovieIDs([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] TmdbBulkFetchBody<TmdbMovie.IncludeDetails> body) =>
        body.IDs
            .Select(movieID => movieID <= 0 ? null : RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID))
            .WhereNotNull()
            .Select(movie =>
            {
                if (_tmdbMetadataService.WaitForMovieUpdate(movie.Id))
                    movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movie.Id) ?? movie;
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
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is not null && _tmdbMetadataService.WaitForMovieUpdate(movieID))
            movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
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
        await _tmdbMetadataService.SchedulePurgeOfMovie(movieID);

        return NoContent();
    }

    [HttpGet("Movie/{movieID}/Titles")]
    public ActionResult<IReadOnlyList<Title>> GetTitlesForTmdbMovieByMovieID(
        [FromRoute] int movieID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is not null && _tmdbMetadataService.WaitForMovieUpdate(movieID))
            movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        var preferredTitle = movie.GetPreferredTitle();
        return new(movie.GetAllTitles().ToDto(movie.EnglishTitle, preferredTitle, language));
    }

    [HttpGet("Movie/{movieID}/Overviews")]
    public ActionResult<IReadOnlyList<Overview>> GetOverviewsForTmdbMovieByMovieID(
        [FromRoute] int movieID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is not null && _tmdbMetadataService.WaitForMovieUpdate(movieID))
            movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        var preferredOverview = movie.GetPreferredOverview();
        return new(movie.GetAllOverviews().ToDto(movie.EnglishTitle, preferredOverview, language));
    }

    [HttpGet("Movie/{movieID}/Images")]
    public ActionResult<Images> GetImagesForTmdbMovieByMovieID(
        [FromRoute] int movieID,
        [FromQuery] bool includeDisabled = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is not null && _tmdbMetadataService.WaitForMovieUpdate(movieID))
            movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.GetImages()
            .ToDto(language, includeDisabled: includeDisabled, preferredPoster: movie.DefaultPoster, preferredBackdrop: movie.DefaultBackdrop);
    }

    [HttpGet("Movie/{movieID}/Cast")]
    public ActionResult<IReadOnlyList<Role>> GetCastForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is not null && _tmdbMetadataService.WaitForMovieUpdate(movieID))
            movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.Cast
            .Select(cast => new Role(cast))
            .ToList();
    }

    [HttpGet("Movie/{movieID}/Crew")]
    public ActionResult<IReadOnlyList<Role>> GetCrewForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is not null && _tmdbMetadataService.WaitForMovieUpdate(movieID))
            movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.Crew
            .Select(cast => new Role(cast))
            .ToList();
    }

    [HttpGet("Movie/{movieID}/CrossReferences")]
    public ActionResult<IReadOnlyList<TmdbMovie.CrossReference>> GetCrossReferencesForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is not null && _tmdbMetadataService.WaitForMovieUpdate(movieID))
            movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
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
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return FileCrossReference.From(movie.FileCrossReferences);
    }

    [HttpGet("Movie/{movieID}/Studios")]
    public ActionResult<IReadOnlyList<Studio>> GetStudiosForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is not null && _tmdbMetadataService.WaitForMovieUpdate(movieID))
            movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.TmdbCompanies
            .Select(company => new Studio(company))
            .ToList();
    }

    [HttpGet("Movie/{movieID}/ContentRatings")]
    public ActionResult<IReadOnlyList<ContentRating>> GetContentRatingsForTmdbMovieByMovieID(
        [FromRoute] int movieID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is not null && _tmdbMetadataService.WaitForMovieUpdate(movieID))
            movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return new(movie.ContentRatings.ToDto(language));
    }

    [HttpGet("Movie/{movieID}/Keywords")]
    public ActionResult<IReadOnlyList<string>> GetKeywordsForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is not null && _tmdbMetadataService.WaitForMovieUpdate(movieID))
            movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.Keywords;
    }

    [HttpGet("Movie/{movieID}/ProductionCountries")]
    public ActionResult<IReadOnlyDictionary<string, string>> GetProductionCountriesForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is not null && _tmdbMetadataService.WaitForMovieUpdate(movieID))
            movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.ProductionCountries
            .ToDictionary(country => country.CountryCode, country => country.CountryName);
    }

    [HttpGet("Movie/{movieID}/YearlySeasons")]
    public ActionResult<IReadOnlyList<YearlySeason>> GetYearlySeasonsForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is not null && _tmdbMetadataService.WaitForMovieUpdate(movieID))
            movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.Seasons.ToV3Dto();
    }

    #endregion

    #region Same-Source Linked Entries

    [HttpGet("Movie/{movieID}/Collection")]
    public ActionResult<TmdbMovie.Collection> GetTmdbMovieCollectionByMovieID(
        [FromRoute] int movieID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbMovie.Collection.IncludeDetails>? include = null
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is not null && _tmdbMetadataService.WaitForMovieUpdate(movieID))
            movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        var movieCollection = movie.TmdbCollection;
        if (movieCollection is null)
            return NotFound(MovieCollectionByMovieIDNotFound);

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
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
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
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
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
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("Movie/{movieID}/Shoko/Series")]
    public ActionResult<List<Series>> GetShokoSeriesByTmdbMovieID(
        [FromRoute] int movieID,
        [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
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
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("Movie/{movieID}/Shoko/Episode")]
    public ActionResult<List<Episode>> GetShokoEpisodesByTmdbMovieID(
        [FromRoute] int movieID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
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
    /// <param name="sortOrder">Sort ordering. Attach '-' at the start to reverse the order of the criteria.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("Movie/{movieID}/Shoko/File")]
    public ActionResult<ListResult<File>> GetShokoFilesByMovieID(
        [FromRoute] int movieID,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[]? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileExcludeTypes[]? exclude = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileIncludeOnlyType[]? include_only = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? sortOrder = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        var videoLocals = movie.CrossReferences
            .Select(xref => xref.AnimeEpisode)
            .WhereNotNull()
            .SelectMany(xref => xref.VideoLocals)
            .DistinctBy(video => video.VideoLocalID);
        return ModelHelper.FilterFiles(videoLocals, User, pageSize, page, include, exclude, include_only, sortOrder, includeDataFrom);
    }

    #endregion

    #region Actions

    /// <summary>
    /// Refresh or download the metadata for a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <param name="body">Body containing options for refreshing or downloading metadata.</param>
    /// <returns>
    /// If <paramref name="body.Immediate"/> is <see langword="true"/>, returns an <see cref="OkResult"/>,
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
            var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
            if (movie is not null)
                return Ok();
        }

        if (body.Immediate)
        {
            await _tmdbMetadataService.UpdateMovie(movieID, body.Force, body.DownloadImages, body.DownloadCrewAndCast ?? SettingsProvider.GetSettings().TMDB.AutoDownloadCrewAndCast, body.DownloadCollections ?? SettingsProvider.GetSettings().TMDB.AutoDownloadCollections);
            return Ok();
        }

        await _tmdbMetadataService.ScheduleUpdateOfMovie(movieID, body.Force, body.DownloadImages, body.DownloadCrewAndCast, body.DownloadCollections);
        return NoContent();
    }

    /// <summary>
    /// Download images for a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <param name="body">Body containing options for downloading images.</param>
    /// <returns>
    /// If <paramref name="body.Immediate"/> is <see langword="true"/>, returns an <see cref="OkResult"/>,
    /// otherwise returns a <see cref="NoContentResult"/>.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("Movie/{movieID}/Action/DownloadImages")]
    public async Task<ActionResult> DownloadImagesForTmdbMovieByMovieID(
        [FromRoute] int movieID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] TmdbDownloadImagesBody body
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        if (body.Immediate)
        {
            await _tmdbMetadataService.DownloadAllMovieImages(movieID, body.Force);
            return Ok();
        }

        await _tmdbMetadataService.ScheduleDownloadAllMovieImages(movieID, body.Force);
        return NoContent();
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
    public ListResult<Search.RemoteSearchMovie> SearchOnlineForTmdbMovies(
        [FromQuery] string query,
        [FromQuery] bool includeRestricted = false,
        [FromQuery, Range(0, int.MaxValue)] int year = 0,
        [FromQuery, Range(0, 100)] int pageSize = 6,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var (pageView, totalMovies) = _tmdbSearchService.SearchMovies(query, includeRestricted, year, page, pageSize)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

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
    [HttpPost("Movie/Online/Bulk")]
    public async Task<ActionResult<List<Search.RemoteSearchMovie>>> SearchBulkForTmdbMovies(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] TmdbBulkSearchBody body
    )
    {
        // We don't care if the inputs are non-unique, but we don't want to double fetch,
        // so we do a distinct here, then at the end we map back to the original order.
        var uniqueIds = body.IDs.Distinct().ToList();
        var movieDict = uniqueIds
            .Select(id => id <= 0 ? null : RepoFactory.TMDB_Movie.GetByTmdbMovieID(id))
            .WhereNotNull()
            .Select(movie => new Search.RemoteSearchMovie(movie))
            .ToDictionary(movie => movie.ID);
        foreach (var id in uniqueIds.Except(movieDict.Keys))
        {
            var movie = id <= 0 ? null : await _tmdbMetadataService.UseClient(c => c.GetMovieAsync(id), $"Get movie {id}");
            if (movie is null)
                continue;

            movieDict[movie.Id] = new Search.RemoteSearchMovie(movie);
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
        if (RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID) is { } localMovie)
            return new Search.RemoteSearchMovie(localMovie);

        if (await _tmdbMetadataService.UseClient(c => c.GetMovieAsync(movieID), $"Get movie {movieID}") is not { } remoteMovie)
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
            var languages = SettingsProvider.GetSettings()
                .Language.DescriptionLanguageOrder
                .Select(lang => lang.GetTitleLanguage())
                .Concat([TitleLanguage.English])
                .ToHashSet();
            return RepoFactory.TMDB_Collection.GetAll()
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
                if (_tmdbMetadataService.WaitForMovieCollectionUpdate(movieCollection.Id))
                    movieCollection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(movieCollection.Id) ?? movieCollection;
                return new TmdbMovie.Collection(movieCollection, include?.CombineFlags(), language);
            }, page, pageSize);
        }

        return RepoFactory.TMDB_Collection.GetAll()
            .ToListResult(movieCollection =>
            {
                if (_tmdbMetadataService.WaitForMovieCollectionUpdate(movieCollection.Id))
                    movieCollection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(movieCollection.Id) ?? movieCollection;
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
        var collection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(collectionID);
        if (collection is not null && _tmdbMetadataService.WaitForMovieCollectionUpdate(collection.Id))
            collection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(collection.Id);
        if (collection is null)
            return NotFound(MovieCollectionNotFound);

        return new TmdbMovie.Collection(collection, include?.CombineFlags(), language);
    }

    [HttpGet("Movie/Collection/{collectionID}/Titles")]
    public ActionResult<IReadOnlyList<Title>> GetTitlesForMovieCollectionByCollectionID(
        [FromRoute] int collectionID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var collection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(collectionID);
        if (collection is not null && _tmdbMetadataService.WaitForMovieCollectionUpdate(collection.Id))
            collection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(collection.Id);
        if (collection is null)
            return NotFound(MovieCollectionNotFound);

        var preferredTitle = collection.GetPreferredTitle();
        return new(collection.GetAllTitles().ToDto(collection.EnglishTitle, preferredTitle, language));
    }

    [HttpGet("Movie/Collection/{collectionID}/Overviews")]
    public ActionResult<IReadOnlyList<Overview>> GetOverviewsForMovieCollectionByCollectionID(
        [FromRoute] int collectionID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var collection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(collectionID);
        if (collection is not null && _tmdbMetadataService.WaitForMovieCollectionUpdate(collection.Id))
            collection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(collection.Id);
        if (collection is null)
            return NotFound(MovieCollectionNotFound);

        var preferredOverview = collection.GetPreferredOverview();
        return new(collection.GetAllOverviews().ToDto(collection.EnglishTitle, preferredOverview, language));
    }

    [HttpGet("Movie/Collection/{collectionID}/Images")]
    public ActionResult<Images> GetImagesForMovieCollectionByCollectionID(
        [FromRoute] int collectionID,
        [FromQuery] bool includeDisabled = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var collection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(collectionID);
        if (collection is not null && _tmdbMetadataService.WaitForMovieCollectionUpdate(collection.Id))
            collection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(collection.Id);
        if (collection is null)
            return NotFound(MovieCollectionNotFound);

        return collection.GetImages()
            .ToDto(language, includeDisabled: includeDisabled);
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
        var collection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(collectionID);
        if (collection is not null && _tmdbMetadataService.WaitForMovieCollectionUpdate(collection.Id))
            collection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(collection.Id);
        if (collection is null)
            return NotFound(MovieCollectionNotFound);

        return collection.GetTmdbMovies()
            .Select(movie =>
            {
                if (_tmdbMetadataService.WaitForMovieUpdate(movie.Id))
                    movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movie.Id) ?? movie;
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
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        var shows = RepoFactory.TMDB_Show.GetAll()
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
        if (hasSearch)
        {
            var languages = SettingsProvider.GetSettings()
                .Language.DescriptionLanguageOrder
                .Select(lang => lang.GetTitleLanguage())
                .Concat([TitleLanguage.English])
                .ToHashSet();
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
                    if (_tmdbMetadataService.WaitForShowUpdate(show.Id))
                        show = RepoFactory.TMDB_Show.GetByTmdbShowID(show.Id) ?? show;

                    var alternateOrdering = (TMDB_AlternateOrdering?)null;
                    if (!string.IsNullOrWhiteSpace(show.PreferredAlternateOrderingID))
                        alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(show.PreferredAlternateOrderingID);

                    return new TmdbShow(show, alternateOrdering, include?.CombineFlags(), language);
                }, page, pageSize);
        }

        return shows
            .OrderBy(show => show.EnglishTitle)
            .ThenBy(show => show.TmdbShowID)
            .ToListResult(show =>
            {
                if (_tmdbMetadataService.WaitForShowUpdate(show.Id))
                    show = RepoFactory.TMDB_Show.GetByTmdbShowID(show.Id) ?? show;

                var alternateOrdering = (TMDB_AlternateOrdering?)null;
                if (!string.IsNullOrWhiteSpace(show.PreferredAlternateOrderingID))
                    alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(show.PreferredAlternateOrderingID);

                return new TmdbShow(show, alternateOrdering, include?.CombineFlags(), language);
            }, page, pageSize);
    }

    [HttpPost("Show/Bulk")]
    public ActionResult<List<TmdbShow>> BulkGetTmdbShowsByShowIDs([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] TmdbBulkFetchBody<TmdbShow.IncludeDetails> body) =>
        body.IDs
            .Select(showID => showID <= 0 ? null : RepoFactory.TMDB_Show.GetByTmdbShowID(showID))
            .WhereNotNull()
            .Select(show =>
            {
                if (_tmdbMetadataService.WaitForShowUpdate(show.Id))
                    show = RepoFactory.TMDB_Show.GetByTmdbShowID(show.Id) ?? show;

                var alternateOrdering = (TMDB_AlternateOrdering?)null;
                if (!string.IsNullOrWhiteSpace(show.PreferredAlternateOrderingID))
                    alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(show.PreferredAlternateOrderingID);

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
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(show.Id))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
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
                var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
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
        await _tmdbMetadataService.SchedulePurgeOfShow(showID);

        return NoContent();
    }

    [HttpGet("Show/{showID}/Titles")]
    public ActionResult<IReadOnlyList<Title>> GetTitlesForTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(show.Id))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        var preferredTitle = show.GetPreferredTitle();
        return new(show.GetAllTitles().ToDto(show.EnglishTitle, preferredTitle, language));
    }

    [HttpGet("Show/{showID}/Overviews")]
    public ActionResult<IReadOnlyList<Overview>> GetOverviewsForTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(show.Id))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        var preferredOverview = show.GetPreferredOverview();
        return new(show.GetAllOverviews().ToDto(show.EnglishOverview, preferredOverview, language));
    }

    [HttpGet("Show/{showID}/Images")]
    public ActionResult<Images> GetImagesForTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery] bool includeDisabled = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(show.Id))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return show.GetImages()
            .ToDto(language, includeDisabled: includeDisabled, preferredPoster: show.DefaultPoster, preferredBackdrop: show.DefaultBackdrop);
    }

    [HttpGet("Show/{showID}/Ordering")]
    public ActionResult<IReadOnlyList<TmdbShow.OrderingInformation>> GetOrderingForTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(show.Id))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        if (string.Equals(AlternateOrderingDisabled, alternateOrderingID, StringComparison.OrdinalIgnoreCase))
            alternateOrderingID = show.Id.ToString();

        if (string.IsNullOrEmpty(alternateOrderingID) && !string.IsNullOrWhiteSpace(show.PreferredAlternateOrderingID))
            alternateOrderingID = show.PreferredAlternateOrderingID;

        if (!string.IsNullOrWhiteSpace(alternateOrderingID) && alternateOrderingID.Length != SeasonIdHexLength && alternateOrderingID != show.Id.ToString())
            return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

        var alternateOrdering = (TMDB_AlternateOrdering?)null;
        if (!string.IsNullOrWhiteSpace(alternateOrderingID) && alternateOrderingID != show.Id.ToString())
        {
            alternateOrdering = !string.IsNullOrWhiteSpace(alternateOrderingID) ? RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID) : null;
            if (alternateOrdering is null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");
        }

        var ordering = new List<TmdbShow.OrderingInformation>
        {
            new(show, alternateOrdering),
        };
        foreach (var altOrder in show.TmdbAlternateOrdering)
            ordering.Add(new(show, altOrder, alternateOrdering));
        return ordering
            .OrderByDescending(o => o.IsDefault)
            .ThenBy(o => o.OrderingType)
            .ThenBy(o => o.OrderingName)
            .ToList();
    }

    [HttpPost("Show/{showID}/Ordering/SetPreferred")]
    public ActionResult SetPreferredTmdbShowOrdering(
        [FromRoute] int showID,
        [FromBody] TmdbSetPreferredOrderingBody body
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(show.Id))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        if (!string.IsNullOrWhiteSpace(body.AlternateOrderingID) && body.AlternateOrderingID.Length == SeasonIdHexLength)
        {
            var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(body.AlternateOrderingID);
            if (alternateOrdering is null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                return ValidationProblem("Invalid Alternate Ordering ID for show.", nameof(body.AlternateOrderingID));

            show.PreferredAlternateOrderingID = body.AlternateOrderingID;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(body.AlternateOrderingID) || (body.AlternateOrderingID != show.Id.ToString() && body.AlternateOrderingID != AlternateOrderingDisabled))
                return ValidationProblem("Invalid Alternate Ordering ID for show.", nameof(body.AlternateOrderingID));

            show.PreferredAlternateOrderingID = null;
        }

        RepoFactory.TMDB_Show.Save(show);
        return Ok();
    }

    [HttpGet("Show/{showID}/CrossReferences")]
    public ActionResult<IReadOnlyList<TmdbShow.CrossReference>> GetCrossReferencesForTmdbShowByShowID(
        [FromRoute] int showID
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
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
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(show.Id))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
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
                var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
                if (alternateOrdering is null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                    return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

                return alternateOrdering.Cast
                    .Select(cast => new Role(cast))
                    .ToList();
            }

            if (alternateOrderingID != show.Id.ToString())
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");
        }

        return show.Cast
            .Select(cast => new Role(cast))
            .ToList();
    }

    [HttpGet("Show/{showID}/Crew")]
    public ActionResult<IReadOnlyList<Role>> GetCrewForTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(show.Id))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
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
                var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
                if (alternateOrdering is null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                    return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

                return alternateOrdering.Crew
                    .Select(cast => new Role(cast))
                    .ToList();
            }

            if (alternateOrderingID != show.Id.ToString())
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");
        }

        return show.Crew
            .Select(cast => new Role(cast))
            .ToList();
    }

    [HttpGet("Show/{showID}/Studios")]
    public ActionResult<IReadOnlyList<Studio>> GetStudiosForTmdbShowByShowID(
        [FromRoute] int showID
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(show.Id))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return show.TmdbCompanies
            .Select(company => new Studio(company))
            .ToList();
    }

    [HttpGet("Show/{showID}/Networks")]
    public ActionResult<IReadOnlyList<Network>> GetNetworksForTmdbShowByShowID(
        [FromRoute] int showID
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(show.Id))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
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
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(show.Id))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return new(show.ContentRatings.ToDto(language));
    }

    [HttpGet("Show/{showID}/Keywords")]
    public ActionResult<IReadOnlyList<string>> GetKeywordsForTmdbShowByShowID(
        [FromRoute] int showID
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(show.Id))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return show.Keywords;
    }

    [HttpGet("Show/{showID}/ProductionCountries")]
    public ActionResult<IReadOnlyDictionary<string, string>> GetProductionCountriesForTmdbShowByShowID(
        [FromRoute] int showID
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(show.Id))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return show.ProductionCountries
            .ToDictionary(country => country.CountryCode, country => country.CountryName);
    }

    [HttpGet("Show/{showID}/YearlySeasons")]
    public ActionResult<IReadOnlyList<YearlySeason>> GetYearlySeasonsForTmdbShowByShowID(
        [FromRoute] int showID
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(showID))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return show.Seasons.ToV3Dto();
    }

    [HttpGet("Show/{showID}/DaysOfWeek")]
    public ActionResult<IReadOnlyList<string>> GetDaysOfWeekForTmdbShowByShowID(
        [FromRoute] int showID
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(showID))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return show.TmdbEpisodes
            .Select(e => e.AiredAt?.DayOfWeek.ToString())
            .WhereNotDefault()
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
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(show.Id))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
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
                var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
                if (alternateOrdering is null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                    return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

                return alternateOrdering.TmdbAlternateOrderingSeasons
                    .ToListResult(season => new TmdbSeason(season, include?.CombineFlags()), page, pageSize);
            }

            if (alternateOrderingID != show.Id.ToString())
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");
        }

        return show.TmdbSeasons
            .ToListResult(season => new TmdbSeason(season, include?.CombineFlags(), language), page, pageSize);
    }


    [GeneratedRegex(@"^\s*(?=[SsEe])(?:(?<isSpecial>[Ss]pecial(?:s|\s*(?<specialNumber>\d+))?)|(?:[Ss](?<seasonNumber>\d+))?((?=[Ee])\s+)?(?:[Ee](?<episodeNumber>\d+))?)", RegexOptions.Compiled)]
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
    /// <param name="search">The optional search string to filter the results by.</param>
    /// <param name="fuzzy">Whether or not to search fuzzily.</param>
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
        [FromQuery] bool fuzzy = false
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(show.Id))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
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
                var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
                if (alternateOrdering is null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                    return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

                var altEpisodes = alternateOrdering.TmdbAlternateOrderingEpisodes
                    .Select(ordering => (ordering, episode: ordering.TmdbEpisode))
                    .Where(tuple => tuple.episode is not null)
                    .OfType<(TMDB_AlternateOrdering_Episode ordering, TMDB_Episode episode)>();
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
                    altEpisodes = altEpisodes.Search(search, t => t.episode.GetAllPreferredTitles().Select(t => t.Value), fuzzy).Select(r => r.Result);
                return altEpisodes
                    .ToListResult(t => new TmdbEpisode(show, t.episode, t.ordering, include?.CombineFlags(), language), page, pageSize);
            }

            if (alternateOrderingID != show.Id.ToString())
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");
        }

        IEnumerable<TMDB_Episode> episodes = show.TmdbEpisodes;
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
        return episodes.ToListResult(e => new TmdbEpisode(show, e, include?.CombineFlags(), language), page, pageSize);
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
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(show.Id))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
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
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(show.Id))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
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
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(show.Id))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
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
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("Show/{showID}/Shoko/Series")]
    public ActionResult<List<Series>> GetShokoSeriesByTmdbShowID(
        [FromRoute] int showID,
        [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(show.Id))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
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
    /// <param name="sortOrder">Sort ordering. Attach '-' at the start to reverse the order of the criteria.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("Show/{showID}/Shoko/File")]
    public ActionResult<ListResult<File>> GetShokoFilesByTmdbShowID(
        [FromRoute] int showID,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[]? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileExcludeTypes[]? exclude = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileIncludeOnlyType[]? include_only = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? sortOrder = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is not null && _tmdbMetadataService.WaitForShowUpdate(show.Id))
            show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        var videoLocals = show.EpisodeCrossReferences
            .Select(xref => xref.AnimeEpisode)
            .WhereNotNull()
            .SelectMany(xref => xref.VideoLocals)
            .DistinctBy(video => video.VideoLocalID);
        return ModelHelper.FilterFiles(videoLocals, User, pageSize, page, include, exclude, include_only, sortOrder, includeDataFrom);
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
        if (body.Immediate)
        {
            // If we want quick results, we're already running an update, and we already have episodes to use, then just return early.
            if (body.QuickRefresh && _tmdbMetadataService.IsShowUpdating(showID) && RepoFactory.TMDB_Episode.GetByTmdbShowID(showID).Count > 0)
                return Ok();

            var settings = SettingsProvider.GetSettings();
            await _tmdbMetadataService.UpdateShow(showID, !body.QuickRefresh && body.Force, body.DownloadImages, body.DownloadCrewAndCast ?? settings.TMDB.AutoDownloadCrewAndCast, body.DownloadAlternateOrdering ?? settings.TMDB.AutoDownloadAlternateOrdering, body.QuickRefresh);
            return Ok();
        }

        await _tmdbMetadataService.ScheduleUpdateOfShow(showID, body.Force, body.DownloadImages, body.DownloadCrewAndCast, body.DownloadAlternateOrdering);
        return NoContent();
    }

    /// <summary>
    /// Download images for a TMDB show.
    /// </summary>
    /// <param name="showID">TMDB Show ID.</param>
    /// <param name="body">Body containing options for downloading images.</param>
    /// <returns>
    /// If <paramref name="body.Immediate"/> is <see langword="true"/>, returns an <see cref="OkResult"/>,
    /// otherwise returns a <see cref="NoContentResult"/>.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("Show/{showID}/Action/DownloadImages")]
    public async Task<ActionResult> DownloadImagesForTmdbShowByShowID(
        [FromRoute] int showID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] TmdbDownloadImagesBody body
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        if (body.Immediate)
        {
            await _tmdbMetadataService.DownloadAllShowImages(showID, body.Force);
            return Ok();
        }

        await _tmdbMetadataService.ScheduleDownloadAllShowImages(showID, body.Force);
        return NoContent();
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
    public ListResult<Search.RemoteSearchShow> SearchOnlineForTmdbShows(
        [FromQuery] string query,
        [FromQuery] bool includeRestricted = false,
        [FromQuery, Range(0, int.MaxValue)] int year = 0,
        [FromQuery, Range(0, 100)] int pageSize = 6,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var (pageView, totalShows) = _tmdbSearchService.SearchShows(query, includeRestricted, year, page, pageSize)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

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
    [HttpPost("Show/Online/Bulk")]
    public async Task<ActionResult<List<Search.RemoteSearchShow>>> SearchBulkForTmdbShows(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] TmdbBulkSearchBody body
    )
    {
        // We don't care if the inputs are non-unique, but we don't want to double fetch,
        // so we do a distinct here, then at the end we map back to the original order.
        var uniqueIds = body.IDs.Distinct().ToList();
        var showDict = uniqueIds
            .Select(id => id <= 0 ? null : RepoFactory.TMDB_Show.GetByTmdbShowID(id))
            .WhereNotNull()
            .Select(show => new Search.RemoteSearchShow(show))
            .ToDictionary(show => show.ID);
        foreach (var id in uniqueIds.Except(showDict.Keys))
        {
            var show = id <= 0 ? null : await _tmdbMetadataService.UseClient(c => c.GetTvShowAsync(id), $"Get show {id}");
            if (show is null)
                continue;

            showDict[show.Id] = new Search.RemoteSearchShow(show);
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
        if (RepoFactory.TMDB_Show.GetByTmdbShowID(showID) is { } localShow)
            return new Search.RemoteSearchShow(localShow);

        if (await _tmdbMetadataService.UseClient(c => c.GetTvShowAsync(showID), $"Get show {showID}") is not { } remoteShow)
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
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is not null && _tmdbMetadataService.WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return new TmdbSeason(altOrderSeason, include?.CombineFlags());
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is not null && _tmdbMetadataService.WaitForShowUpdate(season.TmdbShowID))
            season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
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
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is not null && _tmdbMetadataService.WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return new List<Title>();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is not null && _tmdbMetadataService.WaitForShowUpdate(season.TmdbShowID))
            season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        var preferredTitle = season.GetPreferredTitle();
        return new(season.GetAllTitles().ToDto(season.EnglishTitle, preferredTitle, language));
    }

    [HttpGet("Season/{seasonID}/Overviews")]
    public ActionResult<IReadOnlyList<Overview>> GetOverviewsForTmdbSeasonBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is not null && _tmdbMetadataService.WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return new List<Overview>();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is not null && _tmdbMetadataService.WaitForShowUpdate(season.TmdbShowID))
            season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        var preferredOverview = season.GetPreferredOverview();
        return new(season.GetAllOverviews().ToDto(season.EnglishOverview, preferredOverview, language));
    }

    [HttpGet("Season/{seasonID}/Images")]
    public ActionResult<Images> GetImagesForTmdbSeasonBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID,
        [FromQuery] bool includeDisabled = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is not null && _tmdbMetadataService.WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return new Images();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is not null && _tmdbMetadataService.WaitForShowUpdate(season.TmdbShowID))
            season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        return season.GetImages()
            .ToDto(language, includeDisabled: includeDisabled, preferredPoster: season.DefaultPoster);
    }

    [HttpGet("Season/{seasonID}/Cast")]
    public ActionResult<IReadOnlyList<Role>> GetCastForTmdbSeasonBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is not null && _tmdbMetadataService.WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return altOrderSeason.Cast
                .Select(cast => new Role(cast))
                .ToList();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is not null && _tmdbMetadataService.WaitForShowUpdate(season.TmdbShowID))
            season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        return season.Cast
            .Select(cast => new Role(cast))
            .ToList();
    }

    [HttpGet("Season/{seasonID}/Crew")]
    public ActionResult<IReadOnlyList<Role>> GetCrewForTmdbSeasonBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is not null && _tmdbMetadataService.WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return altOrderSeason.Crew
                .Select(crew => new Role(crew))
                .ToList();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is not null && _tmdbMetadataService.WaitForShowUpdate(season.TmdbShowID))
            season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        return season.Crew
            .Select(crew => new Role(crew))
            .ToList();
    }

    [HttpGet("Season/{seasonID}/YearlySeasons")]
    public ActionResult<IReadOnlyList<YearlySeason>> GetYearlySeasonsForTmdbSeasonBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is not null && _tmdbMetadataService.WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return altOrderSeason.Seasons.ToV3Dto();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is not null && _tmdbMetadataService.WaitForShowUpdate(season.TmdbShowID))
            season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        return season.Seasons.ToV3Dto();
    }

    [HttpGet("Season/{seasonID}/DaysOfWeek")]
    public ActionResult<IReadOnlyList<string>> GetDaysOfWeekForTmdbSeasonBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is not null && _tmdbMetadataService.WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return altOrderSeason.TmdbAlternateOrderingEpisodes
                .Select(e => e.TmdbEpisode?.AiredAt?.DayOfWeek.ToString())
                .WhereNotDefault()
                .Distinct()
                .Order()
                .ToList();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is not null && _tmdbMetadataService.WaitForShowUpdate(season.TmdbShowID))
            season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        return season.TmdbEpisodes
            .Select(e => e.AiredAt?.DayOfWeek.ToString())
            .WhereNotDefault()
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
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is not null && _tmdbMetadataService.WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);
            var altOrder = altOrderSeason.TmdbAlternateOrdering;
            var altShow = altOrder?.TmdbShow;
            if (altShow is null)
                return NotFound(ShowNotFoundBySeasonID);

            return new TmdbShow(altShow, altOrder, include?.CombineFlags(), language);
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is not null && _tmdbMetadataService.WaitForShowUpdate(season.TmdbShowID))
            season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
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
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is not null && _tmdbMetadataService.WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            var altShow = altOrderSeason.TmdbShow;
            if (altShow is null)
                return NotFound(ShowNotFoundBySeasonID);

            var altEpisodes = altOrderSeason.TmdbAlternateOrderingEpisodes
                .Select(ordering => (ordering, episode: ordering.TmdbEpisode))
                .Where(tuple => tuple.episode is not null)
                .OfType<(TMDB_AlternateOrdering_Episode ordering, TMDB_Episode episode)>();
            if (includeHidden is not IncludeOnlyFilter.True)
            {
                var shouldHideHidden = includeHidden is IncludeOnlyFilter.False;
                altEpisodes = altEpisodes.Where(t => t.episode.IsHidden != shouldHideHidden);
            }
            return altEpisodes
                .ToListResult(t => new TmdbEpisode(altShow, t.episode, t.ordering, include?.CombineFlags(), language), page, pageSize);
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is not null && _tmdbMetadataService.WaitForShowUpdate(season.TmdbShowID))
            season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        var show = season.TmdbShow;
        if (show is null)
            return NotFound(ShowNotFoundBySeasonID);

        IEnumerable<TMDB_Episode> episodes = season.TmdbEpisodes;
        if (includeHidden is not IncludeOnlyFilter.True)
        {
            var shouldHideHidden = includeHidden is IncludeOnlyFilter.False;
            episodes = episodes.Where(e => e.IsHidden != shouldHideHidden);
        }
        return episodes
            .ToListResult(e => new TmdbEpisode(show, e, include?.CombineFlags(), language), page, pageSize);
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
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is not null && _tmdbMetadataService.WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return altOrderSeason.TmdbAlternateOrderingEpisodes
                .Select(altOrderEpisode => altOrderEpisode.TmdbEpisode)
                .WhereNotNull()
                .SelectMany(episode => episode.CrossReferences)
                .DistinctBy(xref => xref.AnidbAnimeID)
                .Select(xref => xref.AnidbAnime)
                .WhereNotNull()
                .Select(anime => new AnidbAnime(anime))
                .ToList();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is not null && _tmdbMetadataService.WaitForShowUpdate(season.TmdbShowID))
            season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
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
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is not null && _tmdbMetadataService.WaitForShowUpdate(altOrderSeason.TmdbShowID))
                altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return altOrderSeason.TmdbAlternateOrderingEpisodes
                .Select(altOrderEpisode => altOrderEpisode.TmdbEpisode)
                .WhereNotNull()
                .SelectMany(episode => episode.CrossReferences)
                .DistinctBy(xref => xref.AnidbAnimeID)
                .Select(xref => xref.AnimeSeries)
                .WhereNotNull()
                .Select(series => new Series(series, User.JMMUserID, randomImages, includeDataFrom))
                .ToList();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
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
    /// <param name="sortOrder">Sort ordering. Attach '-' at the start to reverse the order of the criteria.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("Season/{seasonID}/Shoko/File")]
    public ActionResult<ListResult<File>> GetShokoFilesBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[]? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileExcludeTypes[]? exclude = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileIncludeOnlyType[]? include_only = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? sortOrder = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            var videoLocals1 = altOrderSeason.TmdbAlternateOrderingEpisodes
                .Select(altOrderEpisode => altOrderEpisode.TmdbEpisode)
                .WhereNotNull()
                .SelectMany(episode => episode.CrossReferences)
                .Select(xref => xref.AnimeEpisode)
                .WhereNotNull()
                .SelectMany(xref => xref.VideoLocals)
                .DistinctBy(video => video.VideoLocalID);
            return ModelHelper.FilterFiles(videoLocals1, User, pageSize, page, include, exclude, include_only, sortOrder, includeDataFrom);
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        var videoLocals0 = season.TmdbEpisodes
            .SelectMany(episode => episode.CrossReferences)
            .Select(xref => xref.AnimeEpisode)
            .WhereNotNull()
            .SelectMany(xref => xref.VideoLocals)
            .DistinctBy(video => video.VideoLocalID);
        return ModelHelper.FilterFiles(videoLocals0, User, pageSize, page, include, exclude, include_only, sortOrder, includeDataFrom);
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
            .Select(episodeID => episodeID <= 0 ? null : RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID))
            .WhereNotNull()
            .GroupBy(episode => episode.TmdbShowID)
            .SelectMany(group =>
            {
                var show = group.First().TmdbShow
                    ?? throw new Exception(ShowNotFoundByEpisodeID);

                return group.Select(episode =>
                {
                    var alternateOrderingEpisode = !string.IsNullOrEmpty(show.PreferredAlternateOrderingID)
                        ? RepoFactory.TMDB_AlternateOrdering_Episode.GetByEpisodeGroupCollectionAndEpisodeIDs(show.PreferredAlternateOrderingID, episode.TmdbEpisodeID)
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
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
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
                var alternateOrderingEpisode = RepoFactory.TMDB_AlternateOrdering_Episode.GetByEpisodeGroupCollectionAndEpisodeIDs(alternateOrderingID, episodeID);
                if (alternateOrderingEpisode is null)
                    return ValidationProblem("Invalid alternateOrderingID for episode.", "alternateOrderingID");

                return new TmdbEpisode(show, episode, alternateOrderingEpisode, include?.CombineFlags(), language);
            }

            if (alternateOrderingID != episode.TmdbShowID.ToString())
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");
        }

        return new TmdbEpisode(show, episode, include?.CombineFlags(), language);
    }

    [HttpGet("Episode/{episodeID}/Titles")]
    public ActionResult<IReadOnlyList<Title>> GetTitlesForTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        var preferredTitle = episode.GetPreferredTitle();
        return new(episode.GetAllTitles().ToDto(episode.EnglishTitle, preferredTitle, language));
    }

    [HttpGet("Episode/{episodeID}/Overviews")]
    public ActionResult<IReadOnlyList<Overview>> GetOverviewsForTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        var preferredOverview = episode.GetPreferredOverview();
        return new(episode.GetAllOverviews().ToDto(episode.EnglishTitle, preferredOverview, language));
    }

    [HttpGet("Episode/{episodeID}/Ordering")]
    public ActionResult<IReadOnlyList<TmdbEpisode.OrderingInformation>> GetOrderingForTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
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

        var alternateOrderingEpisode = (TMDB_AlternateOrdering_Episode?)null;
        if (!string.IsNullOrWhiteSpace(alternateOrderingID) && alternateOrderingID != show.Id.ToString())
        {
            alternateOrderingEpisode = !string.IsNullOrWhiteSpace(alternateOrderingID) ? RepoFactory.TMDB_AlternateOrdering_Episode.GetByEpisodeGroupCollectionAndEpisodeIDs(alternateOrderingID, episodeID) : null;
            if (alternateOrderingEpisode is null || alternateOrderingEpisode.TmdbShowID != show.TmdbShowID)
                return ValidationProblem("Invalid alternateOrderingID for episode.", "alternateOrderingID");
        }

        var ordering = new List<TmdbEpisode.OrderingInformation>
        {
            new(show, episode, alternateOrderingEpisode),
        };
        foreach (var altOrderEp in episode.TmdbAlternateOrderingEpisodes)
            ordering.Add(new(show, altOrderEp, alternateOrderingEpisode));

        return ordering
            .OrderByDescending(o => o.IsDefault)
            .ThenBy(o => o.OrderingType)
            .ThenBy(o => o.OrderingName)
            .ToList();
    }

    [HttpGet("Episode/{episodeID}/Images")]
    public ActionResult<Images> GetImagesForTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery] bool includeDisabled = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        return episode.GetImages()
            .ToDto(language, includeDisabled: includeDisabled, includeThumbnails: true);
    }

    [HttpGet("Episode/{episodeID}/Cast")]
    public ActionResult<IReadOnlyList<Role>> GetCastForTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        return episode.Cast
            .Select(cast => new Role(cast))
            .ToList();
    }

    [HttpGet("Episode/{episodeID}/Crew")]
    public ActionResult<IReadOnlyList<Role>> GetCrewForTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        return episode.Crew
            .Select(cast => new Role(cast))
            .ToList();
    }

    [HttpGet("Episode/{episodeID}/CrossReferences")]
    public ActionResult<IReadOnlyList<TmdbEpisode.CrossReference>> GetCrossReferencesForTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
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
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        return FileCrossReference.From(episode.FileCrossReferences);
    }

    #endregion

    #region Actions

    [HttpPost("Episode/{episodeID}/Action/SetHiddenState")]
    public ActionResult SetHiddenStateForTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] TmdbEpisode.SetHiddenStateForTmdbEpisodeByEpisodeIDRequestBody? body
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        episode.IsHidden = body?.Value ?? true;
        RepoFactory.TMDB_Episode.Save(episode);

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
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
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
                var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
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
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
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
                var alternateOrderingEpisode = RepoFactory.TMDB_AlternateOrdering_Episode.GetByEpisodeGroupCollectionAndEpisodeIDs(alternateOrderingID, episodeID);
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
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
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
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
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
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
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
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
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
    /// <param name="sortOrder">Sort ordering. Attach '-' at the start to reverse the order of the criteria.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("Episode/{episodeID}/Shoko/File")]
    public ActionResult<ListResult<File>> GetShokoFilesByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[]? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileExcludeTypes[]? exclude = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileIncludeOnlyType[]? include_only = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? sortOrder = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        var videoLocals = episode.CrossReferences
            .Select(xref => xref.AnimeEpisode)
            .WhereNotNull()
            .SelectMany(xref => xref.VideoLocals)
            .DistinctBy(video => video.VideoLocalID);
        return ModelHelper.FilterFiles(videoLocals, User, pageSize, page, include, exclude, include_only, sortOrder, includeDataFrom);
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
    }

    private const string MovieCrossReferenceWithIdHeader = "AnidbAnimeId,AnidbEpisodeId,TmdbMovieId,IsAutomatic";

    private const string EpisodeCrossReferenceWithIdHeader = "AnidbAnimeId,AnidbEpisodeId,TmdbShowId,TmdbEpisodeId,Rating";

    private string MapAnimeType(AbstractAnimeType? type) =>
        type switch
        {
            AbstractAnimeType.Movie => "MV",
            AbstractAnimeType.OVA => "VA",
            AbstractAnimeType.TVSeries => "TV",
            AbstractAnimeType.TVSpecial => "SP",
            AbstractAnimeType.Web => "WB",
            AbstractAnimeType.Other => "OT",
            _ => "??",
        };

    /// <summary>
    /// Export all or selected AniDB/TMDB cross-references in the specified sections.
    /// </summary>
    /// <param name="body">Optional. Export options.</param>
    /// <returns></returns>
    [HttpPost("Export")]
    public ActionResult ExportCrossReferences(
        [FromBody] TmdbExportBody? body
    )
    {
        body ??= new();
        var sections = body.SectionSet?.CombineFlags() ?? default;
        var stringBuilder = new StringBuilder();
        if (sections.HasFlag(CrossReferenceExportType.Movie))
        {
            var crossReferences = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetAll()
                .Where(xref =>
                {
                    if (body.Automatic != IncludeOnlyFilter.True)
                    {
                        var includeAutomatic = body.Automatic == IncludeOnlyFilter.Only;
                        var isAutomatic = xref.Source == CrossRefSource.Automatic;
                        if (isAutomatic != includeAutomatic)
                            return false;
                    }
                    return body.ShouldKeep(xref);
                })
                .OrderBy(xref => xref.AnidbAnimeID)
                .ThenBy(xref => xref.AnidbEpisodeID)
                .ThenBy(xref => xref.TmdbMovieID)
                .SelectMany(xref =>
                {
                    var entry = $"{xref.AnidbAnimeID},{xref.AnidbEpisodeID},{xref.TmdbMovieID},{xref.Source == CrossRefSource.Automatic}";
                    if (!body.IncludeComments)
                        return new string[1] { entry };

                    var anime = xref.AnidbAnime;
                    var animeTitle = anime?.MainTitle ?? "<missing title>";
                    var movie = xref.TmdbMovie;
                    var movieTitle = movie?.EnglishTitle ?? "<missing title>";
                    var episodeNumber = "---";
                    var anidbEpisode = xref.AnidbEpisode;
                    if (anidbEpisode is null)
                        episodeNumber = "???";
                    else if (anidbEpisode.EpisodeType == (int)InternalEpisodeType.Episode)
                        episodeNumber = anidbEpisode.EpisodeNumber.ToString().PadLeft(3, '0');
                    else
                        episodeNumber = $"{((InternalEpisodeType)anidbEpisode.EpisodeType).ToString()[0]}{anidbEpisode.EpisodeNumber.ToString().PadLeft(2, '0')}";
                    episodeNumber += $" (e{xref.AnidbEpisodeID})";
                    var episodeTitle = anidbEpisode?.DefaultTitle is { AniDB_Episode_TitleID: > 0 } defaultTile ? defaultTile.Title : "<missing title>";
                    return
                    [
                        "",
                        $"# AniDB: {MapAnimeType(anime?.AbstractAnimeType)} ``{animeTitle}`` (a{xref.AnidbAnimeID}) {episodeNumber} ``{episodeTitle}`` (e{xref.AnidbEpisodeID}) → TMDB: ``{movieTitle}`` (m{xref.TmdbMovieID})",
                        entry,
                    ];
                })
                .ToList();
            if (crossReferences.Count > 0)
            {
                if (body.IncludeComments)
                    stringBuilder.AppendLine("#".PadRight(MovieCrossReferenceWithIdHeader.Length, '-'))
                        .AppendLine("# AniDB/TMDB Movie Cross-References");
                stringBuilder.AppendLine(MovieCrossReferenceWithIdHeader);
                if (body.IncludeComments)
                    stringBuilder.AppendLine("#".PadRight(MovieCrossReferenceWithIdHeader.Length, '-'))
                        .AppendLine();
                foreach (var line in crossReferences)
                    stringBuilder.AppendLine(line);
            }
        }

        if (body.IncludeComments && sections.HasFlag(CrossReferenceExportType.Movie) && sections.HasFlag(CrossReferenceExportType.Show))
            stringBuilder
                .AppendLine()
                .AppendLine();

        if (sections.HasFlag(CrossReferenceExportType.Show))
        {
            var crossReferences = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetAll()
                .Where(xref =>
                {
                    if (body.Automatic != IncludeOnlyFilter.True)
                    {
                        var includeAutomatic = body.Automatic == IncludeOnlyFilter.Only;
                        var isAutomatic = xref.MatchRating != MatchRating.UserVerified;
                        if (isAutomatic != includeAutomatic)
                            return false;
                    }
                    if (body.WithEpisodes != IncludeOnlyFilter.True)
                    {
                        var includeWithEpisode = body.WithEpisodes == IncludeOnlyFilter.Only;
                        var hasEpisode = xref.TmdbEpisodeID != 0;
                        if (hasEpisode != includeWithEpisode)
                            return false;
                    }
                    return body.ShouldKeep(xref);
                })
                .SelectMany(xref =>
                {
                    // NOTE: Internal easter eggs should stay internally.
                    var rating = xref.MatchRating is MatchRating.SarahJessicaParker ? "None" : xref.MatchRating.ToString();
                    var entry = $"{xref.AnidbAnimeID},{xref.AnidbEpisodeID},{xref.TmdbShowID},{xref.TmdbEpisodeID},{rating}";
                    if (!body.IncludeComments)
                        return new string[1] { entry };

                    var anidbAnime = xref.AnidbAnime;
                    var anidbAnimeTitle = anidbAnime?.MainTitle ?? "<missing title>";
                    var anidbEpisode = xref.AnidbEpisode;
                    var anidbEpisodeNumber = "???";
                    if (anidbEpisode is not null)
                        if (anidbEpisode.EpisodeTypeEnum == InternalEpisodeType.Episode)
                            anidbEpisodeNumber = anidbEpisode.EpisodeNumber.ToString().PadLeft(3, '0');
                        else
                            anidbEpisodeNumber = $"{anidbEpisode.EpisodeTypeEnum.ToString()[0]}{anidbEpisode.EpisodeNumber.ToString().PadLeft(2, '0')}";
                    var anidbEpisodeTitle = anidbEpisode?.DefaultTitle is { AniDB_Episode_TitleID: > 0 } defaultTile ? defaultTile.Title : "<missing title>";
                    var tmdbShow = xref.TmdbShow;
                    var tmdbShowTitle = tmdbShow?.EnglishTitle ?? "<missing title>";
                    var tmdbEpisode = xref.TmdbEpisode;
                    var tmdbEpisodeNumber = "??? ????";
                    if (tmdbEpisode is not null)
                        tmdbEpisodeNumber = $"S{tmdbEpisode.SeasonNumber.ToString().PadLeft(2, '0')} E{tmdbEpisode.EpisodeNumber.ToString().PadLeft(3, '0')}";
                    var tmdbEpisodeTitle = tmdbEpisode?.EnglishTitle ?? "<missing title>";
                    return
                    [
                        "",
                        $"# AniDB: {MapAnimeType(anidbAnime?.AbstractAnimeType)} ``{anidbAnimeTitle}`` (a{xref.AnidbAnimeID}) {anidbEpisodeNumber} ``{anidbEpisodeTitle}`` (e{xref.AnidbEpisodeID}) → TMDB: ``{tmdbShowTitle}`` (s{xref.TmdbShowID}) {tmdbEpisodeNumber} ``{tmdbEpisodeTitle}`` (e{xref.TmdbEpisodeID})",
                        entry,
                    ];
                })
                .ToList();
            if (crossReferences.Count > 0)
            {
                if (body.IncludeComments)
                    stringBuilder.AppendLine("#".PadRight(EpisodeCrossReferenceWithIdHeader.Length, '-'))
                        .AppendLine("# AniDB/TMDB Show/Episode Cross-References");
                stringBuilder.AppendLine(EpisodeCrossReferenceWithIdHeader);
                if (body.IncludeComments)
                    stringBuilder.AppendLine("#".PadRight(EpisodeCrossReferenceWithIdHeader.Length, '-'))
                        .AppendLine();
                foreach (var line in crossReferences)
                    stringBuilder.AppendLine(line);
            }
        }

        var bytes = Encoding.UTF8.GetBytes(stringBuilder.ToString());
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

        var allowedTypes = new HashSet<string>() { "text/plain", "text/csv" };
        if (file is not null && !allowedTypes.Contains(file.ContentType))
            ModelState.AddModelError("Body", "Invalid content-type for endpoint.");

        if (file is not null && file.Name != "file")
            ModelState.AddModelError("Body", "Invalid field name for import file");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        using var stream = new StreamReader(file!.OpenReadStream(), Encoding.UTF8, true);

        string? line;
        var lineNumber = 0;
        var currentHeader = "";
        var movieIdXrefs = new List<(int anidbAnime, int anidbEpisode, int tmdbMovie, bool isAutomatic)>();
        var episodeIdXrefs = new List<(int anidbAnime, int anidbEpisode, int tmdbShow, int tmdbEpisode, MatchRating rating)>();
        while ((line = stream.ReadLine()) is not null)
        {
            lineNumber++;
            if (line.Length == 0 || line[0] == '#')
                continue;

            switch (line)
            {
                case MovieCrossReferenceWithIdHeader:
                case EpisodeCrossReferenceWithIdHeader:
                    currentHeader = line;
                    continue;
            }

            if (string.IsNullOrEmpty(currentHeader) && ModelState.IsValid)
            {
                ModelState.AddModelError("Body", "Invalid or missing CSV header for import file.");
                continue;
            }

            switch (currentHeader)
            {
                default:
                case "":
                    ModelState.AddModelError("Body", $"Unable to parse cross-reference at line {lineNumber}.");
                    break;

                case MovieCrossReferenceWithIdHeader:
                {
                    var (animeId, episodeId, movieId, automatic) = line.Split(",");
                    if (
                        !int.TryParse(animeId, out var anidbAnimeId) || anidbAnimeId <= 0 ||
                        !int.TryParse(episodeId, out var anidbEpisodeId) || anidbEpisodeId <= 0 ||
                        !int.TryParse(movieId, out var tmdbMovieId) || tmdbMovieId <= 0 ||
                        !bool.TryParse(automatic, out var isAutomatic)
                    )
                    {
                        ModelState.AddModelError("Body", $"Unable to parse cross-reference at line {lineNumber}.");
                        continue;
                    }

                    movieIdXrefs.Add((anidbAnimeId, anidbEpisodeId, tmdbMovieId, isAutomatic));

                    break;
                }
                case EpisodeCrossReferenceWithIdHeader:
                {
                    var (anime, anidbEpisode, show, tmdbEpisode, rating) = line.Split(",");
                    if (
                        !int.TryParse(anime, out var anidbAnimeId) || anidbAnimeId <= 0 ||
                        !int.TryParse(anidbEpisode, out var anidbEpisodeId) || anidbEpisodeId <= 0 ||
                        !int.TryParse(show, out var tmdbShowId) || tmdbShowId < 0 ||
                        !int.TryParse(tmdbEpisode, out var tmdbEpisodeId) || tmdbEpisodeId < 0 ||
                        // NOTE: Internal easter eggs should stay internally.
                        !(
                            (Enum.TryParse<MatchRating>(rating, true, out var matchRating) && matchRating != MatchRating.SarahJessicaParker) ||
                            (string.Equals(rating, "None", StringComparison.InvariantCultureIgnoreCase) && (matchRating = MatchRating.SarahJessicaParker) == matchRating)
                        )
                    )
                    {
                        ModelState.AddModelError("Body", $"Unable to parse cross-reference at line {lineNumber}.");
                        continue;
                    }

                    episodeIdXrefs.Add((anidbAnimeId, anidbEpisodeId, tmdbShowId, tmdbEpisodeId, matchRating));
                    break;
                }
            }
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (movieIdXrefs.Count == 0 && episodeIdXrefs.Count == 0)
            ModelState.AddModelError("Body", "File contained no lines to import.");

        var moviesToPull = new HashSet<int>();
        var existingMovieXrefs = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetAll()
            .GroupBy(xref => $"{xref.AnidbAnimeID}:{xref.AnidbEpisodeID}")
            .ToDictionary(groupBy => groupBy.Key, groupBy => groupBy.ToDictionary(xref => xref.TmdbMovieID));
        var movieXrefsToAdd = 0;
        var movieXrefsToPotentiallyRemove = new List<CrossRef_AniDB_TMDB_Movie>();
        var movieXrefsToKeep = new List<CrossRef_AniDB_TMDB_Movie>();
        var movieXrefsToSave = new List<CrossRef_AniDB_TMDB_Movie>();
        foreach (var (animeId, episodeId, movieId, isAutomatic) in movieIdXrefs)
        {
            var id = $"{animeId}:{episodeId}";
            var updated = false;
            var isNew = false;
            var source = isAutomatic ? CrossRefSource.Automatic : CrossRefSource.User;
            CrossRef_AniDB_TMDB_Movie? xref = null;
            if (existingMovieXrefs.TryGetValue(id, out var xrefDict))
            {
                xrefDict.TryGetValue(movieId, out xref);
                if (removeExisting)
                    movieXrefsToPotentiallyRemove.AddRange(xrefDict.Values);
            }
            if (xref is null)
            {
                // Make sure an xref exists.
                movieXrefsToAdd++;
                updated = true;
                isNew = true;
                xref = new()
                {
                    AnidbAnimeID = animeId,
                    AnidbEpisodeID = episodeId,
                    TmdbMovieID = movieId,
                    Source = source,
                };
            }

            if (!isNew && xref.Source is not CrossRefSource.User && source is CrossRefSource.User)
            {
                xref.Source = source;
                updated = true;
            }

            if (updated)
                movieXrefsToSave.Add(xref);
            else if (!isNew)
                movieXrefsToKeep.Add(xref);

            if (!addMissingMovies)
                continue;

            var seriesExists = xref.AnimeSeries is not null;
            var tmdbMovieExists = xref.TmdbMovie is not null;
            if (seriesExists && !tmdbMovieExists)
                moviesToPull.Add(xref.TmdbMovieID);
        }

        var showsToPull = new HashSet<int>();
        var existingShowXrefsDict = RepoFactory.CrossRef_AniDB_TMDB_Show.GetAll()
            .GroupBy(xref => xref.AnidbAnimeID)
            .ToDictionary(groupBy => groupBy.Key, groupBy => groupBy.ToDictionary(xref => xref.TmdbShowID));
        var existingShowXrefs = existingShowXrefsDict.Values
            .SelectMany(xrefDict => xrefDict.Values)
            .Select(xref => $"{xref.AnidbAnimeID}:{xref.TmdbShowID}")
            .ToHashSet();
        var existingEpisodeXrefs = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetAll()
            .GroupBy(xref => $"{xref.AnidbAnimeID}:{xref.AnidbEpisodeID}")
            .ToDictionary(groupBy => groupBy.Key, groupBy => groupBy.ToDictionary(xref => $"{xref.TmdbShowID}:{xref.TmdbEpisodeID}"));
        var episodeXrefsToAdd = 0;
        var episodeXrefsToKeep = new List<CrossRef_AniDB_TMDB_Episode>();
        var episodeXrefsToSave = new List<CrossRef_AniDB_TMDB_Episode>();
        var episodeXrefsToPotentiallyRemove = new List<CrossRef_AniDB_TMDB_Episode>();
        var showXrefsToSave = new Dictionary<string, CrossRef_AniDB_TMDB_Show>();
        foreach (var (animeId, anidbEpisodeId, showId, tmdbEpisodeId, matchRating) in episodeIdXrefs)
        {
            var anidbId = $"{animeId}:{anidbEpisodeId}";
            var tmdbId = $"{showId}:{tmdbEpisodeId}";
            var isNew = false;
            var updated = false;
            CrossRef_AniDB_TMDB_Episode? xref = null;
            if (existingEpisodeXrefs.TryGetValue(anidbId, out var xrefDict))
            {
                xrefDict.TryGetValue(tmdbId, out xref);
                if (removeExisting)
                    episodeXrefsToPotentiallyRemove.AddRange(xrefDict.Values);
            }

            // Make sure an xref exists.
            if (xref is null)
            {
                episodeXrefsToAdd++;
                isNew = true;
                updated = true;
                xref = new()
                {
                    AnidbAnimeID = animeId,
                    AnidbEpisodeID = anidbEpisodeId,
                    TmdbShowID = showId,
                    TmdbEpisodeID = tmdbEpisodeId,
                    Ordering = 0,
                    MatchRating = matchRating,
                };
            }

            if (xref.TmdbEpisodeID != tmdbEpisodeId)
            {
                xref.TmdbEpisodeID = tmdbEpisodeId;
                updated = true;
            }

            if (xref.MatchRating != matchRating)
            {
                xref.MatchRating = matchRating;
                updated = true;
            }

            if (updated)
                episodeXrefsToSave.Add(xref);
            else if (!isNew)
                episodeXrefsToKeep.Add(xref);

            if (!existingShowXrefs.Contains($"{animeId}:{showId}"))
                showXrefsToSave.TryAdd($"{animeId}:{showId}", new(animeId, showId, CrossRefSource.User));

            if (!addMissingShows)
                continue;

            var seriesExists = xref.AnimeSeries is not null;
            var tmdbEpisodeExists = xref.TmdbEpisode is not null;
            if (seriesExists && !tmdbEpisodeExists)
                showsToPull.Add(xref.TmdbShowID);
        }

        var movieXrefsToRemove = movieXrefsToPotentiallyRemove
            .Except([.. movieXrefsToSave, .. movieXrefsToKeep])
            .ToList();
        if (movieXrefsToSave.Count > 0 || movieXrefsToRemove.Count > 0 || moviesToPull.Count > 0)
        {
            _logger.LogDebug(
                "Inserted {InsertedCount}, updated {UpdatedCount}, and skipped {SkippedCount} out of {TotalCount} movie cross-references in the imported file, removed {RemovedCount} existing movie cross-references, and scheduling {MovieCount} movies for update.",
                movieXrefsToAdd,
                movieXrefsToSave.Count - movieXrefsToAdd,
                movieXrefsToKeep.Count,
                movieIdXrefs.Count,
                movieXrefsToRemove.Count,
                moviesToPull.Count
            );

            RepoFactory.CrossRef_AniDB_TMDB_Movie.Save(movieXrefsToSave);
            RepoFactory.CrossRef_AniDB_TMDB_Movie.Delete(movieXrefsToRemove);

            foreach (var movieId in moviesToPull)
                await _tmdbMetadataService.ScheduleUpdateOfMovie(movieId);
        }

        var episodeXrefsToRemove = episodeXrefsToPotentiallyRemove
            .Except([.. episodeXrefsToSave, .. episodeXrefsToKeep])
            .ToList();
        if (episodeXrefsToSave.Count > 0 || episodeXrefsToRemove.Count > 0 || showXrefsToSave.Count > 0 || showsToPull.Count > 0)
        {
            _logger.LogDebug(
                "Inserted {InsertedCount}, updated {UpdatedCount}, and skipped {SkippedCount} out of {TotalCount} episode cross-references in the imported file, removed {RemovedCount} existing episode cross-references, inserted {TotalCount} show cross-references, and scheduling {ShowCount} shows for update.",
                episodeXrefsToAdd,
                episodeXrefsToSave.Count - episodeXrefsToAdd,
                episodeXrefsToKeep.Count,
                episodeIdXrefs.Count,
                episodeXrefsToRemove.Count,
                showXrefsToSave.Count,
                showsToPull.Count
            );

            RepoFactory.CrossRef_AniDB_TMDB_Show.Save(showXrefsToSave.Values.ToList());
            RepoFactory.CrossRef_AniDB_TMDB_Episode.Save(episodeXrefsToSave);
            RepoFactory.CrossRef_AniDB_TMDB_Episode.Delete(episodeXrefsToRemove);

            foreach (var showId in showsToPull)
                await _tmdbMetadataService.ScheduleUpdateOfShow(showId);
        }

        return NoContent();
    }

    #endregion
}
