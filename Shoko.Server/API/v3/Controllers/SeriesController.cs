using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using EpisodeType = Shoko.Server.API.v3.Models.Shoko.EpisodeType;
using AniDBEpisodeType = Shoko.Models.Enums.EpisodeType;
using DataSource = Shoko.Server.API.v3.Models.Common.DataSource;
using TmdbEpisode = Shoko.Server.API.v3.Models.TMDB.Episode;
using TmdbMovie = Shoko.Server.API.v3.Models.TMDB.Movie;
using TmdbSeason = Shoko.Server.API.v3.Models.TMDB.Season;
using TmdbShow = Shoko.Server.API.v3.Models.TMDB.Show;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class SeriesController : BaseController
{
    private readonly AnimeSeriesService _seriesService;
    private readonly AniDBTitleHelper _titleHelper;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly TmdbMetadataService _tmdbService;
    private readonly CrossRef_File_EpisodeRepository _crossRefFileEpisode;
    private readonly WatchedStatusService _watchedService;

    public SeriesController(ISettingsProvider settingsProvider, AnimeSeriesService seriesService, AniDBTitleHelper titleHelper, ISchedulerFactory schedulerFactory, TmdbMetadataService tmdbService, CrossRef_File_EpisodeRepository crossRefFileEpisode, WatchedStatusService watchedService) : base(settingsProvider)
    {
        _seriesService = seriesService;
        _titleHelper = titleHelper;
        _schedulerFactory = schedulerFactory;
        _tmdbService = tmdbService;
        _crossRefFileEpisode = crossRefFileEpisode;
        _watchedService = watchedService;
    }

    #region Return messages

    internal const string SeriesWithZeroID = "SeriesID must be greater than 0";

    internal const string SeriesNotFoundWithSeriesID = "No Series entry for the given seriesID";

    internal const string SeriesNotFoundWithAnidbID = "No Series entry for the given anidbID";

    internal const string SeriesForbiddenForUser = "Accessing Series is not allowed for the current user";

    internal const string AnidbNotFoundForSeriesID = "No Series.AniDB entry for the given seriesID";

    internal const string AnidbNotFoundForAnidbID = "No Series.AniDB entry for the given anidbID";

    internal const string AnidbForbiddenForUser = "Accessing Series.AniDB is not allowed for the current user";

    internal const string TvdbNotFoundForSeriesID = "No Series.TvDB entry for the given seriesID";

    internal const string TvdbNotFoundForTvdbID = "No Series.TvDB entry for the given tvdbID";

    internal const string TvdbForbiddenForUser = "Accessing Series.TvDB is not allowed for the current user";

    #endregion

    #region Metadata

    #region Shoko

    /// <summary>
    /// Get a paginated list of all <see cref="Series"/> available to the current <see cref="User"/>.
    /// </summary>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <param name="startsWith">Search only for series with a main title that start with the given query.</param>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<ListResult<Series>> GetAllSeries(
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] string startsWith = "")
    {
        startsWith = startsWith.ToLowerInvariant();
        var user = User;
        return RepoFactory.AnimeSeries.GetAll()
            .Select(series => (series, seriesName: series.PreferredTitle.ToLowerInvariant()))
            .Where(tuple =>
            {
                var (series, seriesName) = tuple;
                if (!string.IsNullOrEmpty(startsWith) && !seriesName.StartsWith(startsWith))
                {
                    return false;
                }

                return user.AllowedSeries(series);
            })
            .OrderBy(a => a.seriesName)
            .ToListResult(tuple => new Series(tuple.series, user.JMMUserID), page, pageSize);
    }

    /// <summary>
    /// Get the series with ID
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <param name="randomImages">Randomize images shown for the <see cref="Series"/>.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("{seriesID}")]
    public ActionResult<Series> GetSeries([FromRoute] int seriesID, [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        return new Series(series, User.JMMUserID, randomImages, includeDataFrom);
    }

    /// <summary>
    /// Delete a Series
    /// </summary>
    /// <param name="seriesID">The ID of the Series</param>
    /// <param name="deleteFiles">Whether to delete all of the files in the series from the disk.</param>
    /// <param name="completelyRemove">Removes all records relating to the series. Use with caution, as you may get banned if it's abused.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("{seriesID}")]
    public async Task<ActionResult> DeleteSeries([FromRoute] int seriesID, [FromQuery] bool deleteFiles = false, [FromQuery] bool completelyRemove = false)
    {
        if (seriesID == 0)
            return BadRequest(SeriesWithZeroID);

        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        await _seriesService.DeleteSeries(series, deleteFiles, true, completelyRemove);

        return Ok();
    }

    /// <summary>
    /// Override the title of a Series
    /// </summary>
    /// <param name="seriesID">The ID of the Series</param>
    /// <param name="body"></param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("{seriesID}/OverrideTitle")]
    public ActionResult OverrideSeriesTitle([FromRoute] int seriesID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Series.Input.TitleOverrideBody body)
    {
        if (seriesID == 0)
            return BadRequest(SeriesWithZeroID);

        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        if (!string.Equals(series.SeriesNameOverride, body.Title))
        {
            series.SeriesNameOverride = body.Title;

            RepoFactory.AnimeSeries.Save(series);

            ShokoEventHandler.Instance.OnSeriesUpdated(series, UpdateReason.Updated);
        }

        return Ok();
    }

    /// <summary>
    /// Get the auto-matching settings for the series.
    /// </summary>
    /// <param name="seriesID"></param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("{seriesID}/AutoMatchSettings")]
    public ActionResult<Series.AutoMatchSettings> GetAutoMatchSettingsBySeriesID([FromRoute] int seriesID)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        return new Series.AutoMatchSettings(series);
    }

    /// <summary>
    /// Patch the auto-matching settings in the v3 model and merge it back into
    /// the database model.
    /// </summary>
    /// <param name="seriesID"></param>
    /// <param name="patchDocument"></param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPatch("{seriesID}/AutoMatchSettings")]
    public ActionResult<Series.AutoMatchSettings> PatchAutoMatchSettingsBySeriesID([FromRoute] int seriesID, [FromBody] JsonPatchDocument<Series.AutoMatchSettings> patchDocument)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        // Patch the settings in the v3 model and merge it back into the database
        // model.
        var autoMatchSettings = new Series.AutoMatchSettings(series);
        patchDocument.ApplyTo(autoMatchSettings, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return autoMatchSettings.MergeWithExisting(series);
    }

    /// <summary>
    /// Replace the auto-matching settings with the representation sent from the
    /// client.
    /// </summary>
    /// <param name="seriesID"></param>
    /// <param name="autoMatchSettings"></param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPut("{seriesID}/AutoMatchSettings")]
    public ActionResult<Series.AutoMatchSettings> PutAutoMatchSettingsBySeriesID([FromRoute] int seriesID, [FromBody] Series.AutoMatchSettings autoMatchSettings)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        return autoMatchSettings.MergeWithExisting(series);
    }

    /// <summary>
    /// Get all relations to series available in the local database for series with ID
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <returns></returns>
    [HttpGet("{seriesID}/Relations")]
    public ActionResult<List<SeriesRelation>> GetShokoRelationsBySeriesID([FromRoute] int seriesID)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        // TODO: Replace with a more generic implementation capable of supplying relations from more than just AniDB.
        return RepoFactory.AniDB_Anime_Relation.GetByAnimeID(series.AniDB_ID)
            .Select(relation =>
                (relation, relatedSeries: RepoFactory.AnimeSeries.GetByAnimeID(relation.RelatedAnimeID)))
            .Where(tuple => tuple.relatedSeries != null)
            .Select(tuple => new SeriesRelation(HttpContext, tuple.relation, series, tuple.relatedSeries))
            .ToList();
    }

    /// <summary>
    /// Get a paginated list of <see cref="Series"/> without local files, available to the current <see cref="User"/>.
    /// </summary>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <param name="search">An optional search query to filter series based on their titles.</param>
    /// <param name="fuzzy">Indicates that fuzzy-matching should be used for the search query.</param>
    /// <returns></returns>
    [HttpGet("WithoutFiles")]
    public ActionResult<ListResult<Series>> GetSeriesWithoutFiles(
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] string search = null,
        [FromQuery] bool fuzzy = true)
    {
        var user = User;
        var query = RepoFactory.AnimeSeries.GetAll()
            .Where(series => user.AllowedSeries(series) && series.VideoLocals.Count == 0);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var languages = SettingsProvider.GetSettings()
                .Language.SeriesTitleLanguageOrder
                .Select(lang => lang.GetTitleLanguage())
                .Concat(new TitleLanguage[] { TitleLanguage.English, TitleLanguage.Romaji })
                .ToHashSet();
            return query
                .Search(
                    search,
                    series => (series as IShokoSeries).Titles
                        .Where(title => languages.Contains(title.Language))
                        .Select(title => title.Title)
                        .Distinct()
                        .ToList(),
                    fuzzy
                )
                .ToListResult(searchResult => new Series(searchResult.Result, User.JMMUserID), page, pageSize);
        }
        return query
            .OrderBy(series => series.PreferredTitle.ToLowerInvariant())
            .ToListResult(series => new Series(series, User.JMMUserID), page, pageSize);
    }

    /// <summary>
    /// Get a paginated list of <see cref="Series"/> with manually linked local files, available to the current <see cref="User"/>.
    /// </summary>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <param name="search">An optional search query to filter series based on their titles.</param>
    /// <param name="fuzzy">Indicates that fuzzy-matching should be used for the search query.</param>
    /// <returns></returns>
    [HttpGet("WithManuallyLinkedFiles")]
    public ActionResult<ListResult<Series>> GetSeriesWithManuallyLinkedFiles(
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] string search = null,
        [FromQuery] bool fuzzy = true)
    {
        var user = User;
        var query = RepoFactory.AnimeSeries.GetAll()
            .Where(series => user.AllowedSeries(series) && _crossRefFileEpisode.GetByAnimeID(series.AniDB_ID).Where(a => a.VideoLocal != null)
                .Any(a => a.CrossRefSource == (int)CrossRefSource.User));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var languages = SettingsProvider.GetSettings()
                .Language.SeriesTitleLanguageOrder
                .Select(lang => lang.GetTitleLanguage())
                .Concat(new TitleLanguage[] { TitleLanguage.English, TitleLanguage.Romaji })
                .ToHashSet();
            return query
                .Search(
                    search,
                    series => (series as IShokoSeries).Titles
                        .Where(title => languages.Contains(title.Language))
                        .Select(title => title.Title)
                        .Distinct()
                        .ToList(),
                    fuzzy
                )
                .ToListResult(searchResult => new Series(searchResult.Result, User.JMMUserID), page, pageSize);
        }
        return query
            .OrderBy(series => series.PreferredTitle.ToLowerInvariant())
            .ToListResult(series => new Series(series, User.JMMUserID), page, pageSize);
    }

    #endregion

    #region AniDB

    /// <summary>
    /// Get a paginated list of all <see cref="Series.AniDB"/> available to the current <see cref="User"/>.
    /// </summary>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <param name="startsWith">Search only for anime with a main title that start with the given query.</param>
    /// <returns></returns>
    [HttpGet("AniDB")]
    public ActionResult<ListResult<Series.AniDB>> GetAllAnime([FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1, [FromQuery] string startsWith = "")
    {
        startsWith = startsWith.ToLowerInvariant();
        var user = User;
        return RepoFactory.AniDB_Anime.GetAll()
            .Select(anime => (anime, animeTitle: anime.PreferredTitle.ToLowerInvariant()))
            .Where(tuple =>
            {
                var (anime, animeTitle) = tuple;
                if (!string.IsNullOrEmpty(startsWith) && !animeTitle.StartsWith(startsWith))
                {
                    return false;
                }

                return user.AllowedAnime(anime);
            })
            .OrderBy(a => a.animeTitle)
            .ToListResult(tuple => new Series.AniDB(tuple.anime), page, pageSize);
    }

    /// <summary>
    /// Get a paginated list of all AniDB <see cref="SeriesRelation"/>s.
    /// </summary>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <returns></returns>
    [HttpGet("AniDB/Relations")]
    public ActionResult<ListResult<SeriesRelation>> GetAnidbRelations([FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        return RepoFactory.AniDB_Anime_Relation.GetAll()
            .OrderBy(a => a.AnimeID)
            .ThenBy(a => a.RelatedAnimeID)
            .ToListResult(relation => new SeriesRelation(HttpContext, relation), page, pageSize);
    }

    /// <summary>
    /// Get AniDB Info for series with ID
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <returns></returns>
    [HttpGet("{seriesID}/AniDB")]
    public ActionResult<Series.AniDB> GetSeriesAnidbBySeriesID([FromRoute] int seriesID)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        var anidb = series.AniDB_Anime;
        if (anidb == null)
        {
            return InternalError(AnidbNotFoundForSeriesID);
        }

        return new Series.AniDB(anidb, series);
    }

    /// <summary>
    /// Get all similar <see cref="Series.AniDB"/> entries for the <paramref name="seriesID"/>.
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <returns></returns>
    [HttpGet("{seriesID}/AniDB/Similar")]
    public ActionResult<List<Series.AniDB>> GetAnidbSimilarBySeriesID([FromRoute] int seriesID)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        var anidb = series.AniDB_Anime;
        if (anidb == null)
        {
            return InternalError(AnidbNotFoundForSeriesID);
        }

        return RepoFactory.AniDB_Anime_Similar.GetByAnimeID(anidb.AnimeID)
            .Select(similar => new Series.AniDB(similar))
            .ToList();
    }

    /// <summary>
    /// Get all similar <see cref="Series.AniDB"/> entries for the <paramref name="seriesID"/>.
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <returns></returns>
    [HttpGet("{seriesID}/AniDB/Related")]
    public ActionResult<List<Series.AniDB>> GetAnidbRelatedBySeriesID([FromRoute] int seriesID)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        var anidb = series.AniDB_Anime;
        if (anidb == null)
        {
            return InternalError(AnidbNotFoundForSeriesID);
        }

        return RepoFactory.AniDB_Anime_Relation.GetByAnimeID(anidb.AnimeID)
            .Select(relation => new Series.AniDB(relation))
            .ToList();
    }

    /// <summary>
    /// Get all AniDB relations for the <paramref name="seriesID"/>.
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <returns></returns>
    [HttpGet("{seriesID}/AniDB/Relations")]
    public ActionResult<List<SeriesRelation>> GetAnidbRelationsBySeriesID([FromRoute] int seriesID)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        var anidb = series.AniDB_Anime;
        if (anidb == null)
        {
            return InternalError(AnidbNotFoundForSeriesID);
        }

        return RepoFactory.AniDB_Anime_Relation.GetByAnimeID(anidb.AnimeID)
            .Select(relation => new SeriesRelation(HttpContext, relation, series))
            .ToList();
    }

    #region Recommended For You

    /// <summary>
    /// Gets anidb recommendation for the user.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="showAll">If enabled will show recommendations across all the anidb available in Shoko, if disabled will only show for the user's collection.</param>
    /// <param name="includeRestricted">Include restricted (H) series.</param>
    /// <param name="startDate">Start date to use if recommending for a watch period. Only setting the <paramref name="startDate"/> and not <paramref name="endDate"/> will result in using the watch history from the start date to the present date.</param>
    /// <param name="endDate">End date to use if recommending for a watch period.</param>
    /// <param name="approval">Minimum approval percentage for similar anime.</param>
    /// <returns></returns>
    [HttpGet("AniDB/RecommendedForYou")]
    public ActionResult<ListResult<Series.AniDBRecommendedForYou>> GetAnimeRecommendedForYou(
        [FromQuery, Range(0, 100)] int pageSize = 30,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool showAll = false,
        [FromQuery] bool includeRestricted = false,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery, Range(0, 1)] double? approval = null
    )
    {
        startDate = startDate?.ToLocalTime();
        endDate = endDate?.ToLocalTime();
        if (startDate.HasValue && !endDate.HasValue)
            endDate = DateTime.Now;

        if (endDate.HasValue && !startDate.HasValue)
            ModelState.AddModelError(nameof(startDate), "Missing start date.");

        if (startDate.HasValue)
        {
            if (endDate.Value > DateTime.Now)
                ModelState.AddModelError(nameof(endDate), "End date cannot be set into the future.");

            if (startDate.Value > endDate.Value)
                ModelState.AddModelError(nameof(startDate), "Start date cannot be newer than the end date.");
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var user = User;
        var watchedAnimeList = GetWatchedAnimeForPeriod(user, includeRestricted, startDate, endDate);
        var unwatchedAnimeDict = GetUnwatchedAnime(user, showAll,
            includeRestricted, !startDate.HasValue && !endDate.HasValue ? watchedAnimeList : null);
        return watchedAnimeList
            .SelectMany(anime =>
            {
                if (approval.HasValue)
                {
                    return anime.SimilarAnime.Where(similar =>
                        unwatchedAnimeDict.ContainsKey(similar.SimilarAnimeID) &&
                        (double)similar.Approval / similar.Total >= approval.Value);
                }

                return anime.SimilarAnime
                    .Where(similar => unwatchedAnimeDict.ContainsKey(similar.SimilarAnimeID));
            })
            .GroupBy(anime => anime.SimilarAnimeID)
            .Select(similarTo =>
            {
                var (anime, series) = unwatchedAnimeDict[similarTo.Key];
                var similarToCount = similarTo.Count();
                return new Series.AniDBRecommendedForYou(new Series.AniDB(anime, series), similarToCount);
            })
            .OrderByDescending(e => e.SimilarTo)
            .ToListResult(page, pageSize);
    }

    /// <summary>
    /// Get all watched anime in a given period of time for the <paramref name="user"/>.
    /// If the <paramref name="startDate"/> and <paramref name="endDate"/>
    /// is omitted then it will return all watched anime for the <paramref name="user"/>.
    /// </summary>
    /// <param name="user">The user to get the watched anime for.</param>
    /// <param name="includeRestricted">Include restricted (H) series.</param>
    /// <param name="startDate">The start date of the period.</param>
    /// <param name="endDate">The end date of the period.</param>
    /// <returns>The watched anime for the user.</returns>
    [NonAction]
    private static List<SVR_AniDB_Anime> GetWatchedAnimeForPeriod(
        SVR_JMMUser user,
        bool includeRestricted = false,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        startDate = startDate?.ToLocalTime();
        endDate = endDate?.ToLocalTime();
        IEnumerable<SVR_VideoLocal_User> userDataQuery = RepoFactory.VideoLocalUser.GetByUserID(user.JMMUserID);
        if (startDate.HasValue && endDate.HasValue)
        {
            userDataQuery = userDataQuery
                .Where(userData => userData.WatchedDate.HasValue && userData.WatchedDate.Value >= startDate.Value &&
                                   userData.WatchedDate.Value <= endDate.Value);
        }
        else
        {
            userDataQuery = userDataQuery
                .Where(userData => userData.WatchedDate.HasValue);
        }

        return userDataQuery
            .OrderByDescending(userData => userData.LastUpdated)
            .Select(userData => RepoFactory.VideoLocal.GetByID(userData.VideoLocalID))
            .Where(file => file != null)
            .Select(file => file.EpisodeCrossRefs.OrderBy(xref => xref.EpisodeOrder).ThenBy(xref => xref.Percentage)
                .FirstOrDefault())
            .Where(xref => xref != null)
            .Select(xref => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(xref.EpisodeID))
            .Where(episode => episode != null)
            .DistinctBy(episode => episode.AnimeSeriesID)
            .Select(episode => episode.AnimeSeries.AniDB_Anime)
            .Where(anime => user.AllowedAnime(anime) && (includeRestricted || anime.Restricted != 1))
            .ToList();
    }

    /// <summary>
    /// Get all unwatched anime for the user.
    /// </summary>
    /// <param name="user">The user to get the unwatched anime for.</param>
    /// <param name="showAll">If true will get a list of all available anime in shoko, regardless of if it's part of the user's collection or not.</param>
    /// <param name="includeRestricted">Include restricted (H) series.</param>
    /// <param name="watchedAnime">Optional. Re-use an existing list of the watched anime.</param>
    /// <returns>The unwatched anime for the user.</returns>
    [NonAction]
    private static Dictionary<int, (SVR_AniDB_Anime, SVR_AnimeSeries)> GetUnwatchedAnime(
        SVR_JMMUser user,
        bool showAll,
        bool includeRestricted = false,
        IEnumerable<SVR_AniDB_Anime> watchedAnime = null)
    {
        // Get all watched series (reuse if date is not set)
        var watchedSeriesSet = (watchedAnime ?? GetWatchedAnimeForPeriod(user))
            .Select(series => series.AnimeID)
            .ToHashSet();

        if (showAll)
        {
            return RepoFactory.AniDB_Anime.GetAll()
                .Where(anime => user.AllowedAnime(anime) && !watchedSeriesSet.Contains(anime.AnimeID) && (includeRestricted || anime.Restricted != 1))
                .ToDictionary<SVR_AniDB_Anime, int, (SVR_AniDB_Anime, SVR_AnimeSeries)>(anime => anime.AnimeID,
                    anime => (anime, null));
        }

        return RepoFactory.AnimeSeries.GetAll()
            .Where(series => user.AllowedSeries(series) && !watchedSeriesSet.Contains(series.AniDB_ID))
            .Select(series => (anime: series.AniDB_Anime, series))
            .Where(tuple => includeRestricted || tuple.anime.Restricted != 1)
            .ToDictionary(tuple => tuple.anime.AnimeID);
    }

    #endregion

    /// <summary>
    /// Get AniDB Info from the AniDB ID
    /// </summary>
    /// <param name="anidbID">AniDB ID</param>
    /// <returns></returns>
    [HttpGet("AniDB/{anidbID}")]
    public ActionResult<Series.AniDB> GetSeriesAnidbByAnidbID([FromRoute] int anidbID)
    {
        var anidb = RepoFactory.AniDB_Anime.GetByAnimeID(anidbID);
        if (anidb == null)
        {
            return NotFound(AnidbNotFoundForAnidbID);
        }

        if (!User.AllowedAnime(anidb))
        {
            return Forbid(AnidbForbiddenForUser);
        }

        return new Series.AniDB(anidb);
    }

    /// <summary>
    /// Get all similar <see cref="Series.AniDB"/> entries for the <paramref name="anidbID"/>.
    /// </summary>
    /// <param name="anidbID">AniDB ID</param>
    /// <returns></returns>
    [HttpGet("AniDB/{anidbID}/Similar")]
    public ActionResult<List<Series.AniDB>> GetAnidbSimilarByAnidbID([FromRoute] int anidbID)
    {
        var anidb = RepoFactory.AniDB_Anime.GetByAnimeID(anidbID);
        if (anidb == null)
        {
            return NotFound(AnidbNotFoundForAnidbID);
        }

        if (!User.AllowedAnime(anidb))
        {
            return Forbid(AnidbForbiddenForUser);
        }

        return RepoFactory.AniDB_Anime_Similar.GetByAnimeID(anidbID)
            .Select(similar => new Series.AniDB(similar))
            .ToList();
    }

    /// <summary>
    /// Get all related <see cref="Series.AniDB"/> entries for the <paramref name="anidbID"/>.
    /// </summary>
    /// <param name="anidbID">AniDB ID</param>
    /// <returns></returns>
    [HttpGet("AniDB/{anidbID}/Related")]
    public ActionResult<List<Series.AniDB>> GetAnidbRelatedByAnidbID([FromRoute] int anidbID)
    {
        var anidb = RepoFactory.AniDB_Anime.GetByAnimeID(anidbID);
        if (anidb == null)
        {
            return NotFound(AnidbNotFoundForAnidbID);
        }

        if (!User.AllowedAnime(anidb))
        {
            return Forbid(AnidbForbiddenForUser);
        }

        return RepoFactory.AniDB_Anime_Relation.GetByAnimeID(anidbID)
            .Select(relation => new Series.AniDB(relation))
            .ToList();
    }

    /// <summary>
    /// Get all anidb relations for the <paramref name="anidbID"/>.
    /// </summary>
    /// <param name="anidbID">AniDB ID</param>
    /// <returns></returns>
    [HttpGet("AniDB/{anidbID}/Relations")]
    public ActionResult<List<SeriesRelation>> GetAnidbRelationsByAnidbID([FromRoute] int anidbID)
    {
        var anidb = RepoFactory.AniDB_Anime.GetByAnimeID(anidbID);
        if (anidb == null)
        {
            return NotFound(AnidbNotFoundForAnidbID);
        }

        if (!User.AllowedAnime(anidb))
        {
            return Forbid(AnidbForbiddenForUser);
        }

        return RepoFactory.AniDB_Anime_Relation.GetByAnimeID(anidbID)
            .Select(relation => new SeriesRelation(HttpContext, relation))
            .ToList();
    }

    /// <summary>
    /// Get a Series from the AniDB ID
    /// </summary>
    /// <param name="anidbID">AniDB ID</param>
    /// <param name="randomImages">Randomize images shown for the <see cref="Series"/>.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("AniDB/{anidbID}/Series")]
    public ActionResult<Series> GetSeriesByAnidbID([FromRoute] int anidbID, [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        var series = RepoFactory.AnimeSeries.GetByAnimeID(anidbID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithAnidbID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        return new Series(series, User.JMMUserID, randomImages, includeDataFrom);
    }

    /// <summary>
    /// Queue a refresh of the AniDB Info for series with AniDB ID
    /// </summary>
    /// <param name="anidbID">AniDB ID</param>
    /// <param name="force">Try to forcefully retrieve updated data from AniDB if
    /// we're not banned and if the the last update is outside the no-update
    /// window (configured in the settings).</param>
    /// <param name="downloadRelations">Download relations for the series</param>
    /// <param name="createSeriesEntry">Also create the Series entries if
    /// it/they do not exist</param>
    /// <param name="immediate">Try to immediately refresh the data if we're
    /// not HTTP banned.</param>
    /// <param name="cacheOnly">Only used data from the cache when performing the refresh. <paramref name="force"/> takes precedence over this option.</param>
    /// <returns>True if the refresh was performed at once, otherwise false if it was queued.</returns>
    [HttpPost("AniDB/{anidbID}/Refresh")]
    public async Task<ActionResult<bool>> RefreshAniDBByAniDBID([FromRoute] int anidbID, [FromQuery] bool force = false,
        [FromQuery] bool downloadRelations = false, [FromQuery] bool? createSeriesEntry = null,
        [FromQuery] bool immediate = false, [FromQuery] bool cacheOnly = false)
    {
        if (!createSeriesEntry.HasValue)
        {
            var settings = SettingsProvider.GetSettings();
            createSeriesEntry = settings.AniDb.AutomaticallyImportSeries;
        }

        // TODO No
        return await _seriesService.QueueAniDBRefresh(anidbID, force, downloadRelations,
            createSeriesEntry.Value, immediate, cacheOnly);
    }

    /// <summary>
    /// Queue a refresh of the AniDB Info for series with ID
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <param name="force">Try to forcefully retrieve updated data from AniDB if
    /// we're not banned and if the the last update is outside the no-update
    /// window (configured in the settings).</param>
    /// <param name="downloadRelations">Download relations for the series</param>
    /// <param name="createSeriesEntry">Also create the Series entries if
    /// it/they do not exist</param>
    /// <param name="immediate">Try to immediately refresh the data if we're
    /// not HTTP banned.</param>
    /// <param name="cacheOnly">Only used data from the cache when performing the refresh. <paramref name="force"/> takes precedence over this option.</param>
    /// <returns>True if the refresh is done, otherwise false if it was queued.</returns>
    [HttpPost("{seriesID}/AniDB/Refresh")]
    public async Task<ActionResult<bool>> RefreshAniDBBySeriesID([FromRoute] int seriesID, [FromQuery] bool force = false,
        [FromQuery] bool downloadRelations = false, [FromQuery] bool? createSeriesEntry = null,
        [FromQuery] bool immediate = false, [FromQuery] bool cacheOnly = false)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        if (!createSeriesEntry.HasValue)
        {
            var settings = SettingsProvider.GetSettings();
            createSeriesEntry = settings.AniDb.AutomaticallyImportSeries;
        }

        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        var anidb = series.AniDB_Anime;
        if (anidb == null)
        {
            return InternalError(AnidbNotFoundForSeriesID);
        }

        // TODO No
        return await _seriesService.QueueAniDBRefresh(anidb.AnimeID, force, downloadRelations,
            createSeriesEntry.Value, immediate, cacheOnly);
    }

    /// <summary>
    /// Forcefully refresh the AniDB Info from XML on disk for series with ID
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <returns>True if the refresh is done, otherwise false if it failed.</returns>
    [HttpPost("{seriesID}/AniDB/Refresh/ForceFromXML")]
    [Obsolete("Use Refresh with cacheOnly set to true")]
    public async Task<ActionResult<bool>> RefreshAniDBFromXML([FromRoute] int seriesID)
        => await RefreshAniDBBySeriesID(seriesID, false, false, true, true, true);

    #endregion

    #region TvDB

    /// <summary>
    /// Get TvDB Info for series with ID
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <returns></returns>
    [HttpGet("{seriesID}/TvDB")]
    public ActionResult<List<Series.TvDB>> GetSeriesTvdb([FromRoute] int seriesID)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(TvdbNotFoundForSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(TvdbForbiddenForUser);
        }

        var allEpisodes = series.AllAnimeEpisodes;
        return series.TvDBSeries
            .Select(tvdb => new Series.TvDB(tvdb, allEpisodes))
            .ToList();
    }

    /// <summary>
    /// Add a TvDB link to a series.
    /// </summary>
    /// <param name="seriesID">Series ID</param>
    /// <param name="body">Body containing the information about the link to be made</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("{seriesID}/TvDB")]
    public async Task<ActionResult> LinkTvDB([FromRoute] int seriesID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Series.Input.LinkCommonBody body)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(TvdbNotFoundForSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(TvdbForbiddenForUser);

        var tvdbHelper = Utils.ServiceContainer.GetService<TvDBApiHelper>();
        await tvdbHelper.LinkAniDBTvDB(series.AniDB_ID, body.ID, !body.Replace);

        return Ok();
    }

    /// <summary>
    /// Remove one or all TvDB links from a series.
    /// </summary>
    /// <param name="seriesID">Series ID</param>
    /// <param name="body">Optional. Body containing information about the link to be removed</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("{seriesID}/TvDB")]
    public ActionResult UnlinkTvDB([FromRoute] int seriesID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] Series.Input.UnlinkCommonBody body)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(TvdbNotFoundForSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(TvdbForbiddenForUser);

        var tvdbHelper = Utils.ServiceContainer.GetService<TvDBApiHelper>();
        if (body != null && body.ID > 0)
            tvdbHelper.RemoveLinkAniDBTvDB(series.AniDB_ID, body.ID);
        else
            foreach (var xref in RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(series.AniDB_ID))
                tvdbHelper.RemoveLinkAniDBTvDB(series.AniDB_ID, xref.TvDBID);

        return Ok();
    }

    /// <summary>
    /// Queue a refresh of the all the <see cref="Series.TvDB"/> linked to the
    /// <see cref="Series"/> using the <paramref name="seriesID"/>.
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <param name="force">Forcefully retrieve updated data from TvDB</param>
    /// <returns></returns>
    [HttpPost("{seriesID}/TvDB/Refresh")]
    public async Task<ActionResult> RefreshSeriesTvdbBySeriesID([FromRoute] int seriesID, [FromQuery] bool force = false)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(TvdbNotFoundForSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(TvdbForbiddenForUser);
        }

        var tvSeriesList = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(series.AniDB_ID);
        // TODO No
        foreach (var crossRef in tvSeriesList)
            await _seriesService.QueueTvDBRefresh(crossRef.TvDBID, force);

        return Ok();
    }

    /// <summary>
    /// Get TvDB Info from the TvDB ID
    /// </summary>
    /// <param name="tvdbID">TvDB ID</param>
    /// <returns></returns>
    [HttpGet("TvDB/{tvdbID}")]
    public ActionResult<Series.TvDB> GetSeriesTvdbByTvdbID([FromRoute] int tvdbID)
    {
        var tvdb = RepoFactory.TvDB_Series.GetByTvDBID(tvdbID);
        if (tvdb == null)
        {
            return NotFound(TvdbNotFoundForTvdbID);
        }

        var xref = RepoFactory.CrossRef_AniDB_TvDB.GetByTvDBID(tvdbID).FirstOrDefault();
        if (xref == null)
        {
            return NotFound(TvdbNotFoundForTvdbID);
        }

        var series = RepoFactory.AnimeSeries.GetByAnimeID(xref.AniDBID);
        if (series == null)
        {
            return NotFound(TvdbNotFoundForTvdbID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(TvdbForbiddenForUser);
        }

        return new Series.TvDB(tvdb, series.AllAnimeEpisodes);
    }

    /// <summary>
    /// Directly queue a refresh of the the <see cref="Series.TvDB"/> data using
    /// the <paramref name="tvdbID"/>.
    /// </summary>
    /// <param name="tvdbID">TvDB ID</param>
    /// <param name="force">Forcefully retrieve updated data from TvDB</param>
    /// <param name="immediate">Try to immediately refresh the data.</param>
    /// <returns></returns>
    [HttpPost("TvDB/{tvdbID}/Refresh")]
    public async Task<ActionResult<bool>> RefreshSeriesTvdbByTvdbId([FromRoute] int tvdbID, [FromQuery] bool force = false, [FromQuery] bool immediate = false)
    {
        return await _seriesService.QueueTvDBRefresh(tvdbID, force, immediate);
    }

    /// <summary>
    /// Get a Series from the TvDB ID
    /// </summary>
    /// <param name="tvdbID">TvDB ID</param>
    /// <returns></returns>
    [HttpGet("TvDB/{tvdbID}/Series")]
    public ActionResult<List<Series>> GetSeriesByTvdbID([FromRoute] int tvdbID)
    {
        var tvdb = RepoFactory.TvDB_Series.GetByTvDBID(tvdbID);
        if (tvdb == null)
        {
            return NotFound(TvdbNotFoundForTvdbID);
        }

        var seriesList = RepoFactory.CrossRef_AniDB_TvDB.GetByTvDBID(tvdbID)
            .Select(xref => RepoFactory.AnimeSeries.GetByAnimeID(xref.AniDBID))
            .Where(series => series != null)
            .ToList();

        var user = User;
        if (seriesList.Any(series => !user.AllowedSeries(series)))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        return seriesList
            .Select(series => new Series(series, User.JMMUserID))
            .ToList();
    }

    #endregion

    #region TMDB

    /// <summary>
    /// Automagically search for one or more matches for the Shoko Series by ID.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <param name="force">Forcefully update the metadata of the matched entities.</param>
    /// <returns>Void.</returns>
    [HttpPost("{seriesID}/TMDB/Action/AutoSearch")]
    public async Task<ActionResult> AutoMatchTMDBMoviesBySeriesID(
        [FromRoute] int seriesID,
        [FromQuery] bool force = false
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        await _tmdbService.ScheduleSearchForMatch(series.AniDB_ID, force);

        return NoContent();
    }

    #region Movie

    /// <summary>
    /// Get all TMDB Movies linked to the Shoko Series by ID.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <param name="include">Extra details to include.</param>
    /// <param name="language">Language to fetch some details in. Omitting will fetch all languages.</param>
    /// <returns>All TMDB Movies linked to the Shoko Series.</returns>
    [HttpGet("{seriesID}/TMDB/Movie")]
    public ActionResult<List<TmdbMovie>> GetTMDBMoviesBySeriesID(
        [FromRoute] int seriesID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbMovie.IncludeDetails> include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage> language = null
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        return series.TmdbMovieCrossReferences
            .Select(o => o.TmdbMovie)
            .OfType<TMDB_Movie>()
            .Select(tmdbMovie => new TmdbMovie(tmdbMovie, include?.CombineFlags(), language))
            .ToList();
    }

    /// <summary>
    /// Add a new TMDB Movie cross-reference to the Shoko Series by ID.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <param name="body">Body containing the information about the new cross-reference to be made.</param>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpPost("{seriesID}/TMDB/Movie")]
    public async Task<ActionResult> AddLinkToTMDBMoviesBySeriesID(
        [FromRoute] int seriesID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Series.Input.LinkMovieBody body
    )
    {
        if (body.ID <= 0)
        {
            ModelState.AddModelError(nameof(body.ID), "The provider ID cannot be zero or a negative value.");
            return ValidationProblem(ModelState);
        }

        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        await _tmdbService.AddMovieLink(series.AniDB_ID, body.ID, body.EpisodeID, additiveLink: !body.Replace);

        var needRefresh = RepoFactory.TMDB_Movie.GetByTmdbMovieID(body.ID) != null || body.Refresh;
        if (needRefresh)
            await _tmdbService.ScheduleUpdateOfMovie(body.ID, forceRefresh: body.Refresh, downloadImages: true);

        return NoContent();
    }

    /// <summary>
    /// Remove one or all TMDB Movie links from the series.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <param name="body">Optional. Body containing information about the link to be removed.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("{seriesID}/TMDB/Movie")]
    public async Task<ActionResult> RemoveLinkToTMDBMoviesBySeriesID(
        [FromRoute] int seriesID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] Series.Input.UnlinkCommonBody body
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        if (body != null && body.ID > 0)
            await _tmdbService.RemoveMovieLink(series.AniDB_ID, body.ID, body.Purge);
        else
            await _tmdbService.RemoveAllMovieLinks(series.AniDB_ID, body.Purge);

        return NoContent();
    }

    /// <summary>
    /// Refresh all TMDB Movies linked to the Shoko Series by ID.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <param name="force">Forcefully download an update even if we updated recently.</param>
    /// <param name="downloadImages">Also download images.</param>
    /// <param name="downloadCrewAndCast">Also download crew and cast. Will respect global option if not set.</param>
    /// <param name="downloadCollections">Also download movie collections. Will respect the global option if not set.</param>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpPost("{seriesID}/TMDB/Movie/Action/Refresh")]
    public async Task<ActionResult> RefreshTMDBMoviesBySeriesID(
        [FromRoute] int seriesID,
        [FromQuery] bool force = false,
        [FromQuery] bool downloadImages = true,
        [FromQuery] bool? downloadCrewAndCast = null,
        [FromQuery] bool? downloadCollections = null
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        foreach (var xref in RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID(series.AniDB_ID))
            await _tmdbService.ScheduleUpdateOfMovie(xref.TmdbMovieID, force, downloadImages, downloadCrewAndCast, downloadCollections);

        return NoContent();
    }

    /// <summary>
    /// Get all TMDB Movie cross-references for the Shoko Series by ID.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <returns>All TMDB Movie cross-references for the Shoko Series.</returns>
    [HttpGet("{seriesID}/TMDB/Movie/CrossReferences")]
    public ActionResult<IReadOnlyList<TmdbMovie.CrossReference>> GetTMDBMovieCrossReferenceBySeriesID(
        [FromRoute] int seriesID
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        return series.TmdbMovieCrossReferences
            .Select(xref => new TmdbMovie.CrossReference(xref))
            .OrderBy(xref => xref.TmdbMovieID)
            .ToList();
    }

    #endregion

    #region Show

    /// <summary>
    /// Get all TMDB Shows linked to the Shoko Series by ID.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <param name="include">Extra details to include.</param>
    /// <param name="language">Language to fetch some details in. Omitting will fetch all languages.</param>
    /// <returns></returns>
    [HttpGet("{seriesID}/TMDB/Show")]
    public ActionResult<List<TmdbShow>> GetTMDBShowsBySeriesID(
        [FromRoute] int seriesID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbShow.IncludeDetails> include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage> language = null
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        return series.TmdbShowCrossReferences
            .Select(o => o.TmdbShow)
            .OfType<TMDB_Show>()
            .Select(o => new TmdbShow(o, include?.CombineFlags(), language))
            .ToList();
    }

    /// <summary>
    /// Add a new TMDB Show cross-reference to the Shoko Series by ID.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <param name="body">Body containing the information about the new cross-reference to be made.</param>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpPost("{seriesID}/TMDB/Show")]
    public async Task<ActionResult> AddLinkToTMDBShowsBySeriesID(
        [FromRoute] int seriesID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Series.Input.LinkShowBody body
    )
    {
        if (body.ID <= 0)
        {
            ModelState.AddModelError(nameof(body.ID), "The provider ID cannot be zero or a negative value.");
            return ValidationProblem(ModelState);
        }

        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        await _tmdbService.AddShowLink(series.AniDB_ID, body.ID, additiveLink: !body.Replace);

        var needRefresh = RepoFactory.TMDB_Show.GetByTmdbShowID(body.ID) != null || body.Refresh;
        if (needRefresh)
            await _tmdbService.ScheduleUpdateOfShow(body.ID, forceRefresh: body.Refresh, downloadImages: true);

        return NoContent();
    }

    /// <summary>
    /// Remove one or all TMDB Show cross-reference(s) for the Shoko Series by ID.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <param name="body">Optional. The unlink body with the details about the TMDB Show to remove.</param>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpDelete("{seriesID}/TMDB/Show")]
    public async Task<ActionResult> RemoveLinkToTMDBShowsBySeriesID(
        [FromRoute] int seriesID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] Series.Input.UnlinkCommonBody body
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        if (body != null && body.ID > 0)
            await _tmdbService.RemoveShowLink(series.AniDB_ID, body.ID, body.Purge);
        else
            await _tmdbService.RemoveAllShowLinks(series.AniDB_ID, body.Purge);

        return NoContent();
    }

    /// <summary>
    /// Refresh all TMDB Shows linked to the Shoko Series by ID.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <param name="force">Forcefully refresh the shows, even if they've been recently updated.</param>
    /// <param name="downloadImages">Also download images.</param>
    /// /// <param name="downloadCrewAndCast">Also download crew and cast. Will respect global options if not set.</param>
    /// <param name="downloadAlternateOrdering">Also download alternate ordering information. Will respect global options if not set.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("{seriesID}/TMDB/Show/Action/Refresh")]
    public async Task<ActionResult> RefreshTMDBShowsBySeriesID(
        [FromRoute] int seriesID,
        [FromQuery] bool force = false,
        [FromQuery] bool downloadImages = true,
        [FromQuery] bool? downloadCrewAndCast = null,
        [FromQuery] bool? downloadAlternateOrdering = null
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        foreach (var xref in RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID(series.AniDB_ID))
            await _tmdbService.ScheduleUpdateOfShow(xref.TmdbShowID, force, downloadImages, downloadCrewAndCast, downloadAlternateOrdering);

        return NoContent();
    }

    /// <summary>
    /// Get all TMDB Show cross-references for the Shoko Series by ID.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <returns>All TMDB Show cross-references for the Shoko Series.</returns>
    [HttpGet("{seriesID}/TMDB/Show/CrossReferences")]
    public ActionResult<IReadOnlyList<TmdbShow.CrossReference>> GetTMDBShowCrossReferenceBySeriesID(
        [FromRoute] int seriesID
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        return series.TmdbShowCrossReferences
            .Select(xref => new TmdbShow.CrossReference(xref))
            .OrderBy(xref => xref.TmdbShowID)
            .ToList();
    }

    #region Episode Cross-references

    /// <summary>
    /// Shows all existing episode mappings for a Shoko Series. Optionally
    /// allows filtering it to a specific TMDB show.
    /// </summary>
    /// <param name="seriesID">The Shoko Series ID.</param>
    /// <param name="tmdbShowID">The TMDB Show ID to filter the episode mappings. If not specified, mappings for any show may be included.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <returns>A list of TMDB episode cross-references as part of the preview result, based on the provided filtering and pagination settings.</returns>
    [HttpGet("{seriesID}/TMDB/Show/CrossReferences/Episode")]
    public ActionResult<ListResult<TmdbEpisode.CrossReference>> GetTMDBEpisodeMappingsBySeriesID(
        [FromRoute] int seriesID,
        [FromQuery] int? tmdbShowID,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(TvdbNotFoundForSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(TvdbForbiddenForUser);

        if (tmdbShowID.HasValue)
        {
            var xrefs = series.TmdbShowCrossReferences;
            var xref = xrefs.FirstOrDefault(s => s.TmdbShowID == tmdbShowID.Value);
            if (xref == null)
                return ValidationProblem("Unable to find an existing cross-reference for the given TMDB Show ID. Please first link the TMDB Show to the Shoko Series.", "tmdbShowID");
        }

        return series.GetTmdbEpisodeCrossReferences(tmdbShowID)
            .ToListResult(x => new TmdbEpisode.CrossReference(x), page, pageSize);
    }

    /// <summary>
    /// Modifies the existing episode mappings by resetting, replacing, adding,
    /// or removing links between Shoko episodes and TMDB episodes of any TMDB
    /// shows linked to the Shoko series.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <param name="body">The payload containing the operations to be applied, detailing which mappings to reset, replace, add, or remove.</param>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpPost("{seriesID}/TMDB/Show/CrossReferences/Episode")]
    public async Task<ActionResult> OverrideTMDBEpisodeMappingsBySeriesID(
        [FromRoute] int seriesID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Series.Input.OverrideEpisodeMappingBody body
    )
    {
        if (body == null || (body.Mapping.Count == 0 && !body.ResetAll))
            return ValidationProblem("Empty body.");

        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(TvdbNotFoundForSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(TvdbForbiddenForUser);

        // Validate the mappings.
        var xrefs = series.TmdbShowCrossReferences;
        var showIDs = xrefs
            .Select(xref => xref.TmdbShowID)
            .ToHashSet();
        var missingIDs = new HashSet<int>();
        foreach (var link in body.Mapping)
        {
            var shokoEpisode = RepoFactory.AnimeEpisode.GetByID(link.ShokoID);
            if (shokoEpisode == null)
            {
                ModelState.AddModelError("Mapping", $"Unable to find a Shoko Episode with id '{link.ShokoID}'");
                continue;
            }
            if (shokoEpisode.AnimeSeriesID != series.AnimeSeriesID)
            {
                ModelState.AddModelError("Mapping", $"The Shoko Episode with id '{link.ShokoID}' is not part of the series.");
                continue;
            }

            var tmdbEpisode = link.TmdbID == 0 ? null : RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(link.TmdbID);
            if (link.TmdbID != 0 && tmdbEpisode == null)
            {
                ModelState.AddModelError("Mapping", $"Unable to find TMDB Episode with the id '{link.TmdbID}' locally.");
                continue;
            }
            if (link.TmdbID != 0 && !showIDs.Contains(tmdbEpisode.TmdbShowID))
                missingIDs.Add(tmdbEpisode.TmdbShowID);

            link.AnidbID = shokoEpisode.AniDB_EpisodeID;
        }
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Add any missing links if needed.
        foreach (var showId in missingIDs)
            await _tmdbService.AddShowLink(series.AniDB_ID, showId, additiveLink: true);

        // Reset the existing links if we wanted to replace all.
        if (body.ResetAll)
            _tmdbService.ResetAllEpisodeLinks(series.AniDB_ID);

        // Do the actual linking.
        foreach (var link in body.Mapping)
            _tmdbService.SetEpisodeLink(link.AnidbID, link.TmdbID, !link.Replace);

        return NoContent();
    }

    /// <summary>
    /// Preview the automagically matched Shoko episodes with the specified TMDB
    /// show and/or season. If no season is specified, the operation applies to
    /// any season of either the selected show or the first show already linked.
    /// This endpoint allows for replacing all existing links or adding links to
    /// episodes that currently lack any.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <param name="tmdbShowID">The specified TMDB Show ID to search for links. This parameter is used to select a specific show.</param>
    /// <param name="tmdbSeasonID">The specified TMDB Season ID to search for links. If not provided, links are searched for any season of the selected or first linked show.</param>
    /// <param name="keepExisting">Determines whether to retain any and all existing links.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <returns>A preview of the automagically matched episodes.</returns>
    [Authorize("admin")]
    [HttpGet("{seriesID}/TMDB/Show/CrossReferences/Episode/Auto")]
    public ActionResult<ListResult<TmdbEpisode.CrossReference>> PreviewAutoTMDBEpisodeMappingsBySeriesID(
        [FromRoute] int seriesID,
        [FromQuery] int? tmdbShowID,
        [FromQuery] int? tmdbSeasonID,
        [FromQuery] bool keepExisting = true,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(TvdbNotFoundForSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(TvdbForbiddenForUser);

        if (!tmdbShowID.HasValue)
        {
            var xrefs = series.TmdbShowCrossReferences;
            var xref = xrefs.Count > 0 ? xrefs[0] : null;
            if (xref == null)
                return ValidationProblem("Unable to find an existing cross-reference for the series to use. Make sure at least one TMDB Show is linked to the Shoko Series.", "tmdbShowID");

            tmdbShowID = xref.TmdbShowID;
        }

        if (tmdbSeasonID.HasValue)
        {
            var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(tmdbSeasonID.Value);
            if (season == null)
                return ValidationProblem("Unable to find existing TMDB Season with the given season ID.", "tmdbSeasonID");

            if (season.TmdbShowID != tmdbShowID.Value)
                return ValidationProblem("The selected tmdbSeasonID does not belong to the selected tmdbShowID", "tmdbSeasonID");
        }

        return _tmdbService.MatchAnidbToTmdbEpisodes(series.AniDB_ID, tmdbShowID.Value, tmdbSeasonID, keepExisting, saveToDatabase: false)
            .ToListResult(x => new TmdbEpisode.CrossReference(x), page, pageSize);
    }

    /// <summary>
    /// Automagically matches Shoko episodes with the specified TMDB show and/or
    /// season. If no season is specified, the operation applies to any season
    /// of either the selected show or the first show already linked. This
    /// endpoint allows for replacing all existing links or adding links to
    /// episodes that currently lack any.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <param name="tmdbShowID">The specified TMDB Show ID to search for links. This parameter is used to select a specific show.</param>
    /// <param name="tmdbSeasonID">The specified TMDB Season ID to search for links. If not provided, links are searched for any season of the selected or first linked show.</param>
    /// <param name="keepExisting">Determines whether to retain any and all existing links.</param>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpPost("{seriesID}/TMDB/Show/CrossReferences/Episode/Auto")]
    public async Task<ActionResult> AutoTMDBEpisodeMappingsBySeriesID(
        [FromRoute] int seriesID,
        [FromQuery] int? tmdbShowID,
        [FromQuery] int? tmdbSeasonID,
        [FromQuery] bool keepExisting = true
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(TvdbNotFoundForSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(TvdbForbiddenForUser);

        var isMissing = false;
        var xrefs = series.TmdbShowCrossReferences;
        if (tmdbShowID.HasValue)
        {
            isMissing = xrefs.Any(s => s.TmdbShowID == tmdbShowID.Value);
        }
        else
        {
            var xref = xrefs.Count > 0 ? xrefs[0] : null;
            if (xref == null)
                return ValidationProblem("Unable to find an existing cross-reference for the series to use. Make sure at least one TMDB Show is linked to the Shoko Series.", "tmdbShowID");

            tmdbShowID = xref.TmdbShowID;
        }

        // Hard bail if the TMDB show isn't locally available.
        if (RepoFactory.TMDB_Show.GetByTmdbShowID(tmdbShowID.Value) == null)
            return ValidationProblem("Unable to find the selected TMDB Show locally. Add the TMDB Show locally first.", "tmdbShowID");

        // Add the missing link if needed.
        if (isMissing)
            await _tmdbService.AddShowLink(series.AniDB_ID, tmdbShowID.Value, additiveLink: true);

        if (tmdbSeasonID.HasValue)
        {
            var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(tmdbSeasonID.Value);
            if (season == null)
                return ValidationProblem("Unable to find existing TMDB Season with the given season ID.", "tmdbSeasonID");

            if (season.TmdbShowID != tmdbShowID.Value)
                return ValidationProblem("The selected tmdbSeasonID does not belong to the selected tmdbShowID", "tmdbSeasonID");
        }

        _tmdbService.MatchAnidbToTmdbEpisodes(series.AniDB_ID, tmdbShowID.Value, tmdbSeasonID, keepExisting, saveToDatabase: true);

        return NoContent();
    }

    /// <summary>
    /// Reset all existing episode mappings for the shoko series.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpDelete("{seriesID}/TMDB/Show/CrossReferences/Episode")]
    public ActionResult RemoveTMDBEpisodeMappingsBySeriesID(
        [FromRoute] int seriesID
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(TvdbNotFoundForSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(TvdbForbiddenForUser);

        _tmdbService.ResetAllEpisodeLinks(series.AniDB_ID);

        return NoContent();
    }

    #endregion

    #endregion

    #region Season

    /// <summary>
    /// Get all TMDB Season indirectly linked to the Shoko Series by ID.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <param name="include">Extra details to include.</param>
    /// <param name="language">Language to fetch some details in.</param>
    /// <returns>All TMDB Seasons indirectly linked to the Shoko Series.</returns>
    [HttpGet("{seriesID}/TMDB/Season")]
    public ActionResult<List<TmdbSeason>> GetTMDBSeasonsBySeriesID(
        [FromRoute] int seriesID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbSeason.IncludeDetails> include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage> language = null
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(TvdbNotFoundForSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(TvdbForbiddenForUser);

        return series.TmdbEpisodeCrossReferences
            .Select(o => o.TmdbEpisode)
            .OfType<TMDB_Episode>()
            .DistinctBy(o => o.TmdbSeasonID)
            .Select(o => o.TmdbSeason)
            .OfType<TMDB_Season>()
            .OrderBy(season => season.TmdbShowID)
            .ThenBy(season => season.SeasonNumber)
            .Select(o => new TmdbSeason(o, include?.CombineFlags(), language))
            .ToList();
    }

    #endregion

    #endregion

    #endregion

    #region Episode

    /// <summary>
    /// Get the <see cref="Episode"/>s for the <see cref="Series"/> with <paramref name="seriesID"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Filter"/> or <see cref="Group"/> is irrelevant at this level.
    /// </remarks>
    /// <param name="seriesID">Series ID</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="includeMissing">Include missing episodes in the list.</param>
    /// <param name="includeHidden">Include hidden episodes in the list.</param>
    /// <param name="includeWatched">Include watched episodes in the list.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="type">Filter episodes by the specified <see cref="EpisodeType"/>s.</param>
    /// <param name="includeFiles">Include files with the episodes.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeAbsolutePaths">Include absolute paths for the file locations.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
    /// <param name="search">An optional search query to filter episodes based on their titles.</param>
    /// <param name="fuzzy">Indicates that fuzzy-matching should be used for the search query.</param>
    /// <returns>A list of episodes based on the specified filters.</returns>
    [HttpGet("{seriesID}/Episode")]
    public ActionResult<ListResult<Episode>> GetEpisodes(
        [FromRoute] int seriesID,
        [FromQuery, Range(0, 1000)] int pageSize = 20,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] IncludeOnlyFilter includeMissing = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeHidden = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeWatched = IncludeOnlyFilter.True,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeType> type = null,
        [FromQuery] bool includeFiles = false,
        [FromQuery] bool includeMediaInfo = false,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] string search = null,
        [FromQuery] bool fuzzy = true
    )
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        return GetEpisodesInternal(series, includeMissing, includeHidden, includeWatched, type, search, fuzzy)
            .ToListResult(a => new Episode(HttpContext, a, includeDataFrom, includeFiles, includeMediaInfo, includeAbsolutePaths, includeXRefs), page, pageSize);
    }

    /// <summary>
    /// Toggles the watched state for all the episodes that fit the query.
    /// </summary>
    /// <param name="seriesID">Series ID</param>
    /// <param name="value">The new watched state.</param>
    /// <param name="includeMissing">Include missing episodes in the list.</param>
    /// <param name="includeHidden">Include hidden episodes in the list.</param>
    /// <param name="includeWatched">Include watched episodes in the list.</param>
    /// <param name="type">Filter episodes by the specified <see cref="EpisodeType"/>s.</param>
    /// <param name="search">An optional search query to filter episodes based on their titles.</param>
    /// <param name="fuzzy">Indicates that fuzzy-matching should be used for the search query.</param>
    /// <returns></returns>
    [HttpPost("{seriesID}/Episode/Watched")]
    public async Task<ActionResult> MarkSeriesWatched(
        [FromRoute] int seriesID,
        [FromQuery] bool value = true,
        [FromQuery] IncludeOnlyFilter includeMissing = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeHidden = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeWatched = IncludeOnlyFilter.True,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeType> type = null,
        [FromQuery] string search = null,
        [FromQuery] bool fuzzy = true)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        var userId = User.JMMUserID;
        var now = DateTime.Now;
        // this has a parallel query to evaluate filters and data in parallel, but that makes awaiting the SetWatchedStatus calls more difficult, so we ToList() it
        await Task.WhenAll(GetEpisodesInternal(series, includeMissing, includeHidden, includeWatched, type, search, fuzzy).ToList()
            .Select(episode => _watchedService.SetWatchedStatus(episode, value, true, now, false, userId, true)));

        _seriesService.UpdateStats(series, true, false);

        return Ok();
    }

    [NonAction]
    public ParallelQuery<SVR_AnimeEpisode> GetEpisodesInternal(
        SVR_AnimeSeries series,
        IncludeOnlyFilter includeMissing,
        IncludeOnlyFilter includeHidden,
        IncludeOnlyFilter includeWatched,
        HashSet<EpisodeType> type,
        string search,
        bool fuzzy)
    {
        var user = User;
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        var episodes = series.AllAnimeEpisodes
            .AsParallel()
            .Select(episode => new { Shoko = episode, AniDB = episode?.AniDB_Episode })
            .Where(both =>
            {
                // Make sure we have an anidb entry for the episode, otherwise,
                // just hide it.
                var shoko = both.Shoko;
                var anidb = both.AniDB;
                if (anidb == null)
                    return false;

                // Filter by hidden state, if specified
                if (includeHidden != IncludeOnlyFilter.True)
                {
                    // If we should hide hidden episodes and the episode is hidden, then hide it.
                    // Or if we should only show hidden episodes and the episode is not hidden, then hide it.
                    var shouldHideHidden = includeHidden == IncludeOnlyFilter.False;
                    if (shouldHideHidden == shoko.IsHidden)
                        return false;
                }

                // Filter by episode type, if specified
                if (type != null && type.Count > 0)
                {
                    var mappedType = Episode.MapAniDBEpisodeType((AniDBEpisodeType)anidb.EpisodeType);
                    if (!type.Contains(mappedType))
                        return false;
                }

                // Filter by availability, if specified
                if (includeMissing != IncludeOnlyFilter.True)
                {
                    // If we should hide missing episodes and the episode has no files, then hide it.
                    // Or if we should only show missing episodes and the episode has files, the hide it.
                    var shouldHideMissing = includeMissing == IncludeOnlyFilter.False;
                    var noFiles = shoko.VideoLocals.Count == 0;
                    if (shouldHideMissing == noFiles)
                        return false;
                }

                // Filter by user watched status, if specified
                if (includeWatched != IncludeOnlyFilter.True)
                {
                    // If we should hide watched episodes and the episode is watched, then hide it.
                    // Or if we should only show watched episodes and the the episode is not watched, then hide it.
                    var shouldHideWatched = includeWatched == IncludeOnlyFilter.False;
                    var isWatched = shoko.GetUserRecord(user.JMMUserID)?.WatchedDate != null;
                    if (shouldHideWatched == isWatched)
                        return false;
                }

                return true;
            });
        if (hasSearch)
        {
            var languages = SettingsProvider.GetSettings()
                .Language.EpisodeTitleLanguageOrder
                .Select(lang => lang.GetTitleLanguage())
                .Concat(new TitleLanguage[] { TitleLanguage.English, TitleLanguage.Romaji })
                .ToHashSet();
            return episodes
                .Search(
                    search,
                    ep => RepoFactory.AniDB_Episode_Title.GetByEpisodeID(ep.AniDB.EpisodeID)
                        .Where(title => title != null && languages.Contains(title.Language))
                        .Select(title => title.Title)
                        .Append(ep.Shoko.PreferredTitle)
                        .Distinct()
                        .ToList(),
                    fuzzy
                )
                .Select(a => a.Result.Shoko);
        }

        // Order the episodes since we're not using the search ordering.
        return episodes
            .OrderBy(episode => episode.AniDB.EpisodeType)
            .ThenBy(episode => episode.AniDB.EpisodeNumber)
            .Select(a => a.Shoko);
    }

    /// <summary>
    /// Get the <see cref="Episode.AniDB"/>s for the <see cref="Series.AniDB"/> with <paramref name="anidbID"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Filter"/> or <see cref="Group"/> is irrelevant at this level.
    /// </remarks>
    /// <param name="anidbID">AniDB series ID</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="includeMissing">Include missing episodes in the list.</param>
    /// <param name="includeHidden">Include hidden episodes in the list.</param>
    /// <param name="includeWatched">Include watched episodes in the list.</param>
    /// <param name="type">Filter episodes by the specified <see cref="EpisodeType"/>s.</param>
    /// <param name="search">An optional search query to filter episodes based on their titles.</param>
    /// <param name="fuzzy">Indicates that fuzzy-matching should be used for the search query.</param>
    /// <returns>A list of episodes based on the specified filters.</returns>
    [HttpGet("AniDB/{anidbID}/Episode")]
    public ActionResult<ListResult<Episode.AniDB>> GetAniDBEpisodes(
        [FromRoute] int anidbID,
        [FromQuery, Range(0, 1000)] int pageSize = 20,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] IncludeOnlyFilter includeMissing = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeHidden = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeWatched = IncludeOnlyFilter.True,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeType> type = null,
        [FromQuery] string search = null,
        [FromQuery] bool fuzzy = true)
    {
        var anidbSeries = RepoFactory.AniDB_Anime.GetByAnimeID(anidbID);
        if (anidbSeries == null)
            return NotFound(AnidbNotFoundForAnidbID);

        if (!User.AllowedAnime(anidbSeries))
            return Forbid(AnidbForbiddenForUser);

        var user = User;
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        var episodes = anidbSeries.AniDBEpisodes
            .AsParallel()
            .Select(episode => new { Shoko = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(episode.EpisodeID), AniDB = episode })
            .Where(both =>
            {
                // Make sure we have an anidb entry for the episode, otherwise,
                // just hide it.
                var shoko = both.Shoko;
                var anidb = both.AniDB;
                if (anidb == null)
                    return false;

                // Filter by hidden state, if specified
                if (includeHidden != IncludeOnlyFilter.True)
                {
                    // If we should hide hidden episodes and the episode is hidden, then hide it.
                    // Or if we should only show hidden episodes and the episode is not hidden, then hide it.
                    var shouldHideHidden = includeHidden == IncludeOnlyFilter.False;
                    var isHidden = shoko?.IsHidden ?? false;
                    if (shouldHideHidden == isHidden)
                        return false;
                }

                // Filter by episode type, if specified
                if (type != null && type.Count > 0)
                {
                    var mappedType = Episode.MapAniDBEpisodeType((AniDBEpisodeType)anidb.EpisodeType);
                    if (!type.Contains(mappedType))
                        return false;
                }

                // Filter by availability, if specified
                if (includeMissing != IncludeOnlyFilter.True)
                {
                    // If we should hide missing episodes and the episode has no files, then hide it.
                    // Or if we should only show missing episodes and the episode has files, the hide it.
                    var shouldHideMissing = includeMissing == IncludeOnlyFilter.False;
                    var files = shoko?.VideoLocals.Count ?? 0;
                    var noFiles = files == 0;
                    if (shouldHideMissing == noFiles)
                        return false;
                }

                // Filter by user watched status, if specified
                if (includeWatched != IncludeOnlyFilter.True)
                {
                    // If we should hide watched episodes and the episode is watched, then hide it.
                    // Or if we should only show watched episodes and the the episode is not watched, then hide it.
                    var shouldHideWatched = includeWatched == IncludeOnlyFilter.False;
                    var isWatched = shoko?.GetUserRecord(user.JMMUserID)?.WatchedDate != null;
                    if (shouldHideWatched == isWatched)
                        return false;
                }

                return true;
            });
        if (hasSearch)
        {
            var languages = SettingsProvider.GetSettings()
                .Language.SeriesTitleLanguageOrder
                .Select(lang => lang.GetTitleLanguage())
                .Concat(new TitleLanguage[] { TitleLanguage.English, TitleLanguage.Romaji })
                .ToHashSet();
            return episodes
                .Search(
                    search,
                    ep => RepoFactory.AniDB_Episode_Title.GetByEpisodeID(ep.AniDB.EpisodeID)
                        .Where(title => title != null && languages.Contains(title.Language))
                        .Select(title => title.Title)
                        .Append(ep.Shoko.PreferredTitle)
                        .Distinct()
                        .ToList(),
                    fuzzy
                )
                .ToListResult(a => new Episode.AniDB(a.Result.AniDB), page, pageSize);
        }

        // Order the episodes since we're not using the search ordering.
        return episodes
            .OrderBy(episode => episode.AniDB.EpisodeType)
            .ThenBy(episode => episode.AniDB.EpisodeNumber)
            .ToListResult(a => new Episode.AniDB(a.AniDB), page, pageSize);
    }

    /// <summary>
    /// Get the next <see cref="Episode"/> for the <see cref="Series"/> with <paramref name="seriesID"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Filter"/> or <see cref="Group"/> is irrelevant at this level.
    /// </remarks>
    /// <param name="seriesID">Series ID</param>
    /// <param name="onlyUnwatched">Only show the next unwatched episode.</param>
    /// <param name="includeSpecials">Include specials in the search.</param>
    /// <param name="includeMissing">Include missing episodes in the list.</param>
    /// <param name="includeHidden">Include hidden episodes in the list.</param>
    /// <param name="includeRewatching">Include already watched episodes in the
    /// search if we determine the user is "re-watching" the series.</param>
    /// <param name="includeFiles">Include files with the episodes.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeAbsolutePaths">Include absolute paths for the file locations.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("{seriesID}/NextUpEpisode")]
    public ActionResult<Episode> GetNextUnwatchedEpisode([FromRoute] int seriesID,
        [FromQuery] bool onlyUnwatched = true,
        [FromQuery] bool includeSpecials = true,
        [FromQuery] bool includeMissing = true,
        [FromQuery] bool includeHidden = false,
        [FromQuery] bool includeRewatching = false,
        [FromQuery] bool includeFiles = false,
        [FromQuery] bool includeMediaInfo = false,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        var user = User;
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!user.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        var episode = _seriesService.GetNextEpisode(series, user.JMMUserID, new()
        {
            IncludeCurrentlyWatching = !onlyUnwatched,
            IncludeHidden = includeHidden,
            IncludeMissing = includeMissing,
            IncludeRewatching = includeRewatching,
            IncludeSpecials = includeSpecials,
        });
        if (episode == null)
            return null;

        return new Episode(HttpContext, episode, includeDataFrom, includeFiles, includeMediaInfo, includeAbsolutePaths, includeXRefs);
    }
    #endregion

    #region File

    /// <summary>
    /// Rescan all files for a series.
    /// </summary>
    /// <param name="seriesID">Series ID.</param>
    /// <param name="priority">Increase the priority to the max for the queued commands.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("{seriesID}/File/Rescan")]
    public async Task<ActionResult> RescanSeriesFiles([FromRoute] int seriesID, [FromQuery] bool priority = false)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var file in series.VideoLocals)
        {
            if (priority)
                await scheduler.StartJobNow<ProcessFileJob>(c =>
                    {
                        c.VideoLocalID = file.VideoLocalID;
                        c.ForceAniDB = true;
                    }
                );
            else
                await scheduler.StartJob<ProcessFileJob>(c =>
                    {
                        c.VideoLocalID = file.VideoLocalID;
                        c.ForceAniDB = true;
                    }
                );
        }

        return Ok();
    }

    /// <summary>
    /// Rehash all files for a series.
    /// </summary>
    /// <param name="seriesID">Series ID.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("{seriesID}/File/Rehash")]
    public async Task<ActionResult> RehashSeriesFiles([FromRoute] int seriesID)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var file in series.VideoLocals)
        {
            var filePath = file.FirstResolvedPlace?.FullServerPath;
            if (string.IsNullOrEmpty(filePath))
                continue;

            await scheduler.StartJobNow<HashFileJob>(c =>
                {
                    c.FilePath = filePath;
                    c.ForceHash = true;
                }
            );
        }

        return Ok();
    }

    #endregion

    #region Vote

    /// <summary>
    /// Add a permanent or temporary user-submitted rating for the series.
    /// </summary>
    /// <param name="seriesID"></param>
    /// <param name="vote"></param>
    /// <returns></returns>
    [HttpPost("{seriesID}/Vote")]
    public async Task<ActionResult> PostSeriesUserVote([FromRoute] int seriesID, [FromBody] Vote vote)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        if (vote.Value > vote.MaxValue)
            return ValidationProblem($"Value must be less than or equal to the set max value ({vote.MaxValue}).", nameof(vote.Value));

        var voteType = (vote.Type?.ToLowerInvariant() ?? "") switch
        {
            "temporary" => AniDBVoteType.AnimeTemp,
            "permanent" => AniDBVoteType.Anime,
            _ => series.AniDB_Anime?.GetFinishedAiring() ?? false ? AniDBVoteType.Anime : AniDBVoteType.AnimeTemp,
        };
        await _seriesService.AddSeriesVote(series, voteType, vote.GetRating());

        return NoContent();
    }

    #endregion

    #region Images

    #region All images

    private static readonly HashSet<Image.ImageType> _allowedImageTypes = [Image.ImageType.Poster, Image.ImageType.Banner, Image.ImageType.Backdrop, Image.ImageType.Logo];

    private const string InvalidIDForSource = "Invalid image id for selected source.";

    private const string InvalidImageIsDisabled = "Image is disabled.";

    /// <summary>
    /// Get all images for series with ID, optionally with Disabled images, as well.
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <param name="includeDisabled"></param>
    /// <returns></returns>
    [HttpGet("{seriesID}/Images")]
    public ActionResult<Images> GetSeriesImages([FromRoute] int seriesID, [FromQuery] bool includeDisabled)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        return series.GetImages().ToDto(includeDisabled: includeDisabled);
    }

    #endregion

    #region Default image

    /// <summary>
    /// Get the default <see cref="Image"/> for the given <paramref name="imageType"/> for the <see cref="Series"/>.
    /// </summary>
    /// <param name="seriesID">Series ID</param>
    /// <param name="imageType">Poster, Banner, Fanart</param>
    /// <returns></returns>
    [HttpGet("{seriesID}/Images/{imageType}")]
    public ActionResult<Image> GetSeriesDefaultImageForType([FromRoute] int seriesID,
        [FromRoute] Image.ImageType imageType)
    {
        if (!_allowedImageTypes.Contains(imageType))
            return NotFound();

        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        var imageEntityType = imageType.ToServer();
        var preferredImage = series.GetPreferredImageForType(imageEntityType);
        if (preferredImage != null)
            return new Image(preferredImage);

        var images = series.GetImages().ToDto();
        return imageEntityType switch
        {
            ImageEntityType.Poster => images.Posters.FirstOrDefault(),
            ImageEntityType.Banner => images.Banners.FirstOrDefault(),
            ImageEntityType.Backdrop => images.Backdrops.FirstOrDefault(),
            ImageEntityType.Logo => images.Logos.FirstOrDefault(),
            _ => null
        };
    }


    /// <summary>
    /// Set the default <see cref="Image"/> for the given <paramref name="imageType"/> for the <see cref="Series"/>.
    /// </summary>
    /// <param name="seriesID">Series ID</param>
    /// <param name="imageType">Poster, Banner, Fanart</param>
    /// <param name="body">The body containing the source and id used to set.</param>
    /// <returns></returns>
    [HttpPut("{seriesID}/Images/{imageType}")]
    public ActionResult<Image> SetSeriesDefaultImageForType([FromRoute] int seriesID,
        [FromRoute] Image.ImageType imageType, [FromBody] Image.Input.DefaultImageBody body)
    {
        if (!_allowedImageTypes.Contains(imageType))
            return NotFound();

        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        // Check if the id is valid for the given type and source.
        var dataSource = body.Source.ToServer();
        var imageEntityType = imageType.ToServer();
        var image = ImageUtils.GetImageMetadata(dataSource, imageEntityType, body.ID);
        if (image is null)
            return ValidationProblem(InvalidIDForSource);
        if (!image.IsEnabled)
            return ValidationProblem(InvalidImageIsDisabled);

        // Create or update the entry.
        var defaultImage = RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(series.AniDB_ID, imageEntityType) ??
            new() { AnidbAnimeID = series.AniDB_ID, ImageType = imageEntityType };
        defaultImage.ImageID = body.ID;
        defaultImage.ImageSource = dataSource;
        RepoFactory.AniDB_Anime_PreferredImage.Save(defaultImage);

        // Update the contract data (used by Shoko Desktop).
        RepoFactory.AnimeSeries.Save(series, false);

        return new Image(body.ID, imageEntityType, dataSource, true);
    }

    /// <summary>
    /// Unset the default <see cref="Image"/> for the given <paramref name="imageType"/> for the <see cref="Series"/>.
    /// </summary>
    /// <param name="seriesID"></param>
    /// <param name="imageType">Poster, Banner, Fanart</param>
    /// <returns></returns>
    [HttpDelete("{seriesID}/Images/{imageType}")]
    public ActionResult DeleteSeriesDefaultImageForType([FromRoute] int seriesID, [FromRoute] Image.ImageType imageType)
    {
        if (!_allowedImageTypes.Contains(imageType))
            return NotFound();

        // Check if the series exists and if the user can access the series.
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        // Check if a default image is set.
        var imageEntityType = imageType.ToServer();
        var defaultImage = RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(series.AniDB_ID, imageEntityType);
        if (defaultImage == null)
            return ValidationProblem("No default banner.");

        // Delete the entry.
        RepoFactory.AniDB_Anime_PreferredImage.Delete(defaultImage);

        // Update the contract data (used by Shoko Desktop).
        RepoFactory.AnimeSeries.Save(series, false);

        // Don't return any content.
        return NoContent();
    }

    #endregion

    #endregion

    #region Tags

    /// <summary>
    /// Get tags for Series with ID, optionally applying the given <see cref="TagFilter.Filter" />
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <param name="filter"></param>
    /// <param name="excludeDescriptions"></param>
    /// <param name="orderByName">Order tags by name (and source) only. Don't use the tag weights for ordering.</param>
    /// <param name="onlyVerified">Only show verified tags.</param>
    /// <returns></returns>
    [HttpGet("{seriesID}/Tags")]
    public ActionResult<List<Tag>> GetSeriesTags([FromRoute] int seriesID, [FromQuery] TagFilter.Filter filter = 0,
        [FromQuery] bool excludeDescriptions = false, [FromQuery] bool orderByName = false,
        [FromQuery] bool onlyVerified = true)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        var anidb = series.AniDB_Anime;
        if (anidb == null)
        {
            return new List<Tag>();
        }

        return Series.GetTags(anidb, filter, excludeDescriptions, orderByName, onlyVerified);
    }

    /// <summary>
    /// Get user tags for Series with ID.
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <param name="excludeDescriptions">Exclude tag descriptions.</param>
    /// <returns></returns>
    [HttpGet("{seriesID}/Tags/User")]
    public ActionResult<List<Tag>> GetSeriesUserTags(
        [FromRoute] int seriesID,
        [FromQuery] bool excludeDescriptions = false)
        => GetSeriesTags(seriesID, TagFilter.Filter.User | TagFilter.Filter.Invert, excludeDescriptions, true, true);

    /// <summary>
    /// Add user tags for Series with ID.
    /// </summary>
    /// <param name="seriesID">Shoko ID.</param>
    /// <param name="body">Body containing the user tag ids to add.</param>
    /// <returns>No content if nothing was added, Created if any cross-references were added, otherwise an error action result.</returns>
    [HttpPost("{seriesID}/Tags/User")]
    [Authorize("admin")]
    public ActionResult AddSeriesUserTags(
        [FromRoute] int seriesID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Series.Input.AddOrRemoveUserTagsBody body
    )
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        var existingTagIds = RepoFactory.CrossRef_CustomTag.GetByAnimeID(seriesID);
        var toAdd = body.IDs
            .Except(existingTagIds.Select(xref => xref.CustomTagID))
            .Select(id => new CrossRef_CustomTag
            {
                CrossRefID = seriesID,
                CrossRefType = (int)CustomTagCrossRefType.Anime,
                CustomTagID = id,
            })
            .ToList();
        if (toAdd.Count is 0)
            return NoContent();

        RepoFactory.CrossRef_CustomTag.Save(toAdd);

        return Created();
    }

    /// <summary>
    /// Remove user tags for Series with ID.
    /// </summary>
    /// <param name="seriesID">Shoko ID.</param>
    /// <param name="body">Body containing the user tag ids to remove.</param>
    /// <returns>No content if nothing was removed, Ok if any cross-references were removed, otherwise an error action result.</returns>
    [HttpDelete("{seriesID}/Tags/User")]
    [Authorize("admin")]
    public ActionResult RemoveSeriesUserTags(
        [FromRoute] int seriesID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Series.Input.AddOrRemoveUserTagsBody body
    )
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        var existingTagIds = RepoFactory.CrossRef_CustomTag.GetByAnimeID(seriesID);
        var toRemove = existingTagIds
            .IntersectBy(body.IDs, xref => xref.CustomTagID)
            .ToList();
        if (toRemove.Count is 0)
            return NoContent();

        RepoFactory.CrossRef_CustomTag.Delete(toRemove);
        return Ok();
    }

    /// <summary>
    /// Get tags for Series with ID, applying the given TagFilter (0 is show all)
    /// </summary>
    ///
    /// <param name="seriesID">Shoko ID</param>
    /// <param name="filter"></param>
    /// <param name="excludeDescriptions"></param>
    /// <returns></returns>
    [HttpGet("{seriesID}/Tags/{filter}")]
    [Obsolete("Use Tags with query parameter instead.")]
    public ActionResult<List<Tag>> GetSeriesTagsFromPath([FromRoute] int seriesID, [FromRoute] TagFilter.Filter filter,
        [FromQuery] bool excludeDescriptions = false)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        return GetSeriesTags(seriesID, filter, excludeDescriptions);
    }

    #endregion

    #region Cast

    /// <summary>
    /// Get the cast listing for series with ID
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <param name="roleType">Filter by role type</param>
    /// <returns></returns>
    [HttpGet("{seriesID}/Cast")]
    public ActionResult<List<Role>> GetSeriesCast([FromRoute] int seriesID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Role.CreatorRoleType> roleType = null)
    {
        if (seriesID == 0) return BadRequest(SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        return Series.GetCast(series.AniDB_ID, roleType);
    }

    #endregion

    #region Group

    /// <summary>
    /// Move the series to a new group, and update accordingly
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <param name="groupID"></param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPatch("{seriesID}/Move/{groupID}")]
    public ActionResult MoveSeries([FromRoute] int seriesID, [FromRoute] int groupID)
    {
        if (seriesID == 0)
            return BadRequest(SeriesWithZeroID);

        if (groupID == 0)
            return BadRequest(GroupController.GroupWithZeroID);

        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
            return ValidationProblem("No Group entry for the given groupID", "groupID");

        _seriesService.MoveSeries(series, group);

        return Ok();
    }

    #endregion

    #region Search

    /// <summary>
    /// Search for series with given query in name or tag
    /// </summary>
    /// <param name="query">target string</param>
    /// <param name="fuzzy">whether or not to use fuzzy search</param>
    /// <param name="limit">number of return items</param>
    /// <returns>List<see cref="Series.SearchResult"/></returns>
    [HttpGet("Search")]
    public ActionResult<IEnumerable<Series.SearchResult>> SearchQuery([FromQuery] string query, [FromQuery] bool fuzzy = true,
        [FromQuery, Range(0, 1000)] int limit = 50)
        => SearchInternal(query, fuzzy, limit);

    /// <summary>
    /// Search for series with given query in name or tag
    /// </summary>
    /// <param name="query">target string</param>
    /// <param name="fuzzy">whether or not to use fuzzy search</param>
    /// <param name="limit">number of return items</param>
    /// <param name="searchById">Enable search by anidb anime id.</param>
    /// <returns>List<see cref="Series.SearchResult"/></returns>
    [Obsolete("Use the other endpoint instead.")]
    [HttpGet("Search/{query}")]
    public ActionResult<IEnumerable<Series.SearchResult>> SearchPath([FromRoute] string query, [FromQuery] bool fuzzy = true,
        [FromQuery, Range(0, 1000)] int limit = 50, [FromQuery] bool searchById = false)
        => SearchInternal(HttpUtility.UrlDecode(query), fuzzy, limit, searchById);

    [NonAction]
    internal ActionResult<IEnumerable<Series.SearchResult>> SearchInternal(string query, bool fuzzy = true, int limit = 50, bool searchById = false)
    {
        var flags = SeriesSearch.SearchFlags.Titles;
        if (fuzzy)
            flags |= SeriesSearch.SearchFlags.Fuzzy;

        return SeriesSearch.SearchSeries(User, query, limit, flags, searchById: searchById)
            .Select(result => new Series.SearchResult(result, User.JMMUserID))
            .ToList();
    }

    /// <summary>
    /// Search the title dump for the given query or directly using the anidb id.
    /// </summary>
    /// <param name="query">Query to search for</param>
    /// <param name="fuzzy">Indicates fuzzy-matching should be used for the search.</param>
    /// <param name="local">Only search for results in the local collection if it's true and only search for results not in the local collection if false. Omit to include both.</param>
    /// <param name="includeTitles">Include titles in the results.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <returns></returns>
    [HttpGet("AniDB/Search")]
    public ActionResult<ListResult<Series.AniDB>> AnidbSearchQuery([FromQuery] string query,
        [FromQuery] bool fuzzy = true, [FromQuery] bool? local = null,
        [FromQuery] bool includeTitles = true, [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
        => AnidbSearchInternal(query, fuzzy, local, includeTitles, pageSize, page);

    /// <summary>
    /// Search the title dump for the given query or directly using the anidb id.
    /// </summary>
    /// <param name="query">Query to search for</param>
    /// <param name="fuzzy">Indicates fuzzy-matching should be used for the search.</param>
    /// <param name="local">Only search for results in the local collection if it's true and only search for results not in the local collection if false. Omit to include both.</param>
    /// <param name="includeTitles">Include titles in the results.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <returns></returns>
    [Obsolete("Use the other endpoint instead.")]
    [HttpGet("AniDB/Search/{query}")]
    public ActionResult<ListResult<Series.AniDB>> AnidbSearchPath([FromRoute] string query,
        [FromQuery] bool fuzzy = true, [FromQuery] bool? local = null,
        [FromQuery] bool includeTitles = true, [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
        => AnidbSearchInternal(HttpUtility.UrlDecode(query), fuzzy, local, includeTitles, pageSize, page);

    [NonAction]
    internal ListResult<Series.AniDB> AnidbSearchInternal(string query, bool fuzzy = true, bool? local = null,
        bool includeTitles = true, int pageSize = 50, int page = 1)
    {
        // We're searching using the anime ID, so first check the local db then the title cache for a match.
        if (int.TryParse(query, out var animeID))
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            if (anime != null)
            {
                return new(1, [new(anime, includeTitles: includeTitles)]);
            }

            // Check the title cache for a match.
            var result = _titleHelper.SearchAnimeID(animeID);
            if (result != null)
            {
                return new(1, [new(result, includeTitles: includeTitles)]);
            }

            return new();
        }

        // Return all known entries on anidb if no query is given.
        if (string.IsNullOrEmpty(query))
            return _titleHelper.GetAll()
                .OrderBy(anime => anime.AnimeID)
                .Select(result =>
                {
                    var series = RepoFactory.AnimeSeries.GetByAnimeID(result.AnimeID);
                    if (local.HasValue && series == null == local.Value)
                        return null;

                    return new Series.AniDB(result, series, includeTitles);
                })
                .WhereNotNull()
                .ToListResult(page, pageSize);

        // Search the title cache for anime matching the query.
        return _titleHelper.SearchTitle(query, fuzzy)
            .Select(result =>
            {
                var series = RepoFactory.AnimeSeries.GetByAnimeID(result.AnimeID);
                if (local.HasValue && series == null == local.Value)
                    return null;

                return new Series.AniDB(result, series, includeTitles);
            })
            .WhereNotNull()
            .ToListResult(page, pageSize);
    }

    /// <summary>
    /// Searches for series whose title starts with a string
    /// </summary>
    /// <param name="query"></param>
    /// <param name="limit"></param>
    /// <returns></returns>
    [HttpGet("StartsWith/{query}")]
    public ActionResult<List<Series.SearchResult>> StartsWith([FromRoute] string query,
        [FromQuery] int limit = int.MaxValue)
    {
        var user = User;
        query = query.ToLowerInvariant();

        var seriesList = new List<Series.SearchResult>();
        var tempSeries = new ConcurrentDictionary<SVR_AnimeSeries, string>();
        var allSeries = RepoFactory.AnimeSeries.GetAll()
            .Where(user.AllowedSeries)
            .AsParallel();

        #region Search_TitlesOnly

        allSeries.ForAll(a => CheckTitlesStartsWith(a, query, ref tempSeries, limit));
        var series =
            tempSeries.OrderBy(a => a.Value).ToDictionary(a => a.Key, a => a.Value);

        foreach (var (ser, match) in series)
        {
            seriesList.Add(new Series.SearchResult(new() { Result = ser, Match = match }, User.JMMUserID));
            if (seriesList.Count >= limit)
            {
                break;
            }
        }

        #endregion

        return seriesList;
    }

    /// <summary>
    /// Get the series that reside in the path that ends with <param name="path"></param>
    /// </summary>
    /// <returns></returns>
    [HttpGet("PathEndsWith/{*path}")]
    public ActionResult<List<Series>> PathEndsWith([FromRoute] string path)
    {
        var user = User;
        var query = path;
        if (query.Contains('%') || query.Contains('+'))
        {
            query = Uri.UnescapeDataString(query);
        }

        if (query.Contains('%'))
        {
            query = Uri.UnescapeDataString(query);
        }

        query = query.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)
            .TrimEnd(Path.DirectorySeparatorChar);
        // There should be no circumstance where FullServerPath has no Directory Name, unless you have missing import folders
        return RepoFactory.VideoLocalPlace.GetAll().AsParallel()
            .Where(a =>
            {
                if (a.FullServerPath == null) return false;
                var dir = Path.GetDirectoryName(a.FullServerPath);
                return dir != null && dir.EndsWith(query, StringComparison.OrdinalIgnoreCase);
            })
            .SelectMany(a => a.VideoLocal?.AnimeEpisodes ?? Enumerable.Empty<SVR_AnimeEpisode>()).Select(a => a.AnimeSeries)
            .Distinct()
            .Where(ser => ser != null && user.AllowedSeries(ser)).Select(a => new Series(a, User.JMMUserID)).ToList();
    }

    #region Helpers

    [NonAction]
    private static void CheckTitlesStartsWith(SVR_AnimeSeries a, string query,
        ref ConcurrentDictionary<SVR_AnimeSeries, string> series, int limit)
    {
        if (series.Count >= limit)
        {
            return;
        }

        var titles = a.AniDB_Anime.GetAllTitles();
        if ((titles?.Count ?? 0) == 0)
        {
            return;
        }

        var match = string.Empty;
        foreach (var title in titles)
        {
            if (string.IsNullOrEmpty(title))
            {
                continue;
            }

            if (title.StartsWith(query, StringComparison.InvariantCultureIgnoreCase))
            {
                match = title;
            }
        }

        // Keep the lowest distance
        if (match != string.Empty)
        {
            series.TryAdd(a, match);
        }
    }

    #endregion

    #endregion

    /// <summary>
    /// Get a list of all years that series that you have aired in. One Piece would return every year from 1999 to preset (assuming it's still airing *today*)
    /// </summary>
    /// <returns></returns>
    [HttpGet("Years")]
    public static ActionResult<IEnumerable<int>> GetAllYears()
        => RepoFactory.AnimeSeries.GetAllYears().ToList();

    /// <summary>
    /// Get a list of all years and seasons (2024 Winter) that series that you have aired in. One Piece would return every Season from 1999 Fall to preset (assuming it's still airing *today*)
    /// </summary>
    /// <returns></returns>
    [HttpGet("Seasons")]
    public static ActionResult<IEnumerable<YearlySeason>> GetAllSeasons()
        => RepoFactory.AnimeSeries.GetAllSeasons().Select(a => new YearlySeason(a.Year, a.Season)).Order().ToList();
}
