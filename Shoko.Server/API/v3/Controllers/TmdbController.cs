using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Extensions;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using InternalEpisodeType = Shoko.Models.Enums.EpisodeType;
using CrossRefSource = Shoko.Models.Enums.CrossRefSource;
using MatchRating = Shoko.Models.Enums.MatchRating;
using DataSource = Shoko.Server.API.v3.Models.Common.DataSource;
using TmdbEpisode = Shoko.Server.API.v3.Models.TMDB.Episode;
using TmdbMovie = Shoko.Server.API.v3.Models.TMDB.Movie;
using TmdbSearch = Shoko.Server.API.v3.Models.TMDB.Search;
using TmdbSeason = Shoko.Server.API.v3.Models.TMDB.Season;
using TmdbShow = Shoko.Server.API.v3.Models.TMDB.Show;
using System.Threading.Tasks;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class TmdbController : BaseController
{
    private readonly ILogger<TmdbController> _logger;

    private readonly TmdbMetadataService _tmdbService;

    public TmdbController(ISettingsProvider settingsProvider, ILogger<TmdbController> logger, TmdbMetadataService tmdbService) : base(settingsProvider)
    {
        _logger = logger;
        _tmdbService = tmdbService;
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
        [FromRoute] string? search = null,
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
                .Concat(new TitleLanguage[] { TitleLanguage.English })
                .ToHashSet();
            return movies
                .Search(
                    search,
                    movie => movie.GetAllTitles()
                        .Where(title => languages.Contains(title.Language))
                        .Select(title => title.Value)
                        .Append(movie.EnglishTitle)
                        .Append(movie.OriginalTitle)
                        .Distinct()
                        .ToList(),
                    fuzzy
                )
                .ToListResult(a => new TmdbMovie(a.Result, include?.CombineFlags()), page, pageSize);
        }

        return movies
            .OrderBy(movie => movie.EnglishTitle)
            .ThenBy(movie => movie.TmdbMovieID)
            .ToListResult(m => new TmdbMovie(m, include?.CombineFlags()), page, pageSize);
    }

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
        if (movie is null)
            return NotFound(MovieNotFound);

        return new TmdbMovie(movie, include?.CombineFlags(), language);
    }

    /// <summary>
    /// Remove the local copy of the metadata for a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <param name="removeImageFiles">Also remove images related to the show.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("Movie/{movieID}")]
    public async Task<ActionResult> RemoveTmdbMovieByMovieID(
        [FromRoute] int movieID,
        [FromQuery] bool removeImageFiles = true
    )
    {
        await _tmdbService.SchedulePurgeOfMovie(movieID, removeImageFiles);

        return NoContent();
    }

    [HttpGet("Movie/{movieID}/Titles")]
    public ActionResult<IReadOnlyList<Title>> GetTitlesForTmdbMovieByMovieID(
        [FromRoute] int movieID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
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
        if (movie is null)
            return NotFound(MovieNotFound);

        var preferredOverview = movie.GetPreferredOverview();
        return new(movie.GetAllOverviews().ToDto(movie.EnglishTitle, preferredOverview, language));
    }

    [HttpGet("Movie/{movieID}/Images")]
    public ActionResult<Images> GetImagesForTmdbMovieByMovieID(
        [FromRoute] int movieID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.GetImages()
            .ToDto(language);
    }

    [HttpGet("Movie/{movieID}/Cast")]
    public ActionResult<IReadOnlyList<Role>> GetCastForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
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
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.CrossReferences
            .Select(xref => new TmdbMovie.CrossReference(xref))
            .ToList();
    }

    [HttpGet("Movie/{movieID}/Studios")]
    public ActionResult<IReadOnlyList<Studio>> GetStudiosForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.GetTmdbCompanies()
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
        if (movie is null)
            return NotFound(MovieNotFound);

        return new(movie.ContentRatings.ToDto(language));
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
    public ActionResult<List<Series.AniDB>> GetAniDBAnimeByTmdbMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.CrossReferences
            .Select(xref => xref.AnidbAnime)
            .WhereNotNull()
            .Select(anime => new Series.AniDB(anime))
            .ToList();
    }

    /// <summary>
    /// Get all AniDB episodes linked to a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <returns></returns>
    [HttpGet("Movie/{movieID}/AniDB/Episodes")]
    public ActionResult<List<Episode.AniDB>> GetAniDBEpisodesByTmdbMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.CrossReferences
            .Where(xref => xref.AnidbEpisodeID.HasValue)
            .Select(xref => xref.AnidbEpisode)
            .WhereNotNull()
            .Select(episode => new Episode.AniDB(episode))
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
    [HttpGet("Movie/{movieID}/Shoko/Episodes")]
    public ActionResult<List<Episode>> GetShokoEpisodesByTmdbMovieID(
        [FromRoute] int movieID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie is null)
            return NotFound(MovieNotFound);

        return movie.CrossReferences
            .Where(xref => xref.AnidbEpisodeID.HasValue)
            .Select(xref => xref.AnimeEpisode)
            .WhereNotNull()
            .Select(episode => new Episode(HttpContext, episode, includeDataFrom))
            .ToList();
    }

    #endregion

    #region Actions

    /// <summary>
    /// Refresh or download  the metadata for a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <param name="force">Forcefully download an update even if we updated recently.</param>
    /// <param name="downloadImages">Also download images.</param>
    /// <param name="downloadCrewAndCast">Also download crew and cast. Will respect global option if not set.</param>
    /// <param name="downloadCollections">Also download movie collection. Will respect global option if not set.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("Movie/{movieID}/Action/Refresh")]
    public async Task<ActionResult> RefreshTmdbMovieByMovieID(
        [FromRoute] int movieID,
        [FromQuery] bool force = false,
        [FromQuery] bool downloadImages = true,
        [FromQuery] bool? downloadCrewAndCast = null,
        [FromQuery] bool? downloadCollections = null
    )
    {
        await _tmdbService.ScheduleUpdateOfMovie(movieID, force, downloadImages, downloadCrewAndCast, downloadCollections);

        return Ok();
    }

    #endregion

    #region Search

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
    [HttpGet("Movie/Search/Online")]
    public ListResult<TmdbSearch.RemoteSearchMovie> SearchOnlineForTmdbMovies(
        [FromQuery] string query,
        [FromQuery] bool includeRestricted = false,
        [FromQuery, Range(0, int.MaxValue)] int year = 0,
        [FromQuery, Range(0, 100)] int pageSize = 6,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var (pageView, totalMovies) = _tmdbService.SearchMovies(query, includeRestricted, year, page, pageSize)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        return new ListResult<TmdbSearch.RemoteSearchMovie>(totalMovies, pageView.Select(a => new TmdbSearch.RemoteSearchMovie(a)));
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
        [FromRoute] string search,
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
                .Concat(new TitleLanguage[] { TitleLanguage.English })
                .ToHashSet();
            return RepoFactory.TMDB_Collection.GetAll()
                .Search(
                    search,
                    collection => collection.GetAllTitles()
                        .Where(title => languages.Contains(title.Language))
                        .Select(title => title.Value)
                        .Append(collection.EnglishTitle)
                        .Distinct()
                        .ToList(),
                    fuzzy
                )
                .ToListResult(a => new TmdbMovie.Collection(a.Result, include?.CombineFlags(), language), page, pageSize);
        }

        return RepoFactory.TMDB_Collection.GetAll()
            .ToListResult(a => new TmdbMovie.Collection(a, include?.CombineFlags(), language), page, pageSize);
    }

    [HttpGet("Movie/Collection/{collectionID}")]
    public ActionResult<TmdbMovie.Collection> GetMovieCollectionByCollectionID(
        [FromRoute] int collectionID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbMovie.Collection.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var collection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(collectionID);
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
        if (collection is null)
            return NotFound(MovieCollectionNotFound);

        var preferredOverview = collection.GetPreferredOverview();
        return new(collection.GetAllOverviews().ToDto(collection.EnglishTitle, preferredOverview, language));
    }

    [HttpGet("Movie/Collection/{collectionID}/Images")]
    public ActionResult<Images> GetImagesForMovieCollectionByCollectionID(
        [FromRoute] int collectionID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var collection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(collectionID);
        if (collection is null)
            return NotFound(MovieCollectionNotFound);

        return collection.GetImages()
            .ToDto(language);
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
        if (collection is null)
            return NotFound(MovieCollectionNotFound);

        return collection.GetTmdbMovies()
            .Select(movie => new TmdbMovie(movie, include?.CombineFlags(), language))
            .ToList();
    }

    #endregion

    #endregion

    #region Shows

    #region Constants

    internal const string AlternateOrderingIdRegex = @"^[a-f0-9]{24}$";

    internal const string ShowNotFound = "A TMDB.Show by the given `showID` was not found.";

    internal const string ShowNotFoundBySeasonID = "A TMDB.Show by the given `seasonID` was not found";

    internal const string ShowNotFoundByOrderingID = "A TMDB.Show by the given `orderingID` was not found";

    internal const string ShowNotFoundByEpisodeID = "A TMDB.Show by the given `seasonID` was not found";

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
        [FromRoute] string? search = null,
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
                .Concat(new TitleLanguage[] { TitleLanguage.English })
                .ToHashSet();
            return shows
                .Search(
                    search,
                    show => show.GetAllTitles()
                        .Where(title => languages.Contains(title.Language))
                        .Select(title => title.Value)
                        .Append(show.EnglishTitle)
                        .Append(show.OriginalTitle)
                        .Distinct()
                        .ToList(),
                    fuzzy
                )
                .ToListResult(a => new TmdbShow(a.Result, include?.CombineFlags(), language), page, pageSize);
        }

        return shows
            .OrderBy(show => show.EnglishTitle)
            .ThenBy(show => show.TmdbShowID)
            .ToListResult(m => new TmdbShow(m, include?.CombineFlags()), page, pageSize);
    }

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
        if (show is null)
            return NotFound(ShowNotFound);

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
            if (alternateOrdering is null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

            return new TmdbShow(show, alternateOrdering, include?.CombineFlags(), language);
        }

        return new TmdbShow(show, include?.CombineFlags());
    }

    /// <summary>
    /// Remove the local copy of the metadata for a TMDB show.
    /// </summary>
    /// <param name="showID">TMDB Movie ID.</param>
    /// <param name="removeImageFiles">Also remove images related to the show.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("Show/{showID}")]
    public async Task<ActionResult> RemoveTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery] bool removeImageFiles = true
    )
    {
        await _tmdbService.SchedulePurgeOfShow(showID, removeImageFiles);

        return NoContent();
    }

    [HttpGet("Show/{showID}/Titles")]
    public ActionResult<IReadOnlyList<Title>> GetTitlesForTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
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
        if (show is null)
            return NotFound(ShowNotFound);

        var preferredOverview = show.GetPreferredOverview();
        return new(show.GetAllOverviews().ToDto(show.EnglishOverview, preferredOverview, language));
    }

    [HttpGet("Show/{showID}/Images")]
    public ActionResult<Images> GetImagesForTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return show.GetImages()
            .ToDto(language);
    }

    [HttpGet("Show/{showID}/Ordering")]
    public ActionResult<IReadOnlyList<TmdbShow.OrderingInformation>> GetOrderingForTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        var alternateOrdering = !string.IsNullOrWhiteSpace(alternateOrderingID) ? RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID) : null;
        if (!string.IsNullOrWhiteSpace(alternateOrderingID) && (alternateOrdering is null || alternateOrdering.TmdbShowID != show.TmdbShowID))
            return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

        var ordering = new List<TmdbShow.OrderingInformation>
        {
            new(show, alternateOrdering),
        };
        foreach (var altOrder in show.TmdbAlternateOrdering)
            ordering.Add(new(altOrder, alternateOrdering));
        return ordering
            .OrderByDescending(o => o.InUse)
            .ThenByDescending(o => string.IsNullOrEmpty(o.OrderingID))
            .ThenBy(o => o.OrderingName)
            .ToList();
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
        if (show is null)
            return NotFound(ShowNotFound);

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
            if (alternateOrdering is null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

            return alternateOrdering.Cast
                .Select(cast => new Role(cast))
                .ToList();
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
        if (show is null)
            return NotFound(ShowNotFound);

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
            if (alternateOrdering is null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

            return alternateOrdering.Crew
                .Select(cast => new Role(cast))
                .ToList();
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
        if (show is null)
            return NotFound(ShowNotFound);

        return new(show.ContentRatings.ToDto(language));
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
        if (show is null)
            return NotFound(ShowNotFound);

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
            if (alternateOrdering is null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

            return alternateOrdering.TmdbAlternateOrderingSeasons
                .ToListResult(season => new TmdbSeason(season, include?.CombineFlags()), page, pageSize);
        }

        return show.TmdbSeasons
            .ToListResult(season => new TmdbSeason(season, include?.CombineFlags(), language), page, pageSize);
    }

    [HttpGet("Show/{showID}/Episode")]
    public ActionResult<ListResult<TmdbEpisode>> GetTmdbEpisodesByTmdbShowID(
        [FromRoute] int showID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbEpisode.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
            if (alternateOrdering is null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

            return alternateOrdering.TmdbAlternateOrderingEpisodes
                .ToListResult(e => new TmdbEpisode(e.GetTmdbEpisode()!, e, include?.CombineFlags(), language), page, pageSize);
        }

        return show.TmdbEpisodes
            .ToListResult(e => new TmdbEpisode(e, include?.CombineFlags(), language), page, pageSize);
    }

    #endregion

    #region Cross-Source Linked Entries

    /// <summary>
    /// Get all AniDB series linked to a TMDB show.
    /// </summary>
    /// <param name="showID">TMDB Show ID.</param>
    /// <returns></returns>
    [HttpGet("Show/{showID}/AniDB/Anime")]
    public ActionResult<List<Series.AniDB>> GetAnidbAnimeByTmdbShowID(
        [FromRoute] int showID
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show is null)
            return NotFound(ShowNotFound);

        return show.CrossReferences
            .Select(xref => xref.AnidbAnime)
            .WhereNotNull()
            .Select(anime => new Series.AniDB(anime))
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
        if (show is null)
            return NotFound(ShowNotFound);

        return show.CrossReferences
            .Select(xref => xref.AnimeSeries)
            .WhereNotNull()
            .Select(series => new Series(series, User.JMMUserID, randomImages, includeDataFrom))
            .ToList();
    }

    #endregion

    #region Actions

    /// <summary>
    /// Refresh or download the metadata for a TMDB show.
    /// </summary>
    /// <param name="showID">TMDB Show ID.</param>
    /// <param name="force">Forcefully download an update even if we updated recently.</param>
    /// <param name="downloadImages">Also download images.</param>
    /// <param name="downloadCrewAndCast">Also download crew and cast. Will respect global option if not set.</param>
    /// <param name="downloadAlternateOrdering">Also download alternate ordering information. Will respect global option if not set.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("Show/{showID}/Action/Refresh")]
    public async Task<ActionResult> RefreshTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery] bool force = false,
        [FromQuery] bool downloadImages = true,
        [FromQuery] bool? downloadCrewAndCast = null,
        [FromQuery] bool? downloadAlternateOrdering = null
    )
    {
        await _tmdbService.ScheduleUpdateOfShow(showID, force, downloadImages, downloadCrewAndCast, downloadAlternateOrdering);

        return Ok();
    }

    #endregion

    #region Search

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
    [HttpGet("Show/Search/Online")]
    public ListResult<TmdbSearch.RemoteSearchShow> SearchOnlineForTmdbShows(
        [FromQuery] string query,
        [FromQuery] bool includeRestricted = false,
        [FromQuery, Range(0, int.MaxValue)] int year = 0,
        [FromQuery, Range(0, 100)] int pageSize = 6,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var (pageView, totalShows) = _tmdbService.SearchShows(query, includeRestricted, year, page, pageSize)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        return new ListResult<TmdbSearch.RemoteSearchShow>(totalShows, pageView.Select(a => new TmdbSearch.RemoteSearchShow(a)));
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
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return new TmdbSeason(altOrderSeason, include?.CombineFlags());
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
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
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return new List<Title>();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
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
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return new List<Overview>();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        var preferredOverview = season.GetPreferredOverview();
        return new(season.GetAllOverviews().ToDto(season.EnglishOverview, preferredOverview, language));
    }

    [HttpGet("Season/{seasonID}/Images")]
    public ActionResult<Images> GetImagesForTmdbSeasonBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return new Images();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        return season.GetImages().ToDto(language);
    }

    [HttpGet("Season/{seasonID}/Cast")]
    public ActionResult<IReadOnlyList<Role>> GetCastForTmdbSeasonBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return altOrderSeason.Cast
                .Select(cast => new Role(cast))
                .ToList();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        return season.GetCast()
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
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return altOrderSeason.Crew
                .Select(crew => new Role(crew))
                .ToList();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        return season.GetCrew()
            .Select(crew => new Role(crew))
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
        if (season is null)
            return NotFound(SeasonNotFound);

        var show = season.GetTmdbShow();
        if (show is null)
            return NotFound(ShowNotFoundBySeasonID);

        return new TmdbShow(show, include?.CombineFlags(), language);
    }

    [HttpGet("Season/{seasonID}/Episode")]
    public ActionResult<ListResult<TmdbEpisode>> GetTmdbEpisodesBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbEpisode.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return altOrderSeason.TmdbAlternateOrderingEpisodes
                .ToListResult(e => new TmdbEpisode(e.GetTmdbEpisode()!, e, include?.CombineFlags(), language), page, pageSize);
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        return season.GetTmdbEpisodes()
            .ToListResult(e => new TmdbEpisode(e, include?.CombineFlags(), language), page, pageSize);
    }

    #endregion

    #region Cross-Source Linked Entries

    [HttpGet("Season/{seasonID}/AniDB/Anime")]
    public ActionResult<List<Series.AniDB>> GetAniDBAnimeBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return new List<Series.AniDB>();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        return season.GetTmdbEpisodes()
            .SelectMany(episode => episode.CrossReferences)
            .DistinctBy(xref => xref.AnidbAnimeID)
            .Select(xref => xref.AnidbAnime)
            .WhereNotNull()
            .Select(anime => new Series.AniDB(anime))
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
            if (altOrderSeason is null)
                return NotFound(SeasonNotFound);

            return new List<Series>();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season is null)
            return NotFound(SeasonNotFound);

        return season.GetTmdbEpisodes()
            .SelectMany(episode => episode.CrossReferences)
            .DistinctBy(xref => xref.AnidbAnimeID)
            .Select(xref => xref.AnimeSeries)
            .WhereNotNull()
            .Select(series => new Series(series, User.JMMUserID, randomImages, includeDataFrom))
            .ToList();
    }

    #endregion

    #endregion

    #region Episodes

    #region Constants

    internal const string EpisodeNotFound = "A TMDB.Episode by the given `episodeID` was not found.";

    #endregion

    #region Basics

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
        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrderingEpisode = RepoFactory.TMDB_AlternateOrdering_Episode.GetByEpisodeGroupCollectionAndEpisodeIDs(alternateOrderingID, episodeID);
            if (alternateOrderingEpisode is null)
                return ValidationProblem("Invalid alternateOrderingID for episode.", "alternateOrderingID");

            return new TmdbEpisode(episode, alternateOrderingEpisode, include?.CombineFlags(), language);
        }

        return new TmdbEpisode(episode, include?.CombineFlags(), language);
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
        var alternateOrderingEpisode = !string.IsNullOrWhiteSpace(alternateOrderingID)
            ? RepoFactory.TMDB_AlternateOrdering_Episode.GetByEpisodeGroupCollectionAndEpisodeIDs(alternateOrderingID, episodeID) : null;
        if (!string.IsNullOrWhiteSpace(alternateOrderingID) && alternateOrderingEpisode is null)
            return ValidationProblem("Invalid alternateOrderingID for episode.", "alternateOrderingID");

        var ordering = new List<TmdbEpisode.OrderingInformation>
        {
            new(episode, alternateOrderingEpisode),
        };
        foreach (var altOrderEp in episode.TmdbAlternateOrderingEpisodes)
            ordering.Add(new(altOrderEp, alternateOrderingEpisode));

        return ordering
            .OrderByDescending(o => o.InUse)
            .ThenByDescending(o => string.IsNullOrEmpty(o.OrderingID))
            .ThenBy(o => o.OrderingName)
            .ToList();
    }

    [HttpGet("Episode/{episodeID}/Images")]
    public ActionResult<IReadOnlyList<Image>> GetImagesForTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode is null)
            return NotFound(EpisodeNotFound);

        return episode.GetImages()
            .InLanguage(language)
            .Select(image => new Image(image))
            .ToList();
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

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
            if (alternateOrdering is null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

            return new TmdbShow(show, alternateOrdering, include?.CombineFlags(), language);
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

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrderingEpisode = RepoFactory.TMDB_AlternateOrdering_Episode.GetByEpisodeGroupCollectionAndEpisodeIDs(alternateOrderingID, episodeID);
            var altOrderSeason = alternateOrderingEpisode?.GetTmdbAlternateOrderingSeason();
            if (altOrderSeason is null)
                return NotFound(SeasonNotFoundByEpisodeID);

            return new TmdbSeason(altOrderSeason, include?.CombineFlags());
        }

        var season = episode.TmdbSeason;
        if (season is null)
            return NotFound(SeasonNotFoundByEpisodeID);

        return new TmdbSeason(season, include?.CombineFlags(), language);
    }

    #endregion

    #region Cross-Source Linked Entries

    [HttpGet("Episode/{episodeID}/AniDB/Anime")]
    public ActionResult<List<Series.AniDB>> GetAniDBAnimeByEpisodeID(
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
            .Select(anime => new Series.AniDB(anime))
            .ToList();
    }

    [HttpGet("Episode/{episodeID}/Anidb/Episode")]
    public ActionResult<List<Episode.AniDB>> GetAniDBEpisodeByEpisodeID(
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
            .Select(anidbEpisode => new Episode.AniDB(anidbEpisode))
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

    private const string EpisodeCrossReferenceWithIdHeader = "AnidbAnimeId,AnidbEpisodeType,AnidbEpisodeId,TmdbShowId,TmdbEpisodeId,Rating";

    /// <summary>
    /// Export any and all AniDB/TMDB cross-references in the specified sections.
    /// </summary>
    /// <param name="automatic">Include/exclude automatically made cross-references</param>
    /// <param name="withEpisodes">Include/exclude cross-references with an episode. That is movie cross-references with an anidb episode set, or episode cross-references with a tmdb episode set.</param>
    /// <param name="comments">Append human friendly comments in the output file. They serve no purpose other than to enlighten the humans reading the file what each cross-reference is for.</param>
    /// <param name="sectionSet">Sections to include in the output file, if we have anything to fill in in the selected sections.</param>
    /// <returns></returns>
    [HttpPost("Export")]
    public ActionResult ExportCrossReferences(
        [FromQuery] IncludeOnlyFilter automatic = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter withEpisodes = IncludeOnlyFilter.True,
        [FromQuery] bool comments = false,
        [FromQuery(Name = "sections"), ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<CrossReferenceExportType>? sectionSet = null
    )
    {
        var sections = sectionSet?.CombineFlags() ?? default;
        var stringBuilder = new StringBuilder();
        if (sections.HasFlag(CrossReferenceExportType.Movie))
        {
            var crossReferences = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetAll()
                .Where(xref =>
                {
                    if (automatic != IncludeOnlyFilter.True)
                    {
                        var includeAutomatic = automatic == IncludeOnlyFilter.Only;
                        var isAutomatic = xref.Source == CrossRefSource.Automatic;
                        if (isAutomatic != includeAutomatic)
                            return false;
                    }
                    if (withEpisodes != IncludeOnlyFilter.True)
                    {
                        var includeWithEpisode = withEpisodes == IncludeOnlyFilter.Only;
                        var hasEpisode = xref.AnidbEpisodeID.HasValue;
                        if (hasEpisode != includeWithEpisode)
                            return false;
                    }
                    return true;
                })
                .OrderBy(xref => xref.AnidbAnimeID)
                .ThenBy(xref => xref.AnidbEpisodeID)
                .ThenBy(xref => xref.TmdbMovieID)
                .SelectMany(xref =>
                {
                    var entry = $"{xref.AnidbAnimeID},{xref.AnidbEpisodeID ?? 0},{xref.TmdbMovieID},{xref.Source == CrossRefSource.Automatic}";
                    if (!comments)
                        return new string[1] { entry };

                    var anidbAnime = xref.AnidbAnime;
                    var tmdbMovie = xref.TmdbMovie;
                    var episodeNumber = "---";
                    if (xref.AnidbEpisodeID.HasValue)
                    {
                        var anidbEpisode = xref.AnidbEpisode;
                        if (anidbEpisode is null)
                            episodeNumber = "???";
                        else if (anidbEpisode.EpisodeType == (int)InternalEpisodeType.Episode)
                            episodeNumber = anidbEpisode.EpisodeNumber.ToString().PadLeft(3, '0');
                        else
                            episodeNumber = $"{((InternalEpisodeType)anidbEpisode.EpisodeType).ToString()[0]}{anidbEpisode.EpisodeNumber.ToString().PadLeft(2, '0')}";
                        episodeNumber += $" (e{xref.AnidbEpisodeID.Value})";
                    }
                    return new string[3]
                    {
                        "",
                        $"# AniDB: {anidbAnime?.MainTitle ?? "<missing title>"} (a{xref.AnidbAnimeID}) {episodeNumber} → TMDB: {tmdbMovie?.EnglishTitle ?? "<missing title>"} (m{xref.TmdbMovieID})",
                        entry,
                    };
                })
                .ToList();
            if (crossReferences.Count > 0)
            {
                if (comments)
                    stringBuilder.AppendLine("#".PadRight(MovieCrossReferenceWithIdHeader.Length, '-'))
                        .AppendLine("# AniDB/TMDB Movie Cross-References");
                stringBuilder.AppendLine(MovieCrossReferenceWithIdHeader);
                if (comments)
                    stringBuilder.AppendLine("#".PadRight(MovieCrossReferenceWithIdHeader.Length, '-'))
                        .AppendLine();
                foreach (var line in crossReferences)
                    stringBuilder.AppendLine(line);
            }
        }

        if (sections.HasFlag(CrossReferenceExportType.Show))
        {
            var crossReferences = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetAll()
                .Where(xref =>
                {
                    if (automatic != IncludeOnlyFilter.True)
                    {
                        var includeAutomatic = automatic == IncludeOnlyFilter.Only;
                        var isAutomatic = xref.MatchRating != MatchRating.UserVerified;
                        if (isAutomatic != includeAutomatic)
                            return false;
                    }
                    if (withEpisodes != IncludeOnlyFilter.True)
                    {
                        var includeWithEpisode = withEpisodes == IncludeOnlyFilter.Only;
                        var hasEpisode = xref.TmdbEpisodeID != 0;
                        if (hasEpisode != includeWithEpisode)
                            return false;
                    }
                    return true;
                })
                .SelectMany(xref =>
                {
                    // NOTE: Internal easter eggs should stay internally.
                    var rating = xref.MatchRating == MatchRating.SarahJessicaParker ? "None" : xref.MatchRating.ToString();
                    var entry = $"{xref.AnidbAnimeID},{xref.AnidbEpisodeID},{xref.TmdbShowID},{xref.TmdbEpisodeID},{rating}";
                    if (!comments)
                        return new string[1] { entry };

                    var anidbAnime = xref.AnidbAnime;
                    var anidbEpisode = xref.AnidbEpisode;
                    var anidbEpisodeNumber = "???";
                    if (anidbEpisode is not null)
                        if (anidbEpisode.EpisodeType == (int)InternalEpisodeType.Episode)
                            anidbEpisodeNumber = anidbEpisode.EpisodeNumber.ToString().PadLeft(3, '0');
                        else
                            anidbEpisodeNumber = $"{((InternalEpisodeType)anidbEpisode.EpisodeType).ToString()[0]}{anidbEpisode.EpisodeNumber.ToString().PadLeft(2, '0')}";
                    var tmdbShow = xref.GetTmdbShow();
                    var tmdbEpisode = xref.TmdbEpisode;
                    var tmdbEpisodeNumber = "??? ????";
                    if (tmdbEpisode is not null)
                        tmdbEpisodeNumber = $"S{tmdbEpisode.SeasonNumber.ToString().PadLeft(2, '0')} E{tmdbEpisode.EpisodeNumber.ToString().PadLeft(3, '0')}";
                    return new string[3]
                    {
                        "",
                        $"# AniDB: {anidbAnime?.MainTitle ?? "<missing title>"} (a{xref.AnidbAnimeID}) {anidbEpisodeNumber} → TMDB: {tmdbShow?.EnglishTitle ?? "<missing title>"} (s{xref.TmdbShowID}) {tmdbEpisodeNumber}",
                        entry,
                    };
                })
                .ToList();
            if (crossReferences.Count > 0)
            {
                if (comments)
                    stringBuilder.AppendLine("#".PadRight(EpisodeCrossReferenceWithIdHeader.Length, '-'))
                        .AppendLine("# AniDB/TMDB Show/Episode Cross-References");
                stringBuilder.AppendLine(EpisodeCrossReferenceWithIdHeader);
                if (comments)
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
    /// <returns>Void.</returns>
    [HttpPost("Import")]
    public async Task<ActionResult> ImportMovieCrossReferences(
        IFormFile file
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
        var movieIdXrefs = new List<(int anidbAnime, int? anidbEpisode, int tmdbMovie, bool isAutomatic)>();
        var episodeIdXrefs = new List<(int anidbAnime, int anidbEpisode, int tmdbShow, int tmdbEpisode, MatchRating rating)>();
        while (!string.IsNullOrEmpty(line = stream.ReadLine()))
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
                            !int.TryParse(episodeId, out var anidbEpisodeId) || anidbEpisodeId < 0 ||
                            !int.TryParse(movieId, out var tmdbMovieId) || tmdbMovieId <= 0 ||
                            !bool.TryParse(automatic, out var isAutomatic)
                        )
                        {
                            ModelState.AddModelError("Body", $"Unable to parse cross-reference at line {lineNumber}.");
                            continue;
                        }

                        movieIdXrefs.Add((anidbAnimeId, anidbEpisodeId == 0 ? null : anidbEpisodeId, tmdbMovieId, isAutomatic));
                        break;
                    }
                case EpisodeCrossReferenceWithIdHeader:
                    {
                        var (anime, anidbEpisode, show, tmdbEpisode, rating) = line.Split(",");
                        if (
                            !int.TryParse(anime, out var anidbAnimeId) || anidbAnimeId <= 0 ||
                            !int.TryParse(anidbEpisode, out var anidbEpisodeId) || anidbEpisodeId < 0 ||
                            !int.TryParse(show, out var tmdbShowId) || tmdbShowId < 0 ||
                            !int.TryParse(tmdbEpisode, out var tmdbEpisodeId) || tmdbEpisodeId < -1 ||
                            // NOTE: Internal easter eggs should stay internally.
                            !(
                                (Enum.TryParse<MatchRating>(rating, out var matchRating) && matchRating != MatchRating.SarahJessicaParker) ||
                                (rating == "None" && (matchRating = MatchRating.SarahJessicaParker) == matchRating)
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

        if (ModelState.IsValid && movieIdXrefs.Count == 0)
            ModelState.AddModelError("Body", "File contained no lines to import.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var moviesToUpdate = new HashSet<int>();
        var usedMovieIdsWithZeroSet = new HashSet<string>();
        var exitingMovieXrefs = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetAll()
            .ToDictionary(xref => $"{xref.AnidbAnimeID}:{xref.AnidbEpisodeID ?? 0}");
        var movieXrefsToAdd = 0;
        var movieXrefsToSave = new List<CrossRef_AniDB_TMDB_Movie>();
        foreach (var (animeId, episodeId, movieId, isAutomatic) in movieIdXrefs)
        {
            var idWithZero = $"{animeId}:0";
            var id = $"{animeId}:{episodeId ?? 0}";
            var updated = false;
            var source = isAutomatic ? CrossRefSource.Automatic : CrossRefSource.User;
            if (!exitingMovieXrefs.TryGetValue(id, out var xref))
            {
                // Also check the zero id if we haven't already.
                if (!usedMovieIdsWithZeroSet.Contains(idWithZero) && (id == idWithZero || !exitingMovieXrefs.TryGetValue(idWithZero, out xref) || true))
                    usedMovieIdsWithZeroSet.Add(idWithZero);

                // Make sure an xref exists.
                if (xref is null)
                {
                    movieXrefsToAdd++;
                    updated = true;
                    xref = new()
                    {
                        AnidbAnimeID = animeId,
                        AnidbEpisodeID = episodeId,
                        TmdbMovieID = movieId,
                        Source = source,
                    };
                }
            }

            if (xref.AnidbEpisodeID != episodeId)
            {
                xref.AnidbEpisodeID = episodeId;
                updated = true;
            }

            if (xref.TmdbMovieID != movieId)
            {
                xref.TmdbMovieID = movieId;
                updated = true;
            }

            if (xref.Source != source)
            {
                xref.Source = source;
                updated = true;
            }

            if (updated)
                movieXrefsToSave.Add(xref);

            var animeExists = xref.AnidbAnime is not null;
            var tmdbMovieExists = xref.TmdbMovie is not null;
            if (animeExists && !tmdbMovieExists)
                moviesToUpdate.Add(xref.TmdbMovieID);
        }

        var showsToUpdate = new HashSet<int>();
        var usedEpisodeIdsWithZeroSet = new HashSet<string>();
        var existingShowXrefs = RepoFactory.CrossRef_AniDB_TMDB_Show.GetAll()
            .Select(xref => $"{xref.AnidbAnimeID}:{xref.TmdbShowID}")
            .ToHashSet();
        var exitingEpisodeXrefs = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetAll()
            .ToDictionary(xref => $"{xref.AnidbAnimeID}:{xref.AnidbEpisodeID}:{xref.TmdbShowID}:{xref.TmdbEpisodeID}");
        var episodeXrefsToAdd = 0;
        var episodeXrefsToSave = new List<CrossRef_AniDB_TMDB_Episode>();
        var showXrefsToSave = new List<CrossRef_AniDB_TMDB_Show>();
        foreach (var (animeId, anidbEpisodeId, showId, tmdbEpisodeId, matchRating) in episodeIdXrefs)
        {
            var idWithZero = $"{animeId}:{anidbEpisodeId}:{showId}:0";
            var id = $"{animeId}:{anidbEpisodeId}:{showId}:{tmdbEpisodeId}";
            var updated = false;
            if (!exitingEpisodeXrefs.TryGetValue(id, out var xref))
            {
                // Also check the zero id if we haven't already.
                if (!usedEpisodeIdsWithZeroSet.Contains(idWithZero) && (id == idWithZero || !exitingEpisodeXrefs.TryGetValue(idWithZero, out xref) || true))
                    usedEpisodeIdsWithZeroSet.Add(idWithZero);

                // Make sure an xref exists.
                if (xref is null)
                {
                    episodeXrefsToAdd++;
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

            var animeExists = xref.AnidbAnime is not null;
            var tmdbEpisodeExists = xref.TmdbEpisode is not null;
            if (animeExists && !tmdbEpisodeExists)
                showsToUpdate.Add(xref.TmdbShowID);

            if (!existingShowXrefs.Contains($"{animeId}:{showId}"))
                showXrefsToSave.Add(new(animeId, showId, CrossRefSource.User));
        }

        if (movieXrefsToSave.Count > 0 || moviesToUpdate.Count > 0)
        {
            _logger.LogDebug(
                "Inserted {InsertedCount} and updated {UpdatedCount} out of {TotalCount} movie cross-references in the imported file, and scheduling {MovieCount} movies for update.",
                movieXrefsToAdd,
                movieXrefsToSave.Count - movieXrefsToAdd,
                movieIdXrefs.Count,
                moviesToUpdate.Count
            );

            RepoFactory.CrossRef_AniDB_TMDB_Movie.Save(movieXrefsToSave);

            foreach (var movieId in moviesToUpdate)
                await _tmdbService.SchedulePurgeOfMovie(movieId);
        }

        if (episodeXrefsToSave.Count > 0 || showXrefsToSave.Count > 0 || showsToUpdate.Count > 0)
        {
            _logger.LogDebug(
                "Inserted {InsertedCount} and updated {UpdatedCount} out of {TotalCount} episode cross-references in the imported file, inserted {TotalCount} show cross-references and scheduling {ShowCount} shows for update.",
                episodeXrefsToAdd,
                episodeXrefsToSave.Count - episodeXrefsToAdd,
                episodeIdXrefs.Count,
                showXrefsToSave.Count,
                showsToUpdate.Count
            );

            RepoFactory.CrossRef_AniDB_TMDB_Show.Save(showXrefsToSave);
            RepoFactory.CrossRef_AniDB_TMDB_Episode.Save(episodeXrefsToSave);

            foreach (var showId in showsToUpdate)
                await _tmdbService.SchedulePurgeOfShow(showId);
        }

        return NoContent();
    }

    #endregion
}
