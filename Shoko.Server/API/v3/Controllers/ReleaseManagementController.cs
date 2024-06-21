using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
public class ReleaseManagementController : BaseController
{
    private readonly ILogger<ReleaseManagementController> _logger;
    private readonly SeriesFactory _seriesFactory;

    /// <summary>
    /// Get series with multiple releases.
    /// </summary>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="ignoreVariations">Ignore manually toggled variations in the results.</param>
    /// <param name="onlyFinishedSeries">Only show finished series.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [HttpGet("Series")]
    public ActionResult<ListResult<SeriesWithMultipleReleasesResult>> GetSeriesWithMultipleReleases(
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] bool ignoreVariations = true,
        [FromQuery] bool onlyFinishedSeries = false,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        IEnumerable<SVR_AnimeSeries> enumerable = RepoFactory.AnimeSeries.GetWithMultipleReleases(ignoreVariations);
        if (onlyFinishedSeries) enumerable = enumerable.Where(a => a.AniDB_Anime.GetFinishedAiring());

        return enumerable
            .OrderBy(series => series.SeriesName)
            .ThenBy(series => series.AniDB_ID)
            .ToListResult(series => _seriesFactory.GetSeriesWithMultipleReleasesResult(series, false, includeDataFrom, ignoreVariations), page, pageSize);
    }

    /// <summary>
    /// Get episodes with multiple files attached.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="includeFiles">Include files with the episodes.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeAbsolutePaths">Include absolute paths for the file locations.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
    /// <param name="ignoreVariations">Ignore manually toggled variations in the results.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [HttpGet("Series/{seriesID}")]
    public ActionResult<ListResult<Episode>> GetEpisodesForSeries(
        [FromRoute] int seriesID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] bool includeFiles = true,
        [FromQuery] bool includeMediaInfo = true,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] bool ignoreVariations = true,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        if (seriesID == 0) return BadRequest(SeriesController.SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return new ListResult<Episode>();

        if (!User.AllowedSeries(series))
            return new ListResult<Episode>();

        IEnumerable<SVR_AnimeEpisode> enumerable = RepoFactory.AnimeEpisode.GetWithMultipleReleases(ignoreVariations, series.AniDB_ID);

        return enumerable
            .ToListResult(episode => new Episode(HttpContext, episode, includeDataFrom, includeFiles, includeMediaInfo, includeAbsolutePaths, includeXRefs), page, pageSize);
    }

    /// <summary>
    /// Get episodes with multiple files attached.
    /// </summary>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="includeFiles">Include files with the episodes.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeAbsolutePaths">Include absolute paths for the file locations.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
    /// <param name="ignoreVariations">Ignore manually toggled variations in the results.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [HttpGet("Episodes")]
    public ActionResult<ListResult<Episode>> GetEpisodes(
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] bool includeFiles = true,
        [FromQuery] bool includeMediaInfo = true,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] bool ignoreVariations = true,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        IEnumerable<SVR_AnimeEpisode> enumerable = RepoFactory.AnimeEpisode.GetWithMultipleReleases(ignoreVariations);

        return enumerable
            .ToListResult(episode => new Episode(HttpContext, episode, includeDataFrom, includeFiles, includeMediaInfo, includeAbsolutePaths, includeXRefs), page, pageSize);
    }

    /// <summary>
    /// Get the list of file ids to remove according to the file quality
    /// preference.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID</param>
    /// <param name="ignoreVariations">Ignore manually toggled variations in the results.</param>
    /// <returns></returns>
    [HttpGet("Series/{seriesID}/Episode/FilesToDelete")]
    public ActionResult<List<int>> GetFileIdsWithPreference(
        [FromRoute] int seriesID,
        [FromQuery] bool ignoreVariations = true
    )
    {
        if (seriesID == 0) return BadRequest(SeriesController.SeriesWithZeroID);
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return new List<int>();

        if (!User.AllowedSeries(series))
            return new List<int>();

        IEnumerable<SVR_AnimeEpisode> enumerable = RepoFactory.AnimeEpisode.GetWithMultipleReleases(ignoreVariations, series.AniDB_ID);

        return enumerable
            .SelectMany(episode =>
            {
                var files = episode.VideoLocals;
                files.Sort(FileQualityFilter.CompareTo);
                return files
                    .Skip(FileQualityFilter.Settings.MaxNumberOfFilesToKeep)
                    .Where(file => !FileQualityFilter.CheckFileKeep(file))
                    .Select(file => file.VideoLocalID);
            })
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Get the list of file ids to remove according to the file quality
    /// preference.
    /// </summary>
    /// <param name="ignoreVariations">Ignore manually toggled variations in the results.</param>
    /// <returns></returns>
    [HttpGet("Episode/FilesToDelete")]
    public ActionResult<List<int>> GetFileIdsWithPreference(
        [FromQuery] bool ignoreVariations = true
    )
    {
        IEnumerable<SVR_AnimeEpisode> enumerable = RepoFactory.AnimeEpisode.GetWithMultipleReleases(ignoreVariations);

        return enumerable
            .SelectMany(episode =>
            {
                var files = episode.VideoLocals;
                files.Sort(FileQualityFilter.CompareTo);
                return files
                    .Skip(FileQualityFilter.Settings.MaxNumberOfFilesToKeep)
                    .Where(file => !FileQualityFilter.CheckFileKeep(file))
                    .Select(file => file.VideoLocalID);
            })
            .Distinct()
            .ToList();
    }

    public ReleaseManagementController(ISettingsProvider settingsProvider, ILogger<ReleaseManagementController> logger, SeriesFactory seriesFactory) : base(settingsProvider)
    {
        _logger = logger;
        _seriesFactory = seriesFactory;
    }
}
