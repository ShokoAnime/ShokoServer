using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// This Controller is intended to provide the reverse tree. It is used to get the series from episodes, etc.
/// This is to support filtering with Apply At Series Level and any other situations that might involve the need for it.
/// </summary>
[ApiController]
[Route("/api/v{version:apiVersion}")]
[ApiV3]
[Authorize]
public class ReverseTreeController : BaseController
{
    private readonly FilterFactory _filterFactory;

    private readonly SeriesFactory _seriesFactory;

    /// <summary>
    /// Get the parent <see cref="Filter"/> for the <see cref="Filter"/> with the given <paramref name="filterID"/>.
    /// </summary>
    /// <remarks>
    /// This endpoint can be used to get the direct <see cref="Filter"/> parent to a <see cref="Filter"/> (in case
    /// it's within a sub-Filter) or to always get the top-level  <see cref="Filter"/> regardless if
    /// <paramref name="topLevel"/> is set to <code>true</code>.
    /// 
    /// Trying to get the parent of a top-level <see cref="Filter"/> is an user error and will throw a complaint.
    /// </remarks>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="topLevel">Always get the top-level <see cref="Filter"/></param>
    /// <returns></returns>
    [HttpGet("Filter/{filterID}/Parent")]
    public ActionResult<Filter> GetParentFromFilter([FromRoute] int filterID, [FromQuery] bool topLevel = false)
    {
        var filter = RepoFactory.FilterPreset.GetByID(filterID);
        if (filter == null)
        {
            return NotFound(FilterController.FilterNotFound);
        }

        if (filter.ParentFilterPresetID is null or 0)
        {
            return ValidationProblem("Unable to get parent Filter for a top-level Filter", "filterID");
        }

        var parentGroup = topLevel ? RepoFactory.FilterPreset.GetTopLevelFilter(filter.ParentFilterPresetID.Value) : RepoFactory.FilterPreset.GetByID(filter.ParentFilterPresetID.Value);
        if (parentGroup == null)
        {
            return InternalError("No parent Filter entry for the given filterID");
        }

        return _filterFactory.GetFilter(parentGroup);
    }

    /// <summary>
    /// Get the parent <see cref="Group"/> for the <see cref="Group"/> with the given <paramref name="groupID"/>.
    /// </summary>
    /// <remarks>
    /// This endpoint can be used to get the direct <see cref="Group"/> parent to a <see cref="Group"/> (in case
    /// it's within a sub-group) or to always get the top-level  <see cref="Group"/> regardless if
    /// <paramref name="topLevel"/> is set to <code>true</code>.
    /// 
    /// Trying to get the parent of a top-level <see cref="Group"/> is an user error and will throw a complaint.
    /// </remarks>
    /// <param name="groupID"><see cref="Group"/> ID</param>
    /// <param name="topLevel">Always get the top-level <see cref="Group"/></param>
    /// <returns></returns>
    [HttpGet("Group/{groupID}/Parent")]
    public ActionResult<Group> GetParentFromGroup([FromRoute] int groupID, [FromQuery] bool topLevel = false)
    {
        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
        {
            return NotFound(GroupController.GroupNotFound);
        }

        if (!User.AllowedGroup(group))
        {
            return Forbid(GroupController.GroupForbiddenForUser);
        }

        if (!group.AnimeGroupParentID.HasValue || group.AnimeGroupParentID.Value == 0)
        {
            return ValidationProblem("Unable to get parent Group for a top-level Group", "groupID");
        }

        var parentGroup = topLevel ? group.TopLevelAnimeGroup : group.Parent;
        if (parentGroup == null)
        {
            return InternalError("No parent Group entry for the given groupID");
        }

        return new Group(HttpContext, parentGroup);
    }

    /// <summary>
    /// Get the <see cref="Group"/> for the <see cref="Series"/> with the given <paramref name="seriesID"/>.
    /// </summary>
    /// <remarks>
    /// This endpoint can be used to get the direct <see cref="Group"/> parent to a <see cref="Series"/> (in case
    /// it's within a sub-group) or to always get the top-level  <see cref="Group"/> regardless if
    /// <paramref name="topLevel"/> is set to <code>true</code>.
    /// </remarks>
    /// <param name="seriesID"><see cref="Series"/> ID</param>
    /// <param name="topLevel">Always get the top-level <see cref="Group"/></param>
    /// <returns></returns>
    [HttpGet("Series/{seriesID}/Group")]
    public ActionResult<Group> GetGroupFromSeries([FromRoute] int seriesID, [FromQuery] bool topLevel = false)
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesController.SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesController.SeriesForbiddenForUser);
        }

        var group = topLevel ? series.TopLevelAnimeGroup : series.AnimeGroup;
        if (group == null)
        {
            return InternalError("No Group entry for the Series");
        }

        return new Group(HttpContext, group);
    }

    /// <summary>
    /// Get the <see cref="Series"/> for the <see cref="Episode"/> with the given <paramref name="episodeID"/>.
    /// </summary>
    /// <param name="episodeID"><see cref="Episode"/> ID</param>
    /// <param name="randomImages">Randomise images shown for the <see cref="Series"/>.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("Episode/{episodeID}/Series")]
    public ActionResult<Series> GetSeriesFromEpisode([FromRoute] int episodeID, [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
        {
            return NotFound(EpisodeController.EpisodeNotFoundWithEpisodeID);
        }

        var series = episode.GetAnimeSeries();
        if (series == null)
        {
            return InternalError("No Series entry for the Episode");
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(EpisodeController.EpisodeForbiddenForUser);
        }

        return _seriesFactory.GetSeries(series, randomImages, includeDataFrom);
    }

    /// <summary>
    /// Get the <see cref="Episode"/>s for the <see cref="File"/> with the given <paramref name="fileID"/>.
    /// </summary>
    /// <param name="fileID"><see cref="File"/> ID</param>
    /// <param name="includeFiles">Include files with the episodes.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeAbsolutePaths">Include absolute paths for the file locations.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("File/{fileID}/Episode")]
    public ActionResult<List<Episode>> GetEpisodeFromFile(
        [FromRoute] int fileID,
        [FromQuery] bool includeFiles = false,
        [FromQuery] bool includeMediaInfo = false,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
        {
            return NotFound(FileController.FileNotFoundWithFileID);
        }

        var episodes = file.GetAnimeEpisodes();
        if (!episodes.All(episode => User.AllowedSeries(episode.GetAnimeSeries())))
        {
            return Forbid(FileController.FileForbiddenForUser);
        }

        return episodes
            .Select(a => new Episode(HttpContext, a, includeDataFrom, includeFiles, includeMediaInfo, includeAbsolutePaths, includeXRefs))
            .ToList();
    }

    public ReverseTreeController(ISettingsProvider settingsProvider, FilterFactory filterFactory, SeriesFactory seriesFactory) : base(settingsProvider)
    {
        _filterFactory = filterFactory;
        _seriesFactory = seriesFactory;
    }
}
