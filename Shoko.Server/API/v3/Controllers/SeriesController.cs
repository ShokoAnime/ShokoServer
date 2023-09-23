using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Commands;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using DataSource = Shoko.Server.API.v3.Models.Common.DataSource;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class SeriesController : BaseController
{
    private readonly ICommandRequestFactory _commandFactory;
    private readonly SeriesFactory _seriesFactory;
    private readonly IHttpConnectionHandler _httpHandler;

    public SeriesController(ICommandRequestFactory commandFactory, IHttpConnectionHandler httpHandler, ISettingsProvider settingsProvider, SeriesFactory seriesFactory) : base(settingsProvider)
    {
        _commandFactory = commandFactory;
        _httpHandler = httpHandler;
        _seriesFactory = seriesFactory;
    }

    #region Return messages

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
    public ActionResult<ListResult<Series>> GetAllSeries([FromQuery] [Range(0, 100)] int pageSize = 50,
        [FromQuery] [Range(1, int.MaxValue)] int page = 1, [FromQuery] string startsWith = "")
    {
        startsWith = startsWith.ToLowerInvariant();
        var user = User;
        return RepoFactory.AnimeSeries.GetAll()
            .Select(series => (series, seriesName: series.GetSeriesName().ToLowerInvariant()))
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
            .ToListResult(tuple => _seriesFactory.GetSeries(tuple.series), page, pageSize);
    }

    /// <summary>
    /// Get the series with ID
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <param name="randomImages">Randomise images shown for the <see cref="Series"/>.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("{seriesID}")]
    public ActionResult<Series> GetSeries([FromRoute] int seriesID, [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        return _seriesFactory.GetSeries(series, randomImages, includeDataFrom);
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
    public ActionResult DeleteSeries([FromRoute] int seriesID, [FromQuery] bool deleteFiles = false, [FromQuery] bool completelyRemove = false)
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        series.DeleteSeries(deleteFiles, true, completelyRemove);

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
    /// <returns></returns>
    [HttpGet("WithoutFiles")]
    public ActionResult<ListResult<Series>> GetSeriesWithoutFiles([FromQuery] [Range(0, 100)] int pageSize = 50,
        [FromQuery] [Range(1, int.MaxValue)] int page = 1)
    {
        var user = User;
        return RepoFactory.AnimeSeries.GetAll()
            .Where(series => user.AllowedSeries(series) && series.GetVideoLocals().Count == 0)
            .OrderBy(series => series.GetSeriesName().ToLowerInvariant())
            .ToListResult(series => _seriesFactory.GetSeries(series), page, pageSize);
    }

    /// <summary>
    /// Get a paginated list of <see cref="Series"/> with manually linked local files, available to the current <see cref="User"/>.
    /// </summary>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <returns></returns>
    [HttpGet("WithManuallyLinkedFiles")]
    public ActionResult<ListResult<Series>> GetSeriesWithManuallyLinkedFiles([FromQuery] [Range(0, 100)] int pageSize = 50,
        [FromQuery] [Range(1, int.MaxValue)] int page = 1)
    {
        var user = User;
        return RepoFactory.AnimeSeries.GetAll()
            .Where(series => user.AllowedSeries(series) && series.GetVideoLocals(CrossRefSource.User).Count() != 0)
            .OrderBy(series => series.GetSeriesName().ToLowerInvariant())
            .ToListResult(series => _seriesFactory.GetSeries(series), page, pageSize);
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
    public ActionResult<ListResult<Series.AniDB>> GetAllAnime([FromQuery] [Range(0, 100)] int pageSize = 50,
        [FromQuery] [Range(1, int.MaxValue)] int page = 1, [FromQuery] string startsWith = "")
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
            .ToListResult(tuple => _seriesFactory.GetAniDB(tuple.anime), page, pageSize);
    }

    /// <summary>
    /// Get a paginated list of all AniDB <see cref="SeriesRelation"/>s.
    /// </summary>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <returns></returns>
    [HttpGet("AniDB/Relations")]
    public ActionResult<ListResult<SeriesRelation>> GetAnidbRelations([FromQuery] [Range(0, 100)] int pageSize = 50,
        [FromQuery] [Range(1, int.MaxValue)] int page = 1)
    {
        var user = User;
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
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        var anidb = series.GetAnime();
        if (anidb == null)
        {
            return InternalError(AnidbNotFoundForSeriesID);
        }

        return _seriesFactory.GetAniDB(anidb, series);
    }

    /// <summary>
    /// Get all similar <see cref="Series.AniDB"/> entries for the <paramref name="seriesID"/>.
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <returns></returns>
    [HttpGet("{seriesID}/AniDB/Similar")]
    public ActionResult<List<Series.AniDB>> GetAnidbSimilarBySeriesID([FromRoute] int seriesID)
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        var anidb = series.GetAnime();
        if (anidb == null)
        {
            return InternalError(AnidbNotFoundForSeriesID);
        }

        return RepoFactory.AniDB_Anime_Similar.GetByAnimeID(anidb.AnimeID)
            .Select(similar => _seriesFactory.GetAniDB(similar))
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
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        var anidb = series.GetAnime();
        if (anidb == null)
        {
            return InternalError(AnidbNotFoundForSeriesID);
        }

        return RepoFactory.AniDB_Anime_Relation.GetByAnimeID(anidb.AnimeID)
            .Select(relation => _seriesFactory.GetAniDB(relation))
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
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        var anidb = series.GetAnime();
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
    /// <param name="startDate">Start date to use if recommending for a watch period. Only setting the <paramref name="startDate"/> and not <paramref name="endDate"/> will result in using the watch history from the start date to the present date.</param>
    /// <param name="endDate">End date to use if recommending for a watch period.</param>
    /// <param name="approval">Minumum approval percentage for similar animes.</param>
    /// <returns></returns>
    [HttpGet("AniDB/RecommendedForYou")]
    public ActionResult<ListResult<Series.AniDBRecommendedForYou>> GetAnimeRecommendedForYou(
        [FromQuery] [Range(0, 100)] int pageSize = 30,
        [FromQuery] [Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool showAll = false,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] [Range(0, 1)] double? approval = null
    )
    {
        startDate = startDate?.ToLocalTime();
        endDate = endDate?.ToLocalTime();
        if (startDate.HasValue && !endDate.HasValue)
            endDate = DateTime.Now;

        if (endDate.HasValue && !startDate.HasValue)
            ModelState.AddModelError(nameof(startDate), "Missing start date.");

        if (startDate.HasValue && endDate.HasValue)
        {
            if (endDate.Value > DateTime.Now)
                ModelState.AddModelError(nameof(endDate), "End date cannot be set into the future.");

            if (startDate.Value > endDate.Value)
                ModelState.AddModelError(nameof(startDate), "Start date cannot be newer than the end date.");
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var user = User;
        var watchedAnimeList = GetWatchedAnimeForPeriod(user, startDate, endDate);
        var unwatchedAnimeDict = GetUnwatchedAnime(user, showAll,
            !startDate.HasValue && !endDate.HasValue ? watchedAnimeList : null);
        return watchedAnimeList
            .SelectMany(anime =>
            {
                if (approval.HasValue)
                {
                    return anime.GetSimilarAnime().Where(similar =>
                        unwatchedAnimeDict.Keys.Contains(similar.SimilarAnimeID) &&
                        (double)(similar.Approval / similar.Total) >= approval.Value);
                }

                return anime.GetSimilarAnime()
                    .Where(similar => unwatchedAnimeDict.Keys.Contains(similar.SimilarAnimeID));
            })
            .GroupBy(anime => anime.SimilarAnimeID)
            .Select(similarTo =>
            {
                var (anime, series) = unwatchedAnimeDict[similarTo.Key];
                var similarToCount = similarTo.Count();
                return new Series.AniDBRecommendedForYou()
                {
                    Anime = _seriesFactory.GetAniDB(anime, series), SimilarTo = similarToCount
                };
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
    /// <param name="startDate">The start date of the period.</param>
    /// <param name="endDate">The end date of the period.</param>
    /// <returns>The watched anime for the user.</returns>
    [NonAction]
    private List<SVR_AniDB_Anime> GetWatchedAnimeForPeriod(SVR_JMMUser user, DateTime? startDate = null,
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
            .Select(episode => episode.GetAnimeSeries().GetAnime())
            .Where(anime => user.AllowedAnime(anime))
            .ToList();
    }

    /// <summary>
    /// Get all unwatched anime for the user.
    /// </summary>
    /// <param name="user">The user to get the unwatched anime for.</param>
    /// <param name="showAll">If true will get a list of all available anime in shoko, regardless of if it's part of the user's collection or not.</param>
    /// <param name="watchedAnime">Optional. Re-use an existing list of the watched anime.</param>
    /// <returns>The unwatched anime for the user.</returns>
    [NonAction]
    private Dictionary<int, (SVR_AniDB_Anime, SVR_AnimeSeries)> GetUnwatchedAnime(SVR_JMMUser user, bool showAll,
        IEnumerable<SVR_AniDB_Anime> watchedAnime = null)
    {
        // Get all watched series (reuse if date is not set)
        var watchedSeriesSet = (watchedAnime ?? GetWatchedAnimeForPeriod(user))
            .Select(series => series.AnimeID)
            .ToHashSet();

        if (showAll)
        {
            return RepoFactory.AniDB_Anime.GetAll()
                .Where(anime => user.AllowedAnime(anime) && !watchedSeriesSet.Contains(anime.AnimeID))
                .ToDictionary<SVR_AniDB_Anime, int, (SVR_AniDB_Anime, SVR_AnimeSeries)>(anime => anime.AnimeID,
                    anime => (anime, null));
        }

        return RepoFactory.AnimeSeries.GetAll()
            .Where(series => user.AllowedSeries(series) && !watchedSeriesSet.Contains(series.AniDB_ID))
            .ToDictionary(series => series.AniDB_ID, series => (series.GetAnime(), series));
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

        return _seriesFactory.GetAniDB(anidb);
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
            .Select(similar => _seriesFactory.GetAniDB(similar))
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
            .Select(relation => _seriesFactory.GetAniDB(relation))
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
    /// <param name="randomImages">Randomise images shown for the <see cref="Series"/>.</param>
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

        return _seriesFactory.GetSeries(series, randomImages, includeDataFrom);
    }

    /// <summary>
    /// Queue a refresh of the AniDB Info for series with AniDB ID
    /// </summary>
    /// <param name="anidbID">AniDB ID</param>
    /// <param name="force">Try to forcefully retrive updated data from AniDB if
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
    public ActionResult<bool> RefreshAniDBByAniDBID([FromRoute] int anidbID, [FromQuery] bool force = false,
        [FromQuery] bool downloadRelations = false, [FromQuery] bool? createSeriesEntry = null,
        [FromQuery] bool immediate = false, [FromQuery] bool cacheOnly = false)
    {
        if (!createSeriesEntry.HasValue)
        {
            var settings = SettingsProvider.GetSettings();
            createSeriesEntry = settings.AniDb.AutomaticallyImportSeries;
        }

        // TODO No
        return SeriesFactory.QueueAniDBRefresh(_commandFactory, _httpHandler, anidbID, force, downloadRelations,
            createSeriesEntry.Value, immediate, cacheOnly);
    }

    /// <summary>
    /// Queue a refresh of the AniDB Info for series with ID
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <param name="force">Try to forcefully retrive updated data from AniDB if
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
    public ActionResult<bool> RefreshAniDBBySeriesID([FromRoute] int seriesID, [FromQuery] bool force = false,
        [FromQuery] bool downloadRelations = false, [FromQuery] bool? createSeriesEntry = null,
        [FromQuery] bool immediate = false, [FromQuery] bool cacheOnly = false)
    {
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

        var anidb = series.GetAnime();
        if (anidb == null)
        {
            return InternalError(AnidbNotFoundForSeriesID);
        }

        // TODO No
        return SeriesFactory.QueueAniDBRefresh(_commandFactory, _httpHandler, anidb.AnimeID, force, downloadRelations,
            createSeriesEntry.Value, immediate, cacheOnly);
    }

    /// <summary>
    /// Forcefully refresh the AniDB Info from XML on disk for series with ID
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <returns>True if the refresh is done, otherwise false if it failed.</returns>
    [HttpPost("{seriesID}/AniDB/Refresh/ForceFromXML")]
    [Obsolete]
    public ActionResult<bool> RefreshAniDBFromXML([FromRoute] int seriesID)
        => RefreshAniDBBySeriesID(seriesID, false, false, true, true, true);

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
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(TvdbNotFoundForSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(TvdbForbiddenForUser);
        }

        return _seriesFactory.GetTvDBInfo(series);
    }

    /// <summary>
    /// Add a TvDB link to a series.
    /// </summary>
    /// <param name="seriesID">Series ID</param>
    /// <param name="body">Body containing the information about the link to be made</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("{seriesID}/TvDB")]
    public ActionResult LinkTvDB([FromRoute] int seriesID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Series.Input.LinkCommonBody body)
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(TvdbNotFoundForSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(TvdbForbiddenForUser);

        var tvdbHelper = Utils.ServiceContainer.GetService<TvDBApiHelper>();
        tvdbHelper.LinkAniDBTvDB(series.AniDB_ID, body.ID, !body.Replace);

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
    /// <param name="force">Forcefully retrive updated data from TvDB</param>
    /// <returns></returns>
    [HttpPost("{seriesID}/TvDB/Refresh")]
    public ActionResult RefreshSeriesTvdbBySeriesID([FromRoute] int seriesID, [FromQuery] bool force = false)
    {
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
            SeriesFactory.QueueTvDBRefresh(_commandFactory, crossRef.TvDBID, force);

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

        return _seriesFactory.GetTvDB(tvdb, series);
    }

    /// <summary>
    /// Directly queue a refresh of the the <see cref="Series.TvDB"/> data using
    /// the <paramref name="tvdbID"/>.
    /// </summary>
    /// <param name="tvdbID">TvDB ID</param>
    /// <param name="force">Forcefully retrive updated data from TvDB</param>
    /// <param name="immediate">Try to immediately refresh the data.</param>
    /// <returns></returns>
    [HttpPost("TvDB/{tvdbID}/Refresh")]
    public ActionResult<bool> RefreshSeriesTvdbByTvdbId([FromRoute] int tvdbID, [FromQuery] bool force = false, [FromQuery] bool immediate = false)
    {
        // TODO No
        return SeriesFactory.QueueTvDBRefresh(_commandFactory, tvdbID,force, immediate);
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
            .Select(series => _seriesFactory.GetSeries(series))
            .ToList();
    }

    #endregion

    #endregion

    #region Vote

    /// <summary>
    /// Add a permanent or temprary user-submitted rating for the series.
    /// </summary>
    /// <param name="seriesID"></param>
    /// <param name="vote"></param>
    /// <returns></returns>
    [HttpPost("{seriesID}/Vote")]
    public ActionResult PostSeriesUserVote([FromRoute] int seriesID, [FromBody] Vote vote)
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        if (vote.Value > vote.MaxValue)
            return ValidationProblem($"Value must be less than or equal to the set max value ({vote.MaxValue}).", nameof(vote.Value));

        SeriesFactory.AddSeriesVote(_commandFactory, series, User.JMMUserID, vote);

        return NoContent();
    }

    #endregion

    #region Images

    #region All images

    private static HashSet<Image.ImageType> AllowedImageTypes =
        new() { Image.ImageType.Poster, Image.ImageType.Banner, Image.ImageType.Fanart };

    private const string InvalidIDForSource = "Invalid image id for selected source.";

    private const string InvalidImageTypeForSeries = "Invalid image type for series images.";

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
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        return SeriesFactory.GetArt(series.AniDB_ID, includeDisabled);
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
        if (!AllowedImageTypes.Contains(imageType))
            return NotFound();

        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        var imageSizeType = Image.GetImageSizeTypeFromType(imageType);
        var defaultBanner = SeriesFactory.GetDefaultImage(series.AniDB_ID, imageSizeType);
        if (defaultBanner != null)
        {
            return defaultBanner;
        }

        var images = SeriesFactory.GetArt(series.AniDB_ID);
        return imageSizeType switch
        {
            ImageSizeType.Poster => images.Posters.FirstOrDefault(),
            ImageSizeType.WideBanner => images.Banners.FirstOrDefault(),
            ImageSizeType.Fanart => images.Fanarts.FirstOrDefault(),
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
        if (!AllowedImageTypes.Contains(imageType))
            return NotFound();

        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        var imageEntityType = Image.GetImageTypeFromSourceAndType(body.Source, imageType);
        if (!imageEntityType.HasValue)
            return ValidationProblem("Invalid body source");

        // All dynamic ids are stringified ints, so extract the image id from the body.
        if (!int.TryParse(body.ID, out var imageID))
            return ValidationProblem("Invalid body id. Id must be a stringified int.");

        // Check if the id is valid for the given type and source.

        switch (imageEntityType.Value)
        {
            // Posters
            case ImageEntityType.AniDB_Cover:
                if (imageID != series.AniDB_ID)
                {
                    return ValidationProblem(InvalidIDForSource);
                }

                break;
            case ImageEntityType.TvDB_Cover:
                {
                    var tvdbPoster = RepoFactory.TvDB_ImagePoster.GetByID(imageID);
                    if (tvdbPoster == null)
                    {
                        return ValidationProblem(InvalidIDForSource);
                    }

                    if (tvdbPoster.Enabled != 1)
                    {
                        return ValidationProblem(InvalidImageIsDisabled);
                    }

                    break;
                }
            case ImageEntityType.MovieDB_Poster:
                var tmdbPoster = RepoFactory.MovieDB_Poster.GetByID(imageID);
                if (tmdbPoster == null)
                {
                    return ValidationProblem(InvalidIDForSource);
                }

                if (tmdbPoster.Enabled != 1)
                {
                    return ValidationProblem(InvalidImageIsDisabled);
                }

                break;

            // Banners
            case ImageEntityType.TvDB_Banner:
                var tvdbBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(imageID);
                if (tvdbBanner == null)
                {
                    return ValidationProblem(InvalidIDForSource);
                }

                if (tvdbBanner.Enabled != 1)
                {
                    return ValidationProblem(InvalidImageIsDisabled);
                }

                break;

            // Fanart
            case ImageEntityType.TvDB_FanArt:
                var tvdbFanart = RepoFactory.TvDB_ImageFanart.GetByID(imageID);
                if (tvdbFanart == null)
                {
                    return ValidationProblem(InvalidIDForSource);
                }

                if (tvdbFanart.Enabled != 1)
                {
                    return ValidationProblem(InvalidImageIsDisabled);
                }

                break;
            case ImageEntityType.MovieDB_FanArt:
                var tmdbFanart = RepoFactory.MovieDB_Fanart.GetByID(imageID);
                if (tmdbFanart == null)
                {
                    return ValidationProblem(InvalidIDForSource);
                }

                if (tmdbFanart.Enabled != 1)
                {
                    return ValidationProblem(InvalidImageIsDisabled);
                }

                break;

            // Not allowed.
            default:
                return ValidationProblem("Invalid source and/or type.");
        }

        var imageSizeType = Image.GetImageSizeTypeFromType(imageType);
        var defaultImage =
            RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(series.AniDB_ID, imageSizeType) ??
            new AniDB_Anime_DefaultImage { AnimeID = series.AniDB_ID, ImageType = (int)imageSizeType };
        defaultImage.ImageParentID = imageID;
        defaultImage.ImageParentType = (int)imageEntityType.Value;

        // Create or update the entry.
        RepoFactory.AniDB_Anime_DefaultImage.Save(defaultImage);

        // Update the contract data (used by Shoko Desktop).
        RepoFactory.AnimeSeries.Save(series, false);

        return new Image(imageID, imageEntityType.Value, true);
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
        if (!AllowedImageTypes.Contains(imageType))
            return NotFound();

        // Check if the series exists and if the user can access the series.
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        // Check if a default image is set.
        var imageSizeType = Image.GetImageSizeTypeFromType(imageType);
        var defaultImage =
            RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(series.AniDB_ID, imageSizeType);
        if (defaultImage == null)
            return ValidationProblem("No default banner.");

        // Delete the entry.
        RepoFactory.AniDB_Anime_DefaultImage.Delete(defaultImage);

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
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        var anidb = series.GetAnime();
        if (anidb == null)
        {
            return new List<Tag>();
        }

        return _seriesFactory.GetTags(anidb, filter, excludeDescriptions, orderByName, onlyVerified);
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
    [Obsolete]
    public ActionResult<List<Tag>> GetSeriesTagsFromPath([FromRoute] int seriesID, [FromRoute] TagFilter.Filter filter,
        [FromQuery] bool excludeDescriptions = false)
    {
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
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        return _seriesFactory.GetCast(series.AniDB_ID, roleType);
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
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesForbiddenForUser);
        }

        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
            return ValidationProblem("No Group entry for the given groupID", "groupID");

        series.MoveSeries(group);

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
    /// <returns>List<see cref="SeriesSearchResult"/></returns>
    [HttpGet("Search")]
    public ActionResult<IEnumerable<SeriesSearchResult>> SearchQuery([FromQuery] string query, [FromQuery] bool fuzzy = true,
        [FromQuery, Range(0, 1000)] int limit = 50)
        => SearchInternal(query, fuzzy, limit);

    /// <summary>
    /// Search for series with given query in name or tag
    /// </summary>
    /// <param name="query">target string</param>
    /// <param name="fuzzy">whether or not to use fuzzy search</param>
    /// <param name="limit">number of return items</param>
    /// <returns>List<see cref="SeriesSearchResult"/></returns>
    [Obsolete("Use the other endpoint instead.")]
    [HttpGet("Search/{query}")]
    public ActionResult<IEnumerable<SeriesSearchResult>> SearchPath([FromRoute] string query, [FromQuery] bool fuzzy = true,
        [FromQuery, Range(0, 1000)] int limit = 50)
        => SearchInternal(HttpUtility.UrlDecode(query), fuzzy, limit);

    [NonAction]
    internal ActionResult<IEnumerable<SeriesSearchResult>> SearchInternal(string query, bool fuzzy = true, int limit = 50)
    {
        var flags = SeriesSearch.SearchFlags.Titles;
        if (fuzzy)
            flags |= SeriesSearch.SearchFlags.Fuzzy;

        return SeriesSearch.SearchSeries(User, query, limit, flags)
            .Select(result => _seriesFactory.GetSeriesSearchResult(result))
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
        [FromQuery] bool includeTitles = true, [FromQuery] [Range(0, 100)] int pageSize = 50,
        [FromQuery] [Range(1, int.MaxValue)] int page = 1)
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
        [FromQuery] bool includeTitles = true, [FromQuery] [Range(0, 100)] int pageSize = 50,
        [FromQuery] [Range(1, int.MaxValue)] int page = 1)
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
                return new ListResult<Series.AniDB>(1,
                    new List<Series.AniDB> { _seriesFactory.GetAniDB(anime, includeTitles: includeTitles) });
            }

            // Check the title cache for a match.
            var result = Utils.AniDBTitleHelper.SearchAnimeID(animeID);
            if (result != null)
            {
                return new ListResult<Series.AniDB>(1,
                    new List<Series.AniDB> { _seriesFactory.GetAniDB(result, includeTitles: includeTitles) });
            }

            return new ListResult<Series.AniDB>();
        }

        // Return all known entries on anidb if no query is given.
        if (string.IsNullOrEmpty(query))
            return Utils.AniDBTitleHelper.GetAll()
                .OrderBy(anime => anime.AnimeID)
                .Select(result =>
                {
                    var series = RepoFactory.AnimeSeries.GetByAnimeID(result.AnimeID);
                    if (local.HasValue && series == null == local.Value)
                    {
                        return null;
                    }

                    return _seriesFactory.GetAniDB(result, series, includeTitles);
                })
                .Where(result => result != null)
                .ToListResult(page, pageSize);

        // Search the title cache for anime matching the query.
        return Utils.AniDBTitleHelper.SearchTitle(query, fuzzy)
            .Select(result =>
            {
                var series = RepoFactory.AnimeSeries.GetByAnimeID(result.AnimeID);
                if (local.HasValue && series == null == local.Value)
                {
                    return null;
                }

                return _seriesFactory.GetAniDB(result, series, includeTitles);
            })
            .Where(result => result != null)
            .ToListResult(page, pageSize);
    }

    /// <summary>
    /// Searches for series whose title starts with a string
    /// </summary>
    /// <param name="query"></param>
    /// <param name="limit"></param>
    /// <returns></returns>
    [HttpGet("StartsWith/{query}")]
    public ActionResult<List<SeriesSearchResult>> StartsWith([FromRoute] string query,
        [FromQuery] int limit = int.MaxValue)
    {
        var user = User;
        query = query.ToLowerInvariant();

        var seriesList = new List<SeriesSearchResult>();
        var tempSeries = new ConcurrentDictionary<SVR_AnimeSeries, string>();
        var allSeries = RepoFactory.AnimeSeries.GetAll()
            .Where(series => user.AllowedSeries(series))
            .AsParallel();

        #region Search_TitlesOnly

        allSeries.ForAll(a => CheckTitlesStartsWith(a, query, ref tempSeries, limit));
        var series =
            tempSeries.OrderBy(a => a.Value).ToDictionary(a => a.Key, a => a.Value);

        foreach (var (ser, match) in series)
        {
            seriesList.Add(_seriesFactory.GetSeriesSearchResult(new() { Result = ser, Match = match }));
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
        if (query.Contains("%") || query.Contains("+"))
        {
            query = Uri.UnescapeDataString(query);
        }

        if (query.Contains("%"))
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
            .SelectMany(a => a.VideoLocal?.GetAnimeEpisodes() ?? Enumerable.Empty<SVR_AnimeEpisode>()).Select(a => a.GetAnimeSeries())
            .Distinct()
            .Where(ser => ser != null && user.AllowedSeries(ser)).Select(a => _seriesFactory.GetSeries(a)).ToList();
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

        if (a?.Contract?.AniDBAnime?.AniDBAnime.AllTitles == null)
        {
            return;
        }

        var match = string.Empty;
        foreach (var title in a.Contract.AniDBAnime.AnimeTitles.Select(b => b.Title).ToList())
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
}
