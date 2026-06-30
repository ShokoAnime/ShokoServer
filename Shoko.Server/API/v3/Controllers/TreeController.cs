using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Metadata;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// This Controller is intended to provide the tree. An example would be "api/v3/filter/4/group/12/series".
/// This is to support filtering with Apply At Series Level and any other situations that might involve the need for it.
/// </summary>
[ApiController]
[Route("/api/v{version:apiVersion}")]
[ApiV3]
[Authorize]
public class TreeController(ISettingsProvider settingsProvider,
    AnimeGroupRepository _animeGroups,
    AnimeSeriesRepository _animeSeries,
    AnimeEpisodeRepository _animeEpisodes
) : BaseController(settingsProvider)
{
    #region Group

    /// <summary>
    /// Get a list of sub-<see cref="Group"/>s a the <see cref="Group"/>.
    /// </summary>
    /// <param name="groupID"></param>
    /// <param name="randomImages">Randomize images shown for the <see cref="Group"/>.</param>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the search.</param>
    /// <returns></returns>
    [HttpGet("Group/{groupID}/Group")]
    public ActionResult<List<Group>> GetSubGroups([FromRoute, Range(1, int.MaxValue)] int groupID, [FromQuery] bool randomImages = false,
        [FromQuery] bool includeEmpty = true)
    {
        if (_animeGroups.GetByID(groupID) is not { } group)
            return NotFound(GroupController.GroupNotFound);

        var user = User;
        if (!user.AllowedGroup(group))
            return Forbid(GroupController.GroupForbiddenForUser);

        return group.Children
            .Where(a => user.AllowedGroup(a) && (includeEmpty || !a.AllSeries.Any(s => s.VideoLocals.Count > 0)))
            .OrderBy(g => g.SortName)
            .Select(g => new Group(g, user.JMMUserID, randomImages))
            .ToList();
    }

    /// <summary>
    /// Get a list of <see cref="Series"/> within a <see cref="Group"/>.
    /// </summary>
    /// <remarks>
    /// It will return all the <see cref="Series"/> within the group and all sub-groups if
    /// <paramref name="recursive"/> is set to <code>true</code>.
    /// </remarks>
    /// <param name="groupID"><see cref="Group"/> ID</param>
    /// <param name="recursive">Show all the <see cref="Series"/> within the <see cref="Group"/></param>
    /// <param name="includeMissing">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the list.</param>
    /// <param name="randomImages">Randomize images shown for each <see cref="Series"/> within the <see cref="Group"/>.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSourceType"/>s.</param>
    /// <returns></returns>
    [HttpGet("Group/{groupID}/Series")]
    public ActionResult<List<Series>> GetSeriesInGroup([FromRoute, Range(1, int.MaxValue)] int groupID, [FromQuery] bool recursive = false,
        [FromQuery] bool includeMissing = true, [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSourceType>? includeDataFrom = null)
    {
        if (_animeGroups.GetByID(groupID) is not { } group)
            return NotFound(GroupController.GroupNotFound);

        var user = User;
        if (!user.AllowedGroup(group))
            return Forbid(GroupController.GroupForbiddenForUser);

        return (recursive ? group.AllSeries : group.Series)
            .Where(a => user.AllowedSeries(a) && (includeMissing || a.VideoLocals.Count > 0))
            .OrderBy(a => a?.AirDate ?? PartialDateOnly.MaxValue)
            .Select(series => new Series(series, user.JMMUserID, randomImages, includeDataFrom))
            .ToList();
    }

    /// <summary>
    /// Get the main <see cref="Series"/> in a <see cref="Group"/>.
    /// </summary>
    /// <remarks>
    /// It will return 1) the default series or 2) the earliest running
    /// series if the group contains a series, or nothing if the group is
    /// empty.
    /// </remarks>
    /// <param name="groupID"><see cref="Group"/> ID</param>
    /// <param name="randomImages">Randomize images shown for the <see cref="Series"/>.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSourceType"/>s.</param>
    /// <returns></returns>
    [HttpGet("Group/{groupID}/MainSeries")]
    public ActionResult<Series> GetMainSeriesInGroup([FromRoute, Range(1, int.MaxValue)] int groupID, [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSourceType>? includeDataFrom = null)
    {
        if (_animeGroups.GetByID(groupID) is not { } group)
            return NotFound(GroupController.GroupNotFound);

        var user = User;
        if (!user.AllowedGroup(group))
            return Forbid(GroupController.GroupForbiddenForUser);

        if (group.MainSeries is not { } mainSeries)
            return InternalError("Unable to find main series for group.");

        return new Series(mainSeries, user.JMMUserID, randomImages, includeDataFrom);
    }

    #endregion

    /// <summary>
    /// Get the <see cref="File"/>s for the <see cref="Series"/> with the given <paramref name="seriesID"/>.
    /// </summary>
    /// <param name="seriesID">Series ID</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <param name="exclude">Exclude items of certain types</param>
    /// <param name="include_only">Filter to only include items of certain types</param>
    /// <param name="releaseProviders">Filter to only include files from certain release providers. Append <c>!</c> to the provider name to exclude the files</param>
    /// <param name="sortOrder">Sort ordering. Attach '-' at the start to reverse the order of the criteria.</param>
    /// <returns></returns>
    [HttpGet("Series/{seriesID}/File")]
    public ActionResult<ListResult<File>> GetFilesForSeries(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[]? include = default,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileExcludeTypes[]? exclude = default,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileIncludeOnlyType[]? include_only = default,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? releaseProviders = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? sortOrder = null
    )
    {
        if (_animeSeries.GetByID(seriesID) is not { } series)
            return NotFound(SeriesController.SeriesNotFoundWithSeriesID);

        var user = User;
        if (!user.AllowedSeries(series))
            return Forbid(SeriesController.SeriesForbiddenForUser);

        return ModelHelper.FilterFiles(series.VideoLocals, user, pageSize, page, include, exclude, include_only, releaseProviders, sortOrder);
    }

    #region Episode

    /// <summary>
    /// Get the <see cref="File"/>s for the <see cref="Episode"/> with the given <paramref name="episodeID"/>.
    /// </summary>
    /// <param name="episodeID">Episode ID</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <param name="exclude">Exclude items of certain types</param>
    /// <param name="include_only">Filter to only include items of certain types</param>
    /// <param name="releaseProviders">Filter to only include files from certain release providers. Append <c>!</c> to the provider name to exclude the files</param>
    /// <param name="sortOrder">Sort ordering. Attach '-' at the start to reverse the order of the criteria.</param>
    /// <returns></returns>
    [HttpGet("Episode/{episodeID}/File")]
    public ActionResult<ListResult<File>> GetFilesForEpisode([FromRoute, Range(1, int.MaxValue)] int episodeID,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[]? include = default,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileExcludeTypes[]? exclude = default,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileIncludeOnlyType[]? include_only = default,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? releaseProviders = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? sortOrder = null
    )
    {
        if (_animeEpisodes.GetByID(episodeID) is not { } episode)
            return NotFound(EpisodeController.EpisodeNotFoundWithEpisodeID);

        if (episode.AnimeSeries is not { } series)
            return InternalError("No Series entry for given Episode");

        var user = User;
        if (!user.AllowedSeries(series))
            return Forbid(EpisodeController.EpisodeForbiddenForUser);

        return ModelHelper.FilterFiles(episode.VideoLocals, user, pageSize, page, include, exclude, include_only, releaseProviders, sortOrder);
    }

    #endregion
}
