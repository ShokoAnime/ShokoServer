using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Settings;

#pragma warning disable CA1822
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/ReleaseManagement/MissingEpisodes")]
[ApiV3]
[Authorize]
public class ReleaseManagementMissingEpisodesController(ISettingsProvider settingsProvider,
    AnimeEpisodeRepository _animeEpisodes,
    AnimeSeriesRepository _animeSeries,
    AniDB_Anime_TitleRepository _anidbTitles
) : BaseController(settingsProvider)
{
    /// <summary>
    /// Get missing episodes, be it collecting or otherwise.
    /// </summary>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSourceType"/>s.</param>
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
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSourceType> includeDataFrom = null,
        [FromQuery] bool includeFiles = true,
        [FromQuery] bool includeMediaInfo = true,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] bool collecting = false,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        var enumerable = _animeEpisodes.GetMissing(collecting).Where(a => a.AnimeSeries is { } series && User.AllowedSeries(series));

        return enumerable
            .ToListResult(episode => new Episode(HttpContext, episode, includeDataFrom, includeFiles, includeMediaInfo, includeAbsolutePaths, includeXRefs), page, pageSize);
    }

    /// <summary>
    /// Get series with missing episodes, collecting or otherwise.
    /// </summary>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSourceType"/>s.</param>
    /// <param name="collecting">Only show series with missing episodes from release groups we're collecting.</param>
    /// <param name="onlyFinishedSeries">Only show finished series.</param>
    /// <param name="search">Filter by series title. Matched case-insensitively against all main and official titles across all languages.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [HttpGet("Series")]
    public ActionResult<ListResult<Series.WithEpisodeCount>> GetSeriesWithMultipleReleases(
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSourceType> includeDataFrom = null,
        [FromQuery] bool collecting = false,
        [FromQuery] bool onlyFinishedSeries = false,
        [FromQuery] string search = null,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        var missingBySeries = _animeEpisodes.GetMissing(collecting)
            .Where(e => e.AnimeSeries is { } series && User.AllowedSeries(series))
            .GroupBy(e => e.AnimeSeriesID)
            .Select(g => (Series: g.First().AnimeSeries!, Count: g.Count()))
            .AsEnumerable();

        if (onlyFinishedSeries)
            missingBySeries = missingBySeries.Where(t => t.Series.AniDB_Anime is { } anime && anime.GetFinishedAiring());

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = AniDB_Anime_TitleRepository.NormalizeForSearch(search);
            missingBySeries = missingBySeries.Where(t => _anidbTitles.AnimeMatchesSearch(t.Series.AniDB_ID, normalizedSearch));
        }

        return missingBySeries
            .OrderBy(t => t.Series.Title)
            .ThenBy(t => t.Series.AniDB_ID)
            .ToListResult(t => new Series.WithEpisodeCount(t.Count, t.Series, User.JMMUserID, includeDataFrom), page, pageSize);
    }

    /// <summary>
    /// Get missing episodes, be it collecting or otherwise, for a specific series.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSourceType"/>s.</param>
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
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSourceType> includeDataFrom = null,
        [FromQuery] bool includeFiles = true,
        [FromQuery] bool includeMediaInfo = true,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] bool collecting = false,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        var series = _animeSeries.GetByID(seriesID);
        if (series == null)
            return new ListResult<Episode>();

        if (!User.AllowedSeries(series))
            return new ListResult<Episode>();

        var enumerable = _animeEpisodes.GetMissing(collecting, series.AniDB_ID);

        return enumerable
            .ToListResult(episode => new Episode(HttpContext, episode, includeDataFrom, includeFiles, includeMediaInfo, includeAbsolutePaths, includeXRefs), page, pageSize);
    }
}
