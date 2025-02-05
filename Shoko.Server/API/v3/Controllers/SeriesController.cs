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
using Quartz;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.API.v3.Models.TMDB.Input;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Scheduling.Jobs.Trakt;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using EpisodeType = Shoko.Server.API.v3.Models.Shoko.EpisodeType;
using AniDBEpisodeType = Shoko.Models.Enums.EpisodeType;
using DataSource = Shoko.Server.API.v3.Models.Common.DataSource;
using TmdbEpisode = Shoko.Server.API.v3.Models.TMDB.Episode;
using TmdbMovie = Shoko.Server.API.v3.Models.TMDB.Movie;
using TmdbSearch = Shoko.Server.API.v3.Models.TMDB.Search;
using TmdbSeason = Shoko.Server.API.v3.Models.TMDB.Season;
using TmdbShow = Shoko.Server.API.v3.Models.TMDB.Show;

#pragma warning disable CA1822
#nullable enable
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class SeriesController : BaseController
{
    private readonly AnimeSeriesService _seriesService;

    private readonly AnimeGroupService _groupService;

    private readonly AniDBTitleHelper _titleHelper;

    private readonly ISchedulerFactory _schedulerFactory;

    private readonly TmdbLinkingService _tmdbLinkingService;

    private readonly TmdbMetadataService _tmdbMetadataService;


    private readonly TmdbSearchService _tmdbSearchService;

    private readonly CrossRef_File_EpisodeRepository _crossRefFileEpisode;

    private readonly WatchedStatusService _watchedService;

    public SeriesController(
        ISettingsProvider settingsProvider,
        AnimeSeriesService seriesService,
        AnimeGroupService groupService,
        AniDBTitleHelper titleHelper,
        ISchedulerFactory schedulerFactory,
        TmdbLinkingService tmdbLinkingService,
        TmdbMetadataService tmdbMetadataService,
        TmdbSearchService tmdbSearchService,
        CrossRef_File_EpisodeRepository crossRefFileEpisode,
        WatchedStatusService watchedService
    ) : base(settingsProvider)
    {
        _seriesService = seriesService;
        _groupService = groupService;
        _titleHelper = titleHelper;
        _schedulerFactory = schedulerFactory;
        _tmdbLinkingService = tmdbLinkingService;
        _tmdbMetadataService = tmdbMetadataService;
        _tmdbSearchService = tmdbSearchService;
        _crossRefFileEpisode = crossRefFileEpisode;
        _watchedService = watchedService;
    }

    #region Return messages

    internal const string SeriesNotFoundWithSeriesID = "No Series entry for the given seriesID";

    internal const string SeriesNotFoundWithAnidbID = "No Series entry for the given anidbID";

    internal const string SeriesForbiddenForUser = "Accessing Series is not allowed for the current user";

    internal const string AnidbNotFoundForSeriesID = "No Series.AniDB entry for the given seriesID";

    internal const string AnidbNotFoundForAnidbID = "No Series.AniDB entry for the given anidbID";

    internal const string AnidbForbiddenForUser = "Accessing Series.AniDB is not allowed for the current user";

    internal const string TmdbNotFoundForSeriesID = "No TMDB.Show entry for the given seriesID";

    internal const string TmdbForbiddenForUser = "Accessing TMDB.Show is not allowed for the current user";

    internal const string TraktShowNotFound = "No Trakt.Show entry for the given showID";

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
    public ActionResult<Series> GetSeries([FromRoute, Range(1, int.MaxValue)] int seriesID, [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null)
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
    public async Task<ActionResult> DeleteSeries([FromRoute, Range(1, int.MaxValue)] int seriesID, [FromQuery] bool deleteFiles = false, [FromQuery] bool completelyRemove = false)
    {
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
    public ActionResult OverrideSeriesTitle([FromRoute, Range(1, int.MaxValue)] int seriesID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Series.Input.TitleOverrideBody body)
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        if (!string.Equals(series.SeriesNameOverride, body.Title))
        {
            series.SeriesNameOverride = body.Title;
            series.ResetPreferredTitle();
            series.ResetAnimeTitles();

            RepoFactory.AnimeSeries.Save(series, true);

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
    public ActionResult<Series.AutoMatchSettings> GetAutoMatchSettingsBySeriesID([FromRoute, Range(1, int.MaxValue)] int seriesID)
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
    public ActionResult<Series.AutoMatchSettings> PatchAutoMatchSettingsBySeriesID([FromRoute, Range(1, int.MaxValue)] int seriesID, [FromBody] JsonPatchDocument<Series.AutoMatchSettings> patchDocument)
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
    public ActionResult<Series.AutoMatchSettings> PutAutoMatchSettingsBySeriesID([FromRoute, Range(1, int.MaxValue)] int seriesID, [FromBody] Series.AutoMatchSettings autoMatchSettings)
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
    public ActionResult<List<SeriesRelation>> GetShokoRelationsBySeriesID([FromRoute, Range(1, int.MaxValue)] int seriesID)
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
        return RepoFactory.AniDB_Anime_Relation.GetByAnimeID(series.AniDB_ID).OfType<IRelatedMetadata>()
            .Concat(RepoFactory.AniDB_Anime_Relation.GetByRelatedAnimeID(series.AniDB_ID).OfType<IRelatedMetadata>().Select(a => a.Reversed))
            .Distinct()
            .Select(relation => (relation, relatedSeries: RepoFactory.AnimeSeries.GetByAnimeID(relation.RelatedID)))
            .Where(tuple => tuple.relatedSeries != null)
            .OrderBy(tuple => tuple.relation.BaseID)
            .ThenBy(tuple => tuple.relation.RelatedID)
            .ThenBy(tuple => tuple.relation.RelationType)
            .Select(tuple => new SeriesRelation(tuple.relation, series, tuple.relatedSeries))
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
        [FromQuery] string? search = null,
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
                    series => series.Titles
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
        [FromQuery] string? search = null,
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
                    series => series.Titles
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
            .OfType<IRelatedMetadata>()
            .SelectMany<IRelatedMetadata, IRelatedMetadata>(a => [a, a.Reversed])
            .Distinct()
            .OrderBy(a => a.BaseID)
            .ThenBy(a => a.RelatedID)
            .ThenBy(a => a.RelationType)
            .ToListResult(relation => new SeriesRelation(relation), page, pageSize);
    }

    /// <summary>
    /// Get AniDB Info for series with ID
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <returns></returns>
    [HttpGet("{seriesID}/AniDB")]
    public ActionResult<Series.AniDB> GetSeriesAnidbBySeriesID([FromRoute, Range(1, int.MaxValue)] int seriesID)
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
    public ActionResult<List<Series.AniDB>> GetAnidbSimilarBySeriesID([FromRoute, Range(1, int.MaxValue)] int seriesID)
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
    public ActionResult<List<Series.AniDB>> GetAnidbRelatedBySeriesID([FromRoute, Range(1, int.MaxValue)] int seriesID)
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

        var anidb = series.AniDB_Anime;
        if (anidb == null)
        {
            return InternalError(AnidbNotFoundForSeriesID);
        }

        return RepoFactory.AniDB_Anime_Relation.GetByAnimeID(anidb.AnimeID).OfType<IRelatedMetadata>()
            .Concat(RepoFactory.AniDB_Anime_Relation.GetByRelatedAnimeID(anidb.AnimeID).OfType<IRelatedMetadata>().Select(a => a.Reversed))
            .Distinct()
            .OrderBy(a => a.BaseID)
            .ThenBy(a => a.RelatedID)
            .ThenBy(a => a.RelationType)
            .Select(relation => new Series.AniDB(relation))
            .ToList();
    }

    /// <summary>
    /// Get all AniDB relations for the <paramref name="seriesID"/>.
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <returns></returns>
    [HttpGet("{seriesID}/AniDB/Relations")]
    public ActionResult<List<SeriesRelation>> GetAnidbRelationsBySeriesID([FromRoute, Range(1, int.MaxValue)] int seriesID)
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

        var anidb = series.AniDB_Anime;
        if (anidb == null)
        {
            return InternalError(AnidbNotFoundForSeriesID);
        }

        return RepoFactory.AniDB_Anime_Relation.GetByAnimeID(anidb.AnimeID).OfType<IRelatedMetadata>()
            .Concat(RepoFactory.AniDB_Anime_Relation.GetByRelatedAnimeID(anidb.AnimeID).OfType<IRelatedMetadata>().Select(a => a.Reversed))
            .Distinct()
            .OrderBy(a => a.BaseID)
            .ThenBy(a => a.RelatedID)
            .ThenBy(a => a.RelationType)
            .Select(relation => new SeriesRelation(relation, series))
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
            if (endDate!.Value > DateTime.Now)
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
            .Select(userData => userData.VideoLocal)
            .WhereNotNull()
            .Select(file => file.EpisodeCrossReferences.OrderBy(xref => xref.EpisodeOrder).ThenBy(xref => xref.Percentage).FirstOrDefault())
            .WhereNotNull()
            .Select(xref => xref.AnimeEpisode)
            .WhereNotNull()
            .DistinctBy(episode => episode.AnimeSeriesID)
            .Select(episode => episode.AnimeSeries?.AniDB_Anime)
            .WhereNotNull()
            .Where(anime => user.AllowedAnime(anime) && (includeRestricted || !anime.IsRestricted))
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
    private static Dictionary<int, (SVR_AniDB_Anime, SVR_AnimeSeries?)> GetUnwatchedAnime(
        SVR_JMMUser user,
        bool showAll,
        bool includeRestricted = false,
        IEnumerable<SVR_AniDB_Anime>? watchedAnime = null)
    {
        // Get all watched series (reuse if date is not set)
        var watchedSeriesSet = (watchedAnime ?? GetWatchedAnimeForPeriod(user))
            .Select(series => series.AnimeID)
            .ToHashSet();

        if (showAll)
        {
            return RepoFactory.AniDB_Anime.GetAll()
                .Where(anime => user.AllowedAnime(anime) && !watchedSeriesSet.Contains(anime.AnimeID) && (includeRestricted || !anime.IsRestricted))
                .ToDictionary<SVR_AniDB_Anime, int, (SVR_AniDB_Anime, SVR_AnimeSeries?)>(anime => anime.AnimeID,
                    anime => (anime, null));
        }

        return RepoFactory.AnimeSeries.GetAll()
            .Where(series => user.AllowedSeries(series) && !watchedSeriesSet.Contains(series.AniDB_ID))
            .Select(series => (anime: series.AniDB_Anime, series))
            .Where(tuple => tuple.anime is not null && (includeRestricted || !tuple.anime.IsRestricted))
            .ToDictionary<(SVR_AniDB_Anime? anime, SVR_AnimeSeries series), int, (SVR_AniDB_Anime, SVR_AnimeSeries?)>(tuple => tuple.anime!.AnimeID, tuple => (tuple.anime!, tuple.series));
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
            .Select(relation => new SeriesRelation(relation))
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
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null)
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
    /// <param name="skipTmdbUpdate">Skip updating related TMDB entities after refresh.</param>
    /// <returns>True if the refresh was performed at once, otherwise false if it was queued.</returns>
    [HttpPost("AniDB/{anidbID}/Refresh")]
    public async Task<ActionResult<bool>> RefreshAniDBByAniDBID([FromRoute] int anidbID, [FromQuery] bool force = false,
        [FromQuery] bool downloadRelations = false, [FromQuery] bool? createSeriesEntry = null,
        [FromQuery] bool immediate = false, [FromQuery] bool cacheOnly = false,
        [FromQuery] bool skipTmdbUpdate = false)
    {
        if (!createSeriesEntry.HasValue)
        {
            var settings = SettingsProvider.GetSettings();
            createSeriesEntry = settings.AniDb.AutomaticallyImportSeries;
        }

        // TODO No
        return await _seriesService.QueueAniDBRefresh(anidbID, force, downloadRelations,
            createSeriesEntry.Value, immediate, cacheOnly, skipTmdbUpdate);
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
    /// <param name="skipTmdbUpdate">Skip updating related TMDB entities after refresh.</param>
    /// <returns>True if the refresh is done, otherwise false if it was queued.</returns>
    [HttpPost("{seriesID}/AniDB/Refresh")]
    public async Task<ActionResult<bool>> RefreshAniDBBySeriesID([FromRoute, Range(1, int.MaxValue)] int seriesID, [FromQuery] bool force = false,
        [FromQuery] bool downloadRelations = false, [FromQuery] bool? createSeriesEntry = null,
        [FromQuery] bool immediate = false, [FromQuery] bool cacheOnly = false, [FromQuery] bool skipTmdbUpdate = false)
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

        var anidb = series.AniDB_Anime;
        if (anidb == null)
        {
            return InternalError(AnidbNotFoundForSeriesID);
        }

        // TODO No
        return await _seriesService.QueueAniDBRefresh(anidb.AnimeID, force, downloadRelations,
            createSeriesEntry.Value, immediate, cacheOnly, skipTmdbUpdate);
    }

    #endregion

    #region TMDB

    /// <summary>
    /// Automagically search for one or more matches for the Shoko Series by ID,
    /// and return the results.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <returns>Void.</returns>
    [HttpGet("{seriesID}/TMDB/Action/AutoSearch")]
    public async Task<ActionResult<List<TmdbSearch.AutoMatchResult>>> PreviewAutoMatchTMDBMoviesBySeriesID(
        [FromRoute, Range(1, int.MaxValue)] int seriesID
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        var anime = series.AniDB_Anime;
        if (anime is null)
            return InternalError($"Unable to get Series.AniDB with ID {series.AniDB_ID} for Series with ID {series.AnimeSeriesID}!");

        var results = await _tmdbSearchService.SearchForAutoMatch(anime);

        return results.Select(r => new TmdbSearch.AutoMatchResult(r)).ToList();
    }

    /// <summary>
    /// Schedule an automagically search for one or more matches for the Shoko
    /// Series by ID to take place in the background.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <param name="force">Forcefully update the metadata of the matched entities.</param>
    /// <returns>Void.</returns>
    [HttpPost("{seriesID}/TMDB/Action/AutoSearch")]
    public async Task<ActionResult> ScheduleAutoMatchTMDBMoviesBySeriesID(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery] bool force = false
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        await _tmdbMetadataService.ScheduleSearchForMatch(series.AniDB_ID, force);

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
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbMovie.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        return series.TmdbMovieCrossReferences
            .DistinctBy(o => o.TmdbMovieID)
            .Select(xref =>
            {
                var movie = xref.TmdbMovie;
                if (movie is not null && (TmdbMetadataService.Instance?.WaitForMovieUpdate(movie.TmdbMovieID) ?? false))
                    movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movie.TmdbMovieID);
                return movie;
            })
            .WhereNotNull()
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
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Series.Input.LinkMovieBody body
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        if (RepoFactory.AniDB_Episode.GetByEpisodeID(body.EpisodeID) is not { } episode)
            return ValidationProblem("Episode not found.", nameof(body.EpisodeID));

        if (episode.AnimeID != series.AniDB_ID)
            return ValidationProblem("Episode does not belong to the series.", nameof(body.EpisodeID));

        await _tmdbLinkingService.AddMovieLinkForEpisode(body.EpisodeID, body.ID, additiveLink: !body.Replace);

        var needRefresh = RepoFactory.TMDB_Movie.GetByTmdbMovieID(body.ID) is null || body.Refresh;
        if (needRefresh)
            await _tmdbMetadataService.ScheduleUpdateOfMovie(body.ID, forceRefresh: body.Refresh, downloadImages: true);

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
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] Series.Input.UnlinkMovieBody body
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);
        var episodeIDs = series.AllAnimeEpisodes.Select(e => e.AniDB_EpisodeID).ToList();
        if (body.EpisodeID > 0)
        {
            if (!episodeIDs.Contains(body.EpisodeID))
                return ValidationProblem("The specified episode is not part of the series.", nameof(body.EpisodeID));
            episodeIDs = [body.EpisodeID];
        }

        foreach (var episodeID in episodeIDs)
        {
            if (body != null && body.ID > 0)
                await _tmdbLinkingService.RemoveMovieLinkForEpisode(episodeID, body.ID, body.Purge);
            else
                await _tmdbLinkingService.RemoveAllMovieLinksForEpisode(episodeID, body?.Purge ?? false);
        }

        return NoContent();
    }

    /// <summary>
    /// Refresh all TMDB Movies linked to the Shoko Series by ID.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <param name="body">Body containing options for refreshing or downloading metadata.</param>
    /// <returns>
    /// If <paramref name="body.Immediate"/> is <see langword="true"/>, returns an <see cref="OkResult"/>,
    /// otherwise returns a <see cref="NoContentResult"/>.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("{seriesID}/TMDB/Movie/Action/Refresh")]
    public async Task<ActionResult> RefreshTMDBMoviesBySeriesID(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] TmdbRefreshMovieBody body
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        if (body.Immediate)
        {
            var settings = SettingsProvider.GetSettings();
            await Task.WhenAll(
                RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID(series.AniDB_ID)
                    .Select(xref => body.SkipIfExists && RepoFactory.TMDB_Movie.GetByTmdbMovieID(xref.TmdbMovieID) is not null
                        ? Task.CompletedTask
                        : _tmdbMetadataService.UpdateMovie(xref.TmdbMovieID, body.Force, body.DownloadImages, body.DownloadCrewAndCast ?? settings.TMDB.AutoDownloadCrewAndCast, body.DownloadCollections ?? settings.TMDB.AutoDownloadCollections)
                    )
            );
            return Ok();
        }

        foreach (var xref in RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID(series.AniDB_ID))
            await _tmdbMetadataService.ScheduleUpdateOfMovie(xref.TmdbMovieID, body.Force, body.DownloadImages, body.DownloadCrewAndCast, body.DownloadCollections);

        return NoContent();
    }

    /// <summary>
    /// Download all images for all TMDB Movies linked to the Shoko Series by ID.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <param name="body">Body containing options for refreshing or downloading metadata.</param>
    /// <returns>
    /// If <paramref name="body.Immediate"/> is <see langword="true"/>, returns an <see cref="OkResult"/>,
    /// otherwise returns a <see cref="NoContentResult"/>.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("{seriesID}/TMDB/Movie/Action/DownloadImages")]
    public async Task<ActionResult> DownloadTMDBMovieImagesBySeriesID(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] TmdbDownloadImagesBody body
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        if (body.Immediate)
        {
            await Task.WhenAll(
                RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID(series.AniDB_ID)
                    .Select(xref => _tmdbMetadataService.DownloadAllMovieImages(xref.TmdbMovieID, body.Force))
            );
            return Ok();
        }

        foreach (var xref in RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID(series.AniDB_ID))
            await _tmdbMetadataService.ScheduleDownloadAllMovieImages(xref.TmdbMovieID, body.Force);
        return NoContent();
    }

    /// <summary>
    /// Get all TMDB Movie cross-references for the Shoko Series by ID.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <returns>All TMDB Movie cross-references for the Shoko Series.</returns>
    [HttpGet("{seriesID}/TMDB/Movie/CrossReferences")]
    public ActionResult<IReadOnlyList<TmdbMovie.CrossReference>> GetTMDBMovieCrossReferenceBySeriesID(
        [FromRoute, Range(1, int.MaxValue)] int seriesID
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
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbShow.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        return series.TmdbShowCrossReferences
            .Select(o => o.TmdbShow)
            .WhereNotNull()
            .Select(o =>
            {
                if (_tmdbMetadataService.WaitForShowUpdate(o.Id))
                    o = RepoFactory.TMDB_Show.GetByTmdbShowID(o.Id) ?? o;
                return new TmdbShow(o, o.PreferredAlternateOrdering, include?.CombineFlags(), language);
            })
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
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Series.Input.LinkShowBody body
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        await _tmdbLinkingService.AddShowLink(series.AniDB_ID, body.ID, additiveLink: !body.Replace);

        var needRefresh = body.Refresh || RepoFactory.TMDB_Show.GetByTmdbShowID(body.ID) is not { } tmdbShow || tmdbShow.CreatedAt == tmdbShow.LastUpdatedAt;
        if (needRefresh)
            await _tmdbMetadataService.ScheduleUpdateOfShow(body.ID, forceRefresh: body.Refresh, downloadImages: true);

        // Reset series/group titles/descriptions when a new link is added.
        series.ResetAnimeTitles();
        series.ResetPreferredTitle();
        series.ResetPreferredOverview();
        _groupService.UpdateStatsFromTopLevel(series?.AnimeGroup?.TopLevelAnimeGroup, false, false);

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
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] Series.Input.UnlinkCommonBody body
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        if (body != null && body.ID > 0)
            await _tmdbLinkingService.RemoveShowLink(series.AniDB_ID, body.ID, body.Purge);
        else
            await _tmdbLinkingService.RemoveAllShowLinksForAnime(series.AniDB_ID, body?.Purge ?? false);

        // Reset series/group titles/descriptions when a link is removed.
        series.ResetAnimeTitles();
        series.ResetPreferredTitle();
        series.ResetPreferredOverview();
        _groupService.UpdateStatsFromTopLevel(series?.AnimeGroup?.TopLevelAnimeGroup, false, false);


        return NoContent();
    }

    /// <summary>
    /// Refresh all TMDB Shows linked to the Shoko Series by ID.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <param name="body">Body containing options for refreshing or downloading metadata.</param>
    /// <returns>
    /// If <paramref name="body.Immediate"/> is <see langword="true"/>, returns an <see cref="OkResult"/>,
    /// otherwise returns a <see cref="NoContentResult"/>.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("{seriesID}/TMDB/Show/Action/Refresh")]
    public async Task<ActionResult> RefreshTMDBShowsBySeriesID(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] TmdbRefreshShowBody body
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        if (body.Immediate)
        {
            var settings = SettingsProvider.GetSettings();
            await Task.WhenAll(
                RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID(series.AniDB_ID)
                    .Select(xref => _tmdbMetadataService.UpdateShow(xref.TmdbShowID, body.Force, body.DownloadImages, body.DownloadCrewAndCast ?? settings.TMDB.AutoDownloadCrewAndCast, body.DownloadAlternateOrdering ?? settings.TMDB.AutoDownloadAlternateOrdering, body.QuickRefresh)
                    )
            );
            return Ok();
        }

        foreach (var xref in RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID(series.AniDB_ID))
            await _tmdbMetadataService.ScheduleUpdateOfShow(xref.TmdbShowID, body.Force, body.DownloadImages, body.DownloadCrewAndCast, body.DownloadAlternateOrdering);

        return NoContent();
    }

    /// <summary>
    /// Download all images for all TMDB Shows linked to the Shoko Series by ID.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <param name="body">Body containing options for refreshing or downloading metadata.</param>
    /// <returns>
    /// If <paramref name="body.Immediate"/> is <see langword="true"/>, returns an <see cref="OkResult"/>,
    /// otherwise returns a <see cref="NoContentResult"/>.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("{seriesID}/TMDB/Show/Action/DownloadImages")]
    public async Task<ActionResult> DownloadTMDBShowImagesBySeriesID(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] TmdbDownloadImagesBody body
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        if (body.Immediate)
        {
            await Task.WhenAll(
                RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID(series.AniDB_ID)
                    .Select(xref => _tmdbMetadataService.DownloadAllShowImages(xref.TmdbShowID, body.Force))
            );
            return Ok();
        }

        foreach (var xref in RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID(series.AniDB_ID))
            await _tmdbMetadataService.ScheduleDownloadAllShowImages(xref.TmdbShowID, body.Force);
        return NoContent();
    }

    /// <summary>
    /// Get all TMDB Show cross-references for the Shoko Series by ID.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID.</param>
    /// <returns>All TMDB Show cross-references for the Shoko Series.</returns>
    [HttpGet("{seriesID}/TMDB/Show/CrossReferences")]
    public ActionResult<IReadOnlyList<TmdbShow.CrossReference>> GetTMDBShowCrossReferenceBySeriesID(
        [FromRoute, Range(1, int.MaxValue)] int seriesID
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
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery, Range(0, int.MaxValue)] int? tmdbShowID,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(TmdbNotFoundForSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(TmdbForbiddenForUser);

        if (tmdbShowID.HasValue && tmdbShowID.Value > 0)
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
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Series.Input.OverrideTmdbEpisodeMappingBody body
    )
    {
        if (body == null || (body.Mapping.Count == 0 && !body.UnsetAll))
            return ValidationProblem("Empty body.");

        if (body.Mapping.Count > 0)
        {
            body.Mapping = body.Mapping.DistinctBy(x => (x.AniDBID, x.TmdbID)).ToList();
        }

        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(TmdbNotFoundForSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(TmdbForbiddenForUser);

        // Validate the mappings.
        var xrefs = series.TmdbShowCrossReferences;
        var showIDs = xrefs
            .Select(xref => xref.TmdbShowID)
            .ToHashSet();
        var missingIDs = new HashSet<int>();
        var mapping = new List<(Series.Input.OverrideTmdbEpisodeLinkBody link, SVR_AniDB_Episode aniDBEpisode)>();
        foreach (var link in body.Mapping)
        {
            var shokoEpisode = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(link.AniDBID);
            var anidbEpisode = shokoEpisode?.AniDB_Episode;
            if (anidbEpisode is null)
            {
                ModelState.AddModelError("Mapping", $"Unable to find an AniDB Episode with id '{link.AniDBID}'");
                continue;
            }
            if (shokoEpisode is null || shokoEpisode.AnimeSeriesID != series.AnimeSeriesID)
            {
                ModelState.AddModelError("Mapping", $"The AniDB Episode with id '{link.AniDBID}' is not part of the series.");
                continue;
            }
            var tmdbEpisode = link.TmdbID == 0 ? null : RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(link.TmdbID);
            if (link.TmdbID != 0)
            {
                if (tmdbEpisode is null)
                {
                    ModelState.AddModelError("Mapping", $"Unable to find TMDB Episode with the id '{link.TmdbID}' locally.");
                    continue;
                }
                if (!showIDs.Contains(tmdbEpisode.TmdbShowID))
                    missingIDs.Add(tmdbEpisode.TmdbShowID);
            }

            mapping.Add((link, anidbEpisode));
        }
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Add any missing links if needed.
        foreach (var showId in missingIDs)
            await _tmdbLinkingService.AddShowLink(series.AniDB_ID, showId, additiveLink: true);

        // Unset all links if we want to manually replace some or all of them.
        if (body.UnsetAll)
            _tmdbLinkingService.ResetAllEpisodeLinks(series.AniDB_ID, false);

        // Make sure the mappings are in the correct order before linking.
        mapping = mapping
            .OrderByDescending(x => x.link.Replace)
            .ThenBy(x => x.aniDBEpisode.EpisodeTypeEnum)
            .ThenBy(x => x.aniDBEpisode.EpisodeNumber)
            .ToList();

        // Do the actual linking.
        foreach (var (link, _) in mapping)
            _tmdbLinkingService.SetEpisodeLink(link.AniDBID, link.TmdbID, !link.Replace, link.Index);

        var scheduled = false;
        foreach (var showId in missingIDs)
            if (RepoFactory.TMDB_Show.GetByTmdbShowID(showId) is not { } tmdbShow || tmdbShow.CreatedAt == tmdbShow.LastUpdatedAt)
            {
                scheduled = true;
                await _tmdbMetadataService.ScheduleUpdateOfShow(showId, downloadImages: true);
            }

        if (scheduled)
            return Created();

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
    /// <param name="keepExisting">Determines whether to retain existing links when picking episodes.</param>
    /// <param name="considerExistingOtherLinks">Determines whether to consider existing links for other series when picking episodes.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <returns>A preview of the automagically matched episodes.</returns>
    [Authorize("admin")]
    [HttpGet("{seriesID}/TMDB/Show/CrossReferences/Episode/Auto")]
    public ActionResult<ListResult<TmdbEpisode.CrossReference>> PreviewAutoTMDBEpisodeMappingsBySeriesID(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery] int? tmdbShowID,
        [FromQuery] int? tmdbSeasonID,
        [FromQuery] bool keepExisting = true,
        [FromQuery] bool? considerExistingOtherLinks = null,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(TmdbNotFoundForSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(TmdbForbiddenForUser);

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

        return _tmdbLinkingService.MatchAnidbToTmdbEpisodes(series.AniDB_ID, tmdbShowID.Value, tmdbSeasonID, useExisting: keepExisting, useExistingOtherShows: considerExistingOtherLinks, saveToDatabase: false)
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
    /// <param name="body">Optional. Any auto-match options.</param>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpPost("{seriesID}/TMDB/Show/CrossReferences/Episode/Auto")]
    public async Task<ActionResult> AutoTMDBEpisodeMappingsBySeriesID(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] Series.Input.AutoMatchTmdbEpisodesBody? body = null
    )
    {
        body ??= new();
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(TmdbNotFoundForSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(TmdbForbiddenForUser);

        var isMissing = false;
        var xrefs = series.TmdbShowCrossReferences;
        if (body.TmdbShowID.HasValue)
        {
            isMissing = !xrefs.Any(s => s.TmdbShowID == body.TmdbShowID.Value);
        }
        else
        {
            var xref = xrefs.Count > 0 ? xrefs[0] : null;
            if (xref == null)
                return ValidationProblem("Unable to find an existing cross-reference for the series to use. Make sure at least one TMDB Show is linked to the Shoko Series.", "tmdbShowID");

            body.TmdbShowID = xref.TmdbShowID;
        }

        // Hard bail if the TMDB show isn't locally available.
        if (RepoFactory.TMDB_Show.GetByTmdbShowID(body.TmdbShowID.Value) is not { } tmdbShow)
            return ValidationProblem("Unable to find the selected TMDB Show locally. Add the TMDB Show locally first.", "tmdbShowID");

        if (body.TmdbSeasonID.HasValue)
        {
            var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(body.TmdbSeasonID.Value);
            if (season == null)
                return ValidationProblem("Unable to find existing TMDB Season with the given season ID.", "tmdbSeasonID");

            if (season.TmdbShowID != body.TmdbShowID.Value)
                return ValidationProblem("The selected tmdbSeasonID does not belong to the selected tmdbShowID", "tmdbSeasonID");
        }

        // Add the missing link if needed.
        if (isMissing)
            await _tmdbLinkingService.AddShowLink(series.AniDB_ID, body.TmdbShowID.Value, additiveLink: true);
        else
            _tmdbLinkingService.MatchAnidbToTmdbEpisodes(series.AniDB_ID, body.TmdbShowID.Value, body.TmdbSeasonID, useExisting: body.KeepExisting, useExistingOtherShows: body.ConsiderExistingOtherLinks, saveToDatabase: true);

        if (tmdbShow.CreatedAt == tmdbShow.LastUpdatedAt)
        {
            await _tmdbMetadataService.ScheduleUpdateOfShow(tmdbShow.Id, downloadImages: true);
            return Created();
        }

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
        [FromRoute, Range(1, int.MaxValue)] int seriesID
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(TmdbNotFoundForSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(TmdbForbiddenForUser);

        _tmdbLinkingService.ResetAllEpisodeLinks(series.AniDB_ID, true);

        return NoContent();
    }

    /// <summary>
    /// Shows all existing episode cross-references for a Shoko Series grouped
    /// by their corresponding cross-reference groups. Optionally allows
    /// filtering it to a specific TMDB show. 
    /// </summary>
    /// <param name="seriesID">The Shoko Series ID.</param>
    /// <param name="tmdbShowID">The TMDB Show ID to filter the episode mappings. If not specified, mappings for any show may be included.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <returns>The list of grouped episode cross-references.</returns>
    [HttpGet("{seriesID}/TMDB/Show/CrossReferences/EpisodeGroups")]
    public ActionResult<ListResult<List<TmdbEpisode.CrossReference>>> GetGroupedTMDBEpisodeMappingsBySeriesID(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery] int? tmdbShowID,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(TmdbNotFoundForSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(TmdbForbiddenForUser);

        if (tmdbShowID.HasValue)
        {
            var xrefs = series.TmdbShowCrossReferences;
            var xref = xrefs.FirstOrDefault(s => s.TmdbShowID == tmdbShowID.Value);
            if (xref == null)
                return ValidationProblem("Unable to find an existing cross-reference for the given TMDB Show ID. Please first link the TMDB Show to the Shoko Series.", "tmdbShowID");
        }

        return series.GetTmdbEpisodeCrossReferences(tmdbShowID)
            .GroupByCrossReferenceType()
            .ToListResult(list => list.Select((xref, index) => new TmdbEpisode.CrossReference(xref, index)).ToList(), page, pageSize);
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
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbSeason.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(TmdbNotFoundForSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(TmdbForbiddenForUser);

        return series.TmdbEpisodeCrossReferences
            .Select(o => o.TmdbEpisode)
            .WhereNotNull()
            .DistinctBy(o => o.TmdbSeasonID)
            .Select(o =>
            {
                var season = o.TmdbSeason;
                if (season is not null && _tmdbMetadataService.WaitForShowUpdate(season.TmdbShowID))
                    season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(season.TmdbSeasonID);
                return season;
            })
            .WhereNotNull()
            .OrderBy(season => season.TmdbShowID)
            .ThenBy(season => season.SeasonNumber)
            .Select(o => new TmdbSeason(o, include?.CombineFlags(), language))
            .ToList();
    }

    #endregion

    #endregion

    #region Trakt

    /// <summary>
    /// Queue a job for refreshing series data from Trakt
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <returns></returns>
    [HttpPost("{seriesID}/Trakt/Refresh")]
    public async Task<ActionResult> RefreshTraktBySeriesID([FromRoute, Range(1, int.MaxValue)] int seriesID)
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

        var anidb = series.AniDB_Anime;
        if (anidb == null)
        {
            return InternalError(AnidbNotFoundForSeriesID);
        }

        var traktShows = series.TraktShow;
        if (traktShows.Count == 0)
        {
            return ValidationProblem(TraktShowNotFound);
        }

        var scheduler = await _schedulerFactory.GetScheduler();

        foreach (var show in traktShows)
        {
            await scheduler.StartJob<UpdateTraktShowJob>(c => c.TraktShowID = show.TraktID);
        }

        return Ok();
    }

    /// <summary>
    /// Queue a job for syncing series status to Trakt
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <returns></returns>
    [HttpPost("{seriesID}/Trakt/Sync")]
    public async Task<ActionResult> SyncTraktBySeriesID([FromRoute, Range(1, int.MaxValue)] int seriesID)
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

        var anidb = series.AniDB_Anime;
        if (anidb == null)
        {
            return InternalError(AnidbNotFoundForSeriesID);
        }

        var traktShows = series.TraktShow;
        if (traktShows.Count == 0)
        {
            return ValidationProblem(TraktShowNotFound);
        }

        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<SyncTraktCollectionSeriesJob>(c => c.AnimeSeriesID = seriesID);
        return Ok();
    }

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
    /// <param name="includeUnaired">Include unaired episodes in the list.</param>
    /// <param name="includeHidden">Include hidden episodes in the list.</param>
    /// <param name="includeWatched">Include watched episodes in the list.</param>
    /// <param name="includeManuallyLinked">Include manually linked episodes in the list.</param>
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
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery, Range(0, 1000)] int pageSize = 20,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] IncludeOnlyFilter includeMissing = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeUnaired = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeHidden = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeWatched = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter includeManuallyLinked = IncludeOnlyFilter.True,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeType>? type = null,
        [FromQuery] bool includeFiles = false,
        [FromQuery] bool includeMediaInfo = false,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] string? search = null,
        [FromQuery] bool fuzzy = true
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        return GetEpisodesInternal(series, includeMissing, includeUnaired, includeHidden, includeWatched, includeManuallyLinked, type, search, fuzzy)
            .ToListResult(a => new Episode(HttpContext, a, includeDataFrom, includeFiles, includeMediaInfo, includeAbsolutePaths, includeXRefs), page, pageSize);
    }

    /// <summary>
    /// Toggles the watched state for all the episodes that fit the query.
    /// </summary>
    /// <param name="seriesID">Series ID</param>
    /// <param name="value">The new watched state.</param>
    /// <param name="includeMissing">Include missing episodes in the list.</param>
    /// <param name="includeUnaired">Include unaired episodes in the list.</param>
    /// <param name="includeHidden">Include hidden episodes in the list.</param>
    /// <param name="includeWatched">Include watched episodes in the list.</param>
    /// <param name="type">Filter episodes by the specified <see cref="EpisodeType"/>s.</param>
    /// <param name="search">An optional search query to filter episodes based on their titles.</param>
    /// <param name="fuzzy">Indicates that fuzzy-matching should be used for the search query.</param>
    /// <returns></returns>
    [HttpPost("{seriesID}/Episode/Watched")]
    public async Task<ActionResult> MarkSeriesWatched(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery] bool value = true,
        [FromQuery] IncludeOnlyFilter includeMissing = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeUnaired = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeHidden = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeWatched = IncludeOnlyFilter.True,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeType>? type = null,
        [FromQuery] string? search = null,
        [FromQuery] bool fuzzy = true)
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        var userId = User.JMMUserID;
        var now = DateTime.Now;
        // this has a parallel query to evaluate filters and data in parallel, but that makes awaiting the SetWatchedStatus calls more difficult, so we ToList() it
        await Task.WhenAll(GetEpisodesInternal(series, includeMissing, includeUnaired, includeHidden, includeWatched, IncludeOnlyFilter.True, type, search, fuzzy).ToList()
            .Select(episode => _watchedService.SetWatchedStatus(episode, value, true, now, false, userId, true)));

        _seriesService.UpdateStats(series, true, false);

        return Ok();
    }

    [NonAction]
    public ParallelQuery<SVR_AnimeEpisode> GetEpisodesInternal(
        SVR_AnimeSeries series,
        IncludeOnlyFilter includeMissing,
        IncludeOnlyFilter includeUnaired,
        IncludeOnlyFilter includeHidden,
        IncludeOnlyFilter includeWatched,
        IncludeOnlyFilter includeManuallyLinked,
        HashSet<EpisodeType>? type,
        string? search,
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
                    var isMissing = shoko.VideoLocals.Count == 0 && anidb.HasAired;
                    if (shouldHideMissing == isMissing)
                        return false;
                }
                if (includeUnaired != IncludeOnlyFilter.True)
                {
                    // If we should hide unaired episodes and the episode has no files, then hide it.
                    // Or if we should only show unaired episodes and the episode has files, the hide it.
                    var shouldHideUnaired = includeUnaired == IncludeOnlyFilter.False;
                    var isUnaired = shoko.VideoLocals.Count == 0 && !anidb.HasAired;
                    if (shouldHideUnaired == isUnaired)
                        return false;
                }

                // Filter by manually linked, if specified
                if (includeManuallyLinked != IncludeOnlyFilter.True)
                {
                    // If we should hide manually linked episodes and the episode is manually linked, then hide it.
                    // Or if we should only show manually linked episodes and the episode is not manually linked, then hide it.
                    var shouldHideManuallyLinked = includeManuallyLinked == IncludeOnlyFilter.False;
                    var isManuallyLinked = shoko.FileCrossReferences.Any(xref => xref.CrossRefSource != (int)CrossRefSource.AniDB);
                    if (shouldHideManuallyLinked == isManuallyLinked)
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
                    ep => RepoFactory.AniDB_Episode_Title.GetByEpisodeID(ep.AniDB!.EpisodeID)
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
            .OrderBy(episode => episode.AniDB!.EpisodeType)
            .ThenBy(episode => episode.AniDB!.EpisodeNumber)
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
    /// <param name="includeUnaired">Include unaired episodes in the list.</param>
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
        [FromQuery] IncludeOnlyFilter includeUnaired = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeHidden = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeWatched = IncludeOnlyFilter.True,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeType>? type = null,
        [FromQuery] string? search = null,
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
                    var isMissing = shoko is not null && shoko.VideoLocals.Count == 0 && anidb.HasAired;
                    if (shouldHideMissing == isMissing)
                        return false;
                }
                if (includeUnaired != IncludeOnlyFilter.True)
                {
                    // If we should hide unaired episodes and the episode has no files, then hide it.
                    // Or if we should only show unaired episodes and the episode has files, the hide it.
                    var shouldHideUnaired = includeUnaired == IncludeOnlyFilter.False;
                    var isUnaired = shoko is not null && shoko.VideoLocals.Count == 0 && !anidb.HasAired;
                    if (shouldHideUnaired == isUnaired)
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
                        .Append(ep.Shoko?.PreferredTitle)
                        .WhereNotDefault()
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
    /// <param name="includeOthers">Include other type episodes in the search.</param>
    /// <param name="includeMissing">Include missing episodes in the list.</param>
    /// <param name="includeUnaired">Include unaired episodes in the list.</param>
    /// <param name="includeRewatching">Include already watched episodes in the
    /// search if we determine the user is "re-watching" the series.</param>
    /// <param name="includeFiles">Include files with the episodes.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeAbsolutePaths">Include absolute paths for the file locations.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("{seriesID}/NextUpEpisode")]
    public ActionResult<Episode> GetNextUnwatchedEpisode([FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery] bool onlyUnwatched = true,
        [FromQuery] bool includeSpecials = true,
        [FromQuery] bool includeOthers = false,
        [FromQuery] bool includeMissing = true,
        [FromQuery] bool includeUnaired = false,
        [FromQuery] bool includeRewatching = false,
        [FromQuery] bool includeFiles = false,
        [FromQuery] bool includeMediaInfo = false,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null)
    {
        if (RepoFactory.AnimeSeries.GetByID(seriesID) is not { } series)
            return NotFound(SeriesNotFoundWithSeriesID);

        var user = User;
        if (!user.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        var episode = _seriesService.GetNextUpEpisode(series, user.JMMUserID, new()
        {
            IncludeCurrentlyWatching = !onlyUnwatched,
            IncludeMissing = includeMissing,
            IncludeUnaired = includeUnaired,
            IncludeRewatching = includeRewatching,
            IncludeSpecials = includeSpecials,
            IncludeOthers = includeOthers,
        });
        if (episode is null)
            return NoContent();

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
    public async Task<ActionResult> RescanSeriesFiles([FromRoute, Range(1, int.MaxValue)] int seriesID, [FromQuery] bool priority = false)
    {
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
    public async Task<ActionResult> RehashSeriesFiles([FromRoute, Range(1, int.MaxValue)] int seriesID)
    {
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
    public async Task<ActionResult> PostSeriesUserVote([FromRoute, Range(1, int.MaxValue)] int seriesID, [FromBody] Vote vote)
    {
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

    private const string InvalidImageTypeForSeries = "Invalid image type for series.";

    private const string InvalidIDForSource = "Invalid image id for selected source.";

    private const string InvalidImageIsDisabled = "Image is disabled.";

    private const string NoDefaultImageForType = "No default image for type.";

    /// <summary>
    /// Get all images for series with ID, optionally with Disabled images, as well.
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <param name="includeDisabled"></param>
    /// <returns></returns>
    [HttpGet("{seriesID}/Images")]
    public ActionResult<Images> GetSeriesImages([FromRoute, Range(1, int.MaxValue)] int seriesID, [FromQuery] bool includeDisabled)
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
    public ActionResult<Image> GetSeriesDefaultImageForType([FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] Image.ImageType imageType)
    {
        if (!_allowedImageTypes.Contains(imageType))
            return BadRequest(InvalidImageTypeForSeries);

        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        var imageEntityType = imageType.ToServer();
        var preferredImage = series.GetPreferredImageForType(imageEntityType);
        if (preferredImage != null)
            return new Image(preferredImage);

        var images = series.GetImages(imageEntityType).ToDto();
        var image = imageEntityType switch
        {
            ImageEntityType.Poster => images.Posters.FirstOrDefault(),
            ImageEntityType.Banner => images.Banners.FirstOrDefault(),
            ImageEntityType.Backdrop => images.Backdrops.FirstOrDefault(),
            ImageEntityType.Logo => images.Logos.FirstOrDefault(),
            _ => null
        };

        if (image is null)
            return NotFound(NoDefaultImageForType);

        return image;
    }


    /// <summary>
    /// Set the default <see cref="Image"/> for the given <paramref name="imageType"/> for the <see cref="Series"/>.
    /// </summary>
    /// <param name="seriesID">Series ID</param>
    /// <param name="imageType">Poster, Banner, Fanart</param>
    /// <param name="body">The body containing the source and id used to set.</param>
    /// <returns></returns>
    [HttpPut("{seriesID}/Images/{imageType}")]
    public ActionResult<Image> SetSeriesDefaultImageForType([FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] Image.ImageType imageType, [FromBody] Image.Input.DefaultImageBody body)
    {
        if (!_allowedImageTypes.Contains(imageType))
            return BadRequest(InvalidImageTypeForSeries);

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
    public ActionResult DeleteSeriesDefaultImageForType([FromRoute, Range(1, int.MaxValue)] int seriesID, [FromRoute] Image.ImageType imageType)
    {
        if (!_allowedImageTypes.Contains(imageType))
            return BadRequest(InvalidImageTypeForSeries);

        // Check if the series exists and if the user can access the series.
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
    /// <param name="includeCount">Include a count of series with each tag.</param>
    /// <returns></returns>
    [HttpGet("{seriesID}/Tags")]
    public ActionResult<List<Tag>> GetSeriesTags(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery] TagFilter.Filter filter = 0,
        [FromQuery] bool excludeDescriptions = false,
        [FromQuery] bool orderByName = false,
        [FromQuery] bool onlyVerified = true,
        [FromQuery] bool includeCount = false)
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

        var anidb = series.AniDB_Anime;
        if (anidb == null)
        {
            return new List<Tag>();
        }

        return Series.GetTags(anidb, filter, excludeDescriptions, orderByName, onlyVerified, includeCount);
    }

    /// <summary>
    /// Get user tags for Series with ID.
    /// </summary>
    /// <param name="seriesID">Shoko ID</param>
    /// <param name="excludeDescriptions">Exclude tag descriptions.</param>
    /// <param name="includeCount">Include a count of series with each tag.</param>
    /// <returns></returns>
    [HttpGet("{seriesID}/Tags/User")]
    public ActionResult<List<Tag>> GetSeriesUserTags(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery] bool excludeDescriptions = false,
        [FromQuery] bool includeCount = false)
        => GetSeriesTags(seriesID, TagFilter.Filter.User | TagFilter.Filter.Invert, excludeDescriptions: excludeDescriptions, orderByName: true, onlyVerified: true, includeCount: includeCount);

    /// <summary>
    /// Add user tags for Series with ID.
    /// </summary>
    /// <param name="seriesID">Shoko ID.</param>
    /// <param name="body">Body containing the user tag ids to add.</param>
    /// <returns>No content if nothing was added, Created if any cross-references were added, otherwise an error action result.</returns>
    [HttpPost("{seriesID}/Tags/User")]
    [Authorize("admin")]
    public ActionResult AddSeriesUserTags(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Series.Input.AddOrRemoveUserTagsBody body
    )
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

        var existingTagIds = RepoFactory.CrossRef_CustomTag.GetByAnimeID(series.AniDB_ID);
        var toAdd = body.IDs
            .Except(existingTagIds.Select(xref => xref.CustomTagID))
            .Select(id => new CrossRef_CustomTag
            {
                CrossRefID = series.AniDB_ID,
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
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Series.Input.AddOrRemoveUserTagsBody body
    )
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

        var existingTagIds = RepoFactory.CrossRef_CustomTag.GetByAnimeID(series.AniDB_ID);
        var toRemove = existingTagIds
            .IntersectBy(body.IDs, xref => xref.CustomTagID)
            .ToList();
        if (toRemove.Count is 0)
            return NoContent();

        RepoFactory.CrossRef_CustomTag.Delete(toRemove);

        return Ok();
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
    public ActionResult<List<Role>> GetSeriesCast([FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<CreatorRoleType>? roleType = null)
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
    public ActionResult MoveSeries([FromRoute, Range(1, int.MaxValue)] int seriesID, [FromRoute, Range(1, int.MaxValue)] int groupID)
    {
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
                    if (local.HasValue && series is null == local.Value)
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
                if (local.HasValue && series is null == local.Value)
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
        return RepoFactory.VideoLocalPlace.GetAll()
            .Where(a =>
            {
                if (a.FullServerPath == null) return false;
                var dir = Path.GetDirectoryName(a.FullServerPath);
                return dir != null && dir.EndsWith(query, StringComparison.OrdinalIgnoreCase);
            })
            .SelectMany(a => a.VideoLocal?.AnimeEpisodes ?? Enumerable.Empty<SVR_AnimeEpisode>())
            .DistinctBy(a => a.AnimeSeriesID)
            .Select(a => a.AnimeSeries)
            .WhereNotNull()
            .Where(user.AllowedSeries)
            .Select(a => new Series(a, User.JMMUserID))
            .ToList();
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
        if (titles is null || titles.Count == 0)
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
    public ActionResult<IEnumerable<int>> GetAllYears()
        => RepoFactory.AnimeSeries.GetAllYears().ToList();

    /// <summary>
    /// Get a list of all years and seasons (2024 Winter) that series that you have aired in. One Piece would return every Season from 1999 Fall to preset (assuming it's still airing *today*)
    /// </summary>
    /// <returns></returns>
    [HttpGet("Seasons")]
    public ActionResult<IEnumerable<YearlySeason>> GetAllSeasons()
        => RepoFactory.AnimeSeries.GetAllSeasons().Select(a => new YearlySeason(a.Year, a.Season)).Order().ToList();
}
