using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

#pragma warning disable CA1822
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/ReleaseManagement/MissingEpisodes")]
[ApiV3]
public class ReleaseManagementMissingEpisodesController(ISettingsProvider settingsProvider) : BaseController(settingsProvider)
{
    /// <summary>
    /// Get missing episodes, be it collecting or otherwise.
    /// </summary>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="includeFiles">Include files with the episodes.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeAbsolutePaths">Include absolute paths for the file locations.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
    /// <param name="collecting">Only show missing episodes from release groups we're collecting.</param>
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
        [FromQuery] bool collecting = false,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        var enumerable = RepoFactory.AnimeEpisode.GetMissing(collecting);

        return enumerable
            .ToListResult(episode => new Episode(HttpContext, episode, includeDataFrom, includeFiles, includeMediaInfo, includeAbsolutePaths, includeXRefs), page, pageSize);
    }

    /// <summary>
    /// Get series with missing episodes, collecting or otherwise.
    /// </summary>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="collecting">Only show series with missing episodes from release groups we're collecting.</param>
    /// <param name="onlyFinishedSeries">Only show finished series.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [HttpGet("Series")]
    public ActionResult<ListResult<Series.WithEpisodeCount>> GetSeriesWithMultipleReleases(
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] bool collecting = false,
        [FromQuery] bool onlyFinishedSeries = false,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        var enumerable = RepoFactory.AnimeSeries.GetWithMissingEpisodes(collecting);
        if (onlyFinishedSeries)
            enumerable = enumerable.Where(a => a.AniDB_Anime.GetFinishedAiring());

        return enumerable
            .OrderBy(series => series.PreferredTitle)
            .ThenBy(series => series.AniDB_ID)
            .ToListResult(series => new Series.WithEpisodeCount(collecting ? series.MissingEpisodeCountGroups : series.MissingEpisodeCount, series, User.JMMUserID, includeDataFrom), page, pageSize);
    }

    /// <summary>
    /// Get missing episodes, be it collecting or otherwise, for a specific series.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="includeFiles">Include files with the episodes.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeAbsolutePaths">Include absolute paths for the file locations.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
    /// <param name="collecting">Only show missing episodes from release groups we're collecting.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [HttpGet("Series/{seriesID}/Episodes")]
    public ActionResult<ListResult<Episode>> GetEpisodesForSeries(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] bool includeFiles = true,
        [FromQuery] bool includeMediaInfo = true,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] bool collecting = false,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return new ListResult<Episode>();

        if (!User.AllowedSeries(series))
            return new ListResult<Episode>();

        var enumerable = RepoFactory.AnimeEpisode.GetMissing(collecting, series.AniDB_ID);

        return enumerable
            .ToListResult(episode => new Episode(HttpContext, episode, includeDataFrom, includeFiles, includeMediaInfo, includeAbsolutePaths, includeXRefs), page, pageSize);
    }
}
