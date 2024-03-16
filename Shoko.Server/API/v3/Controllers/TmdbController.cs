using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Commands;
using Shoko.Server.Models;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using TMDbLib.Objects.Search;

using DataSource = Shoko.Server.API.v3.Models.Common.DataSource;
using TmdbEpisode = Shoko.Server.API.v3.Models.TMDB.Episode;
using TmdbMovie = Shoko.Server.API.v3.Models.TMDB.Movie;
using TmdbSeason = Shoko.Server.API.v3.Models.TMDB.Season;
using TmdbShow = Shoko.Server.API.v3.Models.TMDB.Show;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class TmdbController : BaseController
{
    private readonly ICommandRequestFactory CommandFactory;

    private readonly TMDBHelper TmdbHelper;

    public TmdbController(ICommandRequestFactory commandFactory, ISettingsProvider settingsProvider, TMDBHelper tmdbHelper) : base(settingsProvider)
    {
        CommandFactory = commandFactory;
        TmdbHelper = tmdbHelper;
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
    /// <param name="isRestricted"></param>
    /// <param name="isVideo"></param>
    /// <param name="pageSize"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    [HttpGet("Movie")]
    public ActionResult<ListResult<TmdbMovie>> GetTmdbMovies(
        [FromRoute] string? search = null,
        [FromQuery] bool fuzzy = true,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbMovie.IncludeDetails>? include = null,
        [FromQuery] bool? isRestricted = null,
        [FromQuery] bool? isVideo = null,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        var movies = RepoFactory.TMDB_Movie.GetAll()
            .AsParallel()
            .Where(movie =>
            {
                if (isRestricted.HasValue && isRestricted.Value != movie.IsRestricted)
                    return false;

                if (isVideo.HasValue && isVideo.Value != movie.IsVideo)
                    return false;

                return true;
            });
        if (hasSearch)
        {
            var languages = SettingsProvider.GetSettings()
                .LanguagePreference
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
    /// <returns></returns>
    [HttpGet("Movie/{movieID}")]
    public ActionResult<TmdbMovie> GetTmdbMovieByMovieID(
        [FromRoute] int movieID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbMovie.IncludeDetails>? include = null
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie == null)
            return NotFound(MovieNotFound);

        return new TmdbMovie(movie, include?.CombineFlags());
    }

    /// <summary>
    /// Remove the local copy of the metadata for a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("Movie/{movieID}")]
    public ActionResult RemoveTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie == null)
            return NotFound(MovieNotFound);

        CommandFactory.CreateAndSave<CommandRequest_TMDB_Movie_Purge>(c => c.TmdbMovieID = movieID);

        return NoContent();
    }

    [HttpGet("Movie/{movieID}/Titles")]
    public ActionResult<IReadOnlyList<Title>> GetTitlesForTmdbMovieByMovieID(
        [FromRoute] int movieID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie == null)
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
        if (movie == null)
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
        if (movie == null)
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
        if (movie == null)
            return NotFound(MovieNotFound);

        return movie.GetCast()
            .Select(cast => new Role(cast))
            .ToList();
    }

    [HttpGet("Movie/{movieID}/Crew")]
    public ActionResult<IReadOnlyList<Role>> GetCrewForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie == null)
            return NotFound(MovieNotFound);

        return movie.GetCrew()
            .Select(cast => new Role(cast))
            .ToList();
    }

    [HttpGet("Movie/{movieID}/CrossReferences")]
    public ActionResult<IReadOnlyList<TmdbMovie.CrossReference>> GetCrossReferencesForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie == null)
            return NotFound(MovieNotFound);

        return movie.GetCrossReferences()
            .Select(xref => new TmdbMovie.CrossReference(xref))
            .ToList();
    }

    [HttpGet("Movie/{movieID}/Studios")]
    public ActionResult<IReadOnlyList<Studio>> GetStudiosForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie == null)
            return NotFound(MovieNotFound);

        return movie.GetTmdbCompanies()
            .Select(company => new Studio(company))
            .ToList();
    }

    [HttpGet("Movie/{movieID}/ContentRatings")]
    public ActionResult<IReadOnlyList<ContentRating>> GetContentRatingsForTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie == null)
            return NotFound(MovieNotFound);

        return movie.ContentRatings
            .Select(rating => new ContentRating(rating))
            .ToList();
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
        if (movie == null)
            return NotFound(MovieNotFound);

        var movieCollection = movie.GetTmdbCollection();
        if (movieCollection == null)
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
        if (movie == null)
            return NotFound(MovieNotFound);

        return movie.GetCrossReferences()
            .Select(xref => xref.GetAnidbAnime())
            .OfType<SVR_AniDB_Anime>()
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
        if (movie == null)
            return NotFound(MovieNotFound);

        return movie.GetCrossReferences()
            .Where(xref => xref.AnidbEpisodeID.HasValue)
            .Select(xref => xref.GetAnidbEpisode())
            .OfType<AniDB_Episode>()
            .Select(episode => new Episode.AniDB(episode))
            .ToList();
    }

    /// <summary>
    /// Get all Shoko series linked to a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <param name="randomImages">Randomise images shown for the <see cref="Series"/>.</param>
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
        if (movie == null)
            return NotFound(MovieNotFound);

        return movie.GetCrossReferences()
            .Select(xref => xref.GetShokoSeries())
            .OfType<SVR_AnimeSeries>()
            .Select(series => new Series(HttpContext, series, randomImages, includeDataFrom))
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
        if (movie == null)
            return NotFound(MovieNotFound);

        return movie.GetCrossReferences()
            .Where(xref => xref.AnidbEpisodeID.HasValue)
            .Select(xref => xref.GetShokoEpisode())
            .OfType<SVR_AnimeEpisode>()
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
    /// <param name="downloadCollections"></param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("Movie/{movieID}/Action/Refresh")]
    public ActionResult RefreshTmdbMovieByMovieID(
        [FromRoute] int movieID,
        [FromQuery] bool force = false,
        [FromQuery] bool downloadImages = true,
        [FromQuery] bool? downloadCollections = null
    )
    {
        CommandFactory.CreateAndSave<CommandRequest_TMDB_Movie_Update>(c =>
        {
            c.TmdbMovieID = movieID;
            c.ForceRefresh = force;
            c.DownloadImages = downloadImages;
            c.DownloadCollections = downloadCollections;
        });

        return Ok();
    }

    #endregion

    #region Search

    /// <summary>
    /// Search TMDB for movies using the offline or online search.
    /// </summary>
    /// <param name="query">Query to search for.</param>
    /// <param name="includeRestricted">Include restriced movies.</param>
    /// <param name="page">The page index.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("Movie/Search/Online")]
    public ListResult<SearchMovie> SearchOnlineForTmdbMovies(
        [FromQuery] string query,
        [FromQuery] bool includeRestricted = false,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var (pageView, totalMovies) = TmdbHelper.SearchMovies(query, includeRestricted, page);

        return new ListResult<SearchMovie>(totalMovies, pageView);
    }

    /// <summary>
    /// Search TMDB for movies using the offline or online search.
    /// </summary>
    /// <param name="query">Query to search for.</param>
    /// <param name="fuzzy">Indicates fuzzy-matching should be used for the search.</param>
    /// <param name="restricted">Only search for results which are or are not restriced if set, otherwise will include both restricted and not restriced movies.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("Movie/Search/Offline")]
    public ListResult<object> SearchOfflineForTmdbMovies(
        [FromQuery] string query,
        [FromQuery] bool fuzzy = true,
        [FromQuery] IncludeOnlyFilter restricted = IncludeOnlyFilter.False,
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        // TODO: Modify this once the tmdb movie search model is finalised. Also maybe switch to using online search, maybe utilising the offline search if we're offline.

        return TmdbHelper.OfflineSearch.SearchMovies(query, fuzzy)
            .Where(movie =>
            {
                if (restricted != IncludeOnlyFilter.True)
                {
                    var includeRestricred = restricted == IncludeOnlyFilter.Only;
                    var isRestricted = movie.IsRestricted;
                    if (isRestricted != includeRestricred)
                        return false;
                }

                return true;
            })
            .ToListResult(a => new { a.ID, a.IsRestricted, a.Popularity, a.Title } as object, page, pageSize);
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
                .LanguagePreference
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
        if (collection == null)
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
        if (collection == null)
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
        if (collection == null)
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
        if (collection == null)
            return NotFound(MovieCollectionNotFound);

        return collection.GetImages()
            .ToDto(language);
    }

    #endregion

    #region Same-Source Linked Entries

    [HttpGet("Movie/Collection/{collecitonID}/Movie")]
    public ActionResult<List<TmdbMovie>> GetMoviesForMovieCollectionByCollectionID(
        [FromRoute] int collectionID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbMovie.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var collection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(collectionID);
        if (collection == null)
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
    /// <param name="isRestricted"></param>
    /// <param name="pageSize"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    [HttpGet("Show")]
    public ActionResult<ListResult<TmdbShow>> GetTmdbShows(
        [FromRoute] string? search = null,
        [FromQuery] bool fuzzy = true,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbShow.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery] bool? isRestricted = null,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        var shows = RepoFactory.TMDB_Show.GetAll()
            .AsParallel()
            .Where(show =>
            {
                if (isRestricted.HasValue && isRestricted.Value != show.IsRestricted)
                    return false;

                return true;
            });
        if (hasSearch)
        {
            var languages = SettingsProvider.GetSettings()
                .LanguagePreference
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
        if (show == null)
            return NotFound(ShowNotFound);

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
            if (alternateOrdering == null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

            return new TmdbShow(show, alternateOrdering, include?.CombineFlags(), language);
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
    public ActionResult RemoveTmdbShowByShowID(
        [FromRoute] int showID
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show == null)
            return NotFound(ShowNotFound);

        CommandFactory.CreateAndSave<CommandRequest_TMDB_Show_Purge>(c => c.TmdbShowID = showID);

        return NoContent();
    }

    [HttpGet("Show/{showID}/Titles")]
    public ActionResult<IReadOnlyList<Title>> GetTitlesForTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show == null)
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
        if (show == null)
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
        if (show == null)
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
        if (show == null)
            return NotFound(ShowNotFound);

        var alternateOrdering = !string.IsNullOrWhiteSpace(alternateOrderingID) ? RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID) : null;
        if (!string.IsNullOrWhiteSpace(alternateOrderingID) && (alternateOrdering == null || alternateOrdering.TmdbShowID != show.TmdbShowID))
            return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

        var ordering = new List<TmdbShow.OrderingInformation>
        {
            new(show, alternateOrdering),
        };
        foreach (var altOrder in show.GetTmdbAlternateOrdering())
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
        if (show == null)
            return NotFound(ShowNotFound);

        return show.GetCrossReferences()
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
        if (show == null)
            return NotFound(ShowNotFound);

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
            if (alternateOrdering == null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

            return alternateOrdering.GetCast()
                .Select(cast => new Role(cast))
                .ToList();
        }

        return show.GetCast()
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
        if (show == null)
            return NotFound(ShowNotFound);

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
            if (alternateOrdering == null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

            return alternateOrdering.GetCrew()
                .Select(cast => new Role(cast))
                .ToList();
        }

        return show.GetCrew()
            .Select(cast => new Role(cast))
            .ToList();
    }

    [HttpGet("Show/{showID}/Studios")]
    public ActionResult<IReadOnlyList<Studio>> GetStudiosForTmdbShowByShowID(
        [FromRoute] int showID
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show == null)
            return NotFound(ShowNotFound);

        return show.GetTmdbCompanies()
            .Select(company => new Studio(company))
            .ToList();
    }

    [HttpGet("Show/{showID}/Networks")]
    public ActionResult<IReadOnlyList<Network>> GetNetworksForTmdbShowByShowID(
        [FromRoute] int showID
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show == null)
            return NotFound(ShowNotFound);

        return show.GetTmdbNetworks()
            .Select(network => new Network(network))
            .ToList();
    }

    [HttpGet("Show/{showID}/ContentRatings")]
    public ActionResult<IReadOnlyList<ContentRating>> GetContentRatingsForTmdbShowByShowID(
        [FromRoute] int showID
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show == null)
            return NotFound(ShowNotFound);

        return show.ContentRatings
            .Select(rating => new ContentRating(rating))
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
        if (show == null)
            return NotFound(ShowNotFound);

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
            if (alternateOrdering == null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

            return alternateOrdering.GetTmdbAlternateOrderingSeasons()
                .ToListResult(season => new TmdbSeason(season, include?.CombineFlags()), page, pageSize);
        }

        return show.GetTmdbSeasons()
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
        if (show == null)
            return NotFound(ShowNotFound);

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
            if (alternateOrdering == null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

            return alternateOrdering.GetTmdbAlternateOrderingEpisodes()
                .ToListResult(e => new TmdbEpisode(e.GetTmdbEpisode()!, e, include?.CombineFlags(), language), page, pageSize);
        }

        return show.GetTmdbEpisodes()
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
        if (show == null)
            return NotFound(ShowNotFound);

        return show.GetCrossReferences()
            .Select(xref => xref.GetAnidbAnime())
            .OfType<SVR_AniDB_Anime>()
            .Select(anime => new Series.AniDB(anime))
            .ToList();
    }

    /// <summary>
    /// Get all Shoko series linked to a TMDB show.
    /// </summary>
    /// <param name="showID">TMDB Show ID.</param>
    /// <param name="randomImages">Randomise images shown for the <see cref="Series"/>.</param>
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
        if (show == null)
            return NotFound(ShowNotFound);

        return show.GetCrossReferences()
            .Select(xref => xref.GetShokoSeries())
            .OfType<SVR_AnimeSeries>()
            .Select(series => new Series(HttpContext, series, randomImages, includeDataFrom))
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
    /// <param name="downloadAlternateOrdering"></param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("Show/{showID}/Action/Refresh")]
    public ActionResult RefreshTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery] bool force = false,
        [FromQuery] bool downloadImages = true,
        [FromQuery] bool? downloadAlternateOrdering = null
    )
    {
        CommandFactory.CreateAndSave<CommandRequest_TMDB_Show_Update>(c =>
        {
            c.TmdbShowID = showID;
            c.ForceRefresh = force;
            c.DownloadImages = downloadImages;
            c.DownloadAlternateOrdering = downloadAlternateOrdering;
        });

        return Ok();
    }

    #endregion

    #region Search

    /// <summary>
    /// Search TMDB for shows using the online search.
    /// </summary>
    /// <param name="query">Query to search for.</param>
    /// <param name="includeRestricted">Include restriced shows.</param>
    /// <param name="page">The page index.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("Show/Search/Online")]
    public ListResult<SearchTv> SearchOnlineForTmdbShows(
        [FromQuery] string query,
        [FromQuery] bool includeRestricted = false,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var (pageView, totalShows) = TmdbHelper.SearchShows(query, includeRestricted, page);

        return new ListResult<SearchTv>(totalShows, pageView);
    }

    /// <summary>
    /// Search TMDB for shows using the offline or online search.
    /// </summary>
    /// <param name="query">Query to search for.</param>
    /// <param name="fuzzy">Indicates fuzzy-matching should be used for the search.</param>
    /// <param name="restricted">Only search for results which are or are not restriced if set, otherwise will include both restricted and not restriced shows.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("Show/Search/Offline")]
    public ListResult<object> SearchOfflineForTmdbShows(
        [FromQuery] string query,
        [FromQuery] bool fuzzy = true,
        [FromQuery] IncludeOnlyFilter restricted = IncludeOnlyFilter.False,
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        // TODO: Modify this once the tmdb show search model is finalised. Also maybe switch to using online search, maybe utilising the offline search if we're offline.

        return TmdbHelper.OfflineSearch.SearchShows(query, fuzzy)
            .Where(show =>
            {
                if (restricted != IncludeOnlyFilter.True)
                {
                    var includeRestricred = restricted == IncludeOnlyFilter.Only;
                    var isRestricted = show.IsRestricted;
                    if (isRestricted != includeRestricred)
                        return false;
                }

                return true;
            })
            .ToListResult(a => new { a.ID, a.IsRestricted, a.Popularity, a.Title } as object, page, pageSize);
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
            if (altOrderSeason == null)
                return NotFound(SeasonNotFound);

            return new TmdbSeason(altOrderSeason, include?.CombineFlags());
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season == null)
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
            if (altOrderSeason == null)
                return NotFound(SeasonNotFound);

            return new List<Title>();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season == null)
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
            if (altOrderSeason == null)
                return NotFound(SeasonNotFound);

            return new List<Overview>();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season == null)
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
            if (altOrderSeason == null)
                return NotFound(SeasonNotFound);

            return new Images();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season == null)
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
            if (altOrderSeason == null)
                return NotFound(SeasonNotFound);

            return altOrderSeason.GetCast()
                .Select(cast => new Role(cast))
                .ToList();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season == null)
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
            if (altOrderSeason == null)
                return NotFound(SeasonNotFound);

            return altOrderSeason.GetCrew()
                .Select(crew => new Role(crew))
                .ToList();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season == null)
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
            if (altOrderSeason == null)
                return NotFound(SeasonNotFound);
            var altOrder = altOrderSeason.GetTmdbAlternateOrdering();
            var altShow = altOrder?.GetTmdbShow();
            if (altShow == null)
                return NotFound(ShowNotFoundBySeasonID);

            return new TmdbShow(altShow, altOrder, include?.CombineFlags(), language);
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season == null)
            return NotFound(SeasonNotFound);

        var show = season.GetTmdbShow();
        if (show == null)
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
            if (altOrderSeason == null)
                return NotFound(SeasonNotFound);

            return altOrderSeason.GetTmdbAlternateOrderingEpisodes()
                .ToListResult(e => new TmdbEpisode(e.GetTmdbEpisode()!, e, include?.CombineFlags(), language), page, pageSize);
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season == null)
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
            if (altOrderSeason == null)
                return NotFound(SeasonNotFound);

            return new List<Series.AniDB>();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season == null)
            return NotFound(SeasonNotFound);

        return season.GetTmdbEpisodes()
            .SelectMany(episode => episode.GetCrossReferences())
            .DistinctBy(xref => xref.AnidbAnimeID)
            .Select(xref => xref.GetAnidbAnime())
            .OfType<SVR_AniDB_Anime>()
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
            if (altOrderSeason == null)
                return NotFound(SeasonNotFound);

            return new List<Series>();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season == null)
            return NotFound(SeasonNotFound);

        return season.GetTmdbEpisodes()
            .SelectMany(episode => episode.GetCrossReferences())
            .DistinctBy(xref => xref.AnidbAnimeID)
            .Select(xref => xref.GetShokoSeries())
            .OfType<SVR_AnimeSeries>()
            .Select(series => new Series(HttpContext, series, randomImages, includeDataFrom))
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
        if (episode == null)
            return NotFound(EpisodeNotFound);
        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrderingEpisode = RepoFactory.TMDB_AlternateOrdering_Episode.GetByEpisodeGroupCollectionAndEpisodeIDs(alternateOrderingID, episodeID);
            if (alternateOrderingEpisode == null)
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
        if (episode == null)
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
        if (episode == null)
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
        if (episode == null)
            return NotFound(EpisodeNotFound);
        var alternateOrderingEpisode = !string.IsNullOrWhiteSpace(alternateOrderingID)
            ? RepoFactory.TMDB_AlternateOrdering_Episode.GetByEpisodeGroupCollectionAndEpisodeIDs(alternateOrderingID, episodeID) : null;
        if (!string.IsNullOrWhiteSpace(alternateOrderingID) && alternateOrderingEpisode == null)
            return ValidationProblem("Invalid alternateOrderingID for episode.", "alternateOrderingID");

        var ordering = new List<TmdbEpisode.OrderingInformation>
        {
            new(episode, alternateOrderingEpisode),
        };
        foreach (var altOrderEp in episode.GetTmdbAlternateOrderingEpisodes())
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
        if (episode == null)
            return NotFound(EpisodeNotFound);

        return episode.GetImages()
            .InLanguage(language)
            .Select(image => image.ToDto())
            .ToList();
    }

    [HttpGet("Episode/{episodeID}/Cast")]
    public ActionResult<IReadOnlyList<Role>> GetCastForTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFound);

        return episode.GetCast()
            .Select(cast => new Role(cast))
            .ToList();
    }

    [HttpGet("Episode/{episodeID}/Crew")]
    public ActionResult<IReadOnlyList<Role>> GetCrewForTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFound);

        return episode.GetCrew()
            .Select(cast => new Role(cast))
            .ToList();
    }

    [HttpGet("Episode/{episodeID}/CrossReferences")]
    public ActionResult<IReadOnlyList<TmdbEpisode.CrossReference>> GetCrossReferencesForTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFound);

        return episode.GetCrossReferences()
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
        if (episode == null)
            return NotFound(EpisodeNotFound);

        var show = episode.GetTmdbShow();
        if (show == null)
            return NotFound(ShowNotFoundByEpisodeID);

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
            if (alternateOrdering == null || alternateOrdering.TmdbShowID != show.TmdbShowID)
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
        if (episode == null)
            return NotFound(EpisodeNotFound);

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrderingEpisode = RepoFactory.TMDB_AlternateOrdering_Episode.GetByEpisodeGroupCollectionAndEpisodeIDs(alternateOrderingID, episodeID);
            var altOrderSeason = alternateOrderingEpisode?.GetTmdbAlternateOrderingSeason();
            if (altOrderSeason == null)
                return NotFound(SeasonNotFoundByEpisodeID);

            return new TmdbSeason(altOrderSeason, include?.CombineFlags());
        }

        var season = episode.GetTmdbSeason();
        if (season == null)
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
        if (episode == null)
            return NotFound(EpisodeNotFound);

        return episode.GetCrossReferences()
            .DistinctBy(xref => xref.AnidbAnimeID)
            .Select(xref => xref.GetAnidbAnime())
            .OfType<SVR_AniDB_Anime>()
            .Select(anime => new Series.AniDB(anime))
            .ToList();
    }

    [HttpGet("Episode/{episodeID}/Anidb/Episode")]
    public ActionResult<List<Episode.AniDB>> GetAniDBEpisodeByEpisodeID(
        [FromRoute] int episodeID
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFound);

        return episode.GetCrossReferences()
            .DistinctBy(xref => xref.AnidbAnimeID)
            .Select(xref => xref.GetAnidbEpisode())
            .OfType<AniDB_Episode>()
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
        if (episode == null)
            return NotFound(EpisodeNotFound);

        return episode.GetCrossReferences()
            .DistinctBy(xref => xref.AnidbAnimeID)
            .Select(xref => xref.GetShokoSeries())
            .OfType<SVR_AnimeSeries>()
            .Select(shokoSeries => new Series(HttpContext, shokoSeries, randomImages, includeDataFrom))
            .ToList();
    }

    [HttpGet("Episode/{episodeID}/Shoko/Episode")]
    public ActionResult<List<Episode>> GetShokoEpisodesByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFound);

        return episode.GetCrossReferences()
            .DistinctBy(xref => xref.AnidbEpisodeID)
            .Select(xref => xref.GetShokoEpisode())
            .OfType<SVR_AnimeEpisode>()
            .Select(shokoEpisode => new Episode(HttpContext, shokoEpisode, includeDataFrom))
            .ToList();
    }

    #endregion

    #endregion
}
