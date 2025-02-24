using System;
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
using Shoko.Server.Utilities;

#pragma warning disable CA1822
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Obsolete("Use the ReleaseManagementMultipleReleasesController instead")]
public class ReleaseManagementController(ISettingsProvider settingsProvider) : BaseController(settingsProvider)
{
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
    public ActionResult<ListResult<Series.WithEpisodeCount>> GetSeriesWithMultipleReleases(
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] bool ignoreVariations = true,
        [FromQuery] bool onlyFinishedSeries = false,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        var enumerable = RepoFactory.AnimeSeries.GetWithMultipleReleases(ignoreVariations);
        if (onlyFinishedSeries) enumerable = enumerable.Where(a => a.AniDB_Anime.GetFinishedAiring());

        return enumerable
            .OrderBy(series => series.PreferredTitle)
            .ThenBy(series => series.AniDB_ID)
            .ToListResult(series => new Series.WithEpisodeCount(RepoFactory.AnimeEpisode.GetWithMultipleReleases(ignoreVariations, series.AniDB_ID).Count(), series, User.JMMUserID, includeDataFrom), page, pageSize);
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
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] bool includeFiles = true,
        [FromQuery] bool includeMediaInfo = true,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] bool ignoreVariations = true,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return new ListResult<Episode>();

        if (!User.AllowedSeries(series))
            return new ListResult<Episode>();

        var enumerable = RepoFactory.AnimeEpisode.GetWithMultipleReleases(ignoreVariations, series.AniDB_ID);

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
        var enumerable = RepoFactory.AnimeEpisode.GetWithMultipleReleases(ignoreVariations);

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
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery] bool ignoreVariations = true
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return new List<int>();

        if (!User.AllowedSeries(series))
            return new List<int>();

        var enumerable = RepoFactory.AnimeEpisode.GetWithMultipleReleases(ignoreVariations, series.AniDB_ID);

        return enumerable
            .SelectMany(episode =>
            {
                var files = episode.VideoLocals.ToList();
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
        var enumerable = RepoFactory.AnimeEpisode.GetWithMultipleReleases(ignoreVariations);

        return enumerable
            .SelectMany(episode =>
            {
                var files = episode.VideoLocals.ToList();
                files.Sort(FileQualityFilter.CompareTo);
                return files
                    .Skip(FileQualityFilter.Settings.MaxNumberOfFilesToKeep)
                    .Where(file => !FileQualityFilter.CheckFileKeep(file))
                    .Select(file => file.VideoLocalID);
            })
            .Distinct()
            .ToList();
    }
}
