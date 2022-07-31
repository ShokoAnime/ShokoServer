using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Models.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Controllers
{
    /// <summary>
    /// This Controller is intended to provide the tree. An example would be "api/v3/filter/4/group/12/series".
    /// This is to support filtering with Apply At Series Level and any other situations that might involve the need for it.
    /// </summary>
    [ApiController, Route("/api/v{version:apiVersion}"), ApiV3]
    [Authorize]
    public class TreeController : BaseController
    {
        #region Filter

        /// <summary>
        /// Get a list of all the sub-<see cref="Filter"/> for the <see cref="Filter"/> with the given <paramref name="filterID"/>.
        /// </summary>
        /// <remarks>
        /// The <see cref="Filter"/> must have <see cref="Filter.Directory"/> set to true to use
        /// this endpoint.
        /// </remarks>
        /// <param name="filterID"><see cref="Filter"/> ID</param>
        /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
        /// <param name="page">The page index.</param>
        /// <param name="showHidden">Show hidden filters</param>
        /// <returns></returns>
        [HttpGet("Filter/{filterID}/Filter")]
        public ActionResult<ListResult<Filter>> GetSubFilters([FromRoute] int filterID, [FromQuery] [Range(0, 100)] int pageSize = 50, [FromQuery] [Range(1, int.MaxValue)] int page = 1, [FromQuery] bool showHidden = false)
        {
            var groupFilter = RepoFactory.GroupFilter.GetByID(filterID);
            if (groupFilter == null)
                return NotFound(FilterController.FilterNotFound);

            if (!((GroupFilterType)groupFilter.FilterType).HasFlag(GroupFilterType.Directory))
                return BadRequest("Filter contains no sub-filters.");

            return RepoFactory.GroupFilter.GetByParentID(filterID)
                .Where(filter => showHidden || filter.InvisibleInClients != 1)
                .OrderByName()
                .ToListResult(filter => new Filter(HttpContext, filter), page, pageSize);
        }

        /// <summary>
        /// Get a paginated list of all the top-level <see cref="Group"/>s for the <see cref="Filter"/> with the given <paramref name="filterID"/>.
        /// </summary>
        /// <param name="filterID"><see cref="Filter"/> ID</param>
        /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
        /// <param name="page">The page index.</param>
        /// <returns></returns>
        [HttpGet("Filter/{filterID}/Group")]
        public ActionResult<ListResult<Group>> GetFilteredGroups([FromRoute] int filterID, [FromQuery] [Range(0, 100)] int pageSize = 50, [FromQuery] [Range(1, int.MaxValue)] int page = 1)
        {
            // Return the top level groups with no filter.
            IEnumerable<SVR_AnimeGroup> groups;
            if (filterID == 0)
            {
                groups = RepoFactory.AnimeGroup.GetAll()
                    .Where(group => group.AnimeGroupParentID.HasValue && User.AllowedGroup(group))
                    .OrderByName();
            }
            else
            {
                var groupFilter = RepoFactory.GroupFilter.GetByID(filterID);
                if (groupFilter == null)
                    return NotFound(FilterController.FilterNotFound);

                // Fast path when user is not in the filter
                if (!groupFilter.GroupsIds.TryGetValue(User.JMMUserID, out var groupIds))
                    return new ListResult<Group>();

                groups = groupIds
                    .Select(group => RepoFactory.AnimeGroup.GetByID(group))
                    .Where(group => group != null)
                    .OrderByGroupFilter(groupFilter);
            }

            return groups
                .ToListResult(group => new Group(HttpContext, group), page, pageSize);
        }

        /// <summary>
        /// Get a list of all the sub-<see cref="Group"/>s belonging to the <see cref="Group"/> with the given <paramref name="groupID"/> and which are present within the <see cref="Filter"/> with the given <paramref name="filterID"/>.
        /// </summary>
        /// <param name="filterID"><see cref="Filter"/> ID</param>
        /// <param name="groupID"><see cref="Group"/> ID</param>
        /// <param name="randomImages">Randomise images shown for the <see cref="Group"/>.</param>
        /// <returns></returns>
        [HttpGet("Filter/{filterID}/Group/{groupID}/Group")]
        public ActionResult<List<Group>> GetFilteredSubGroups([FromRoute] int filterID, [FromRoute] int groupID, [FromQuery] bool randomImages = false)
        {
            // Return sub-groups with no group filter applied.
            if (filterID == 0)
                return GetSubGroups(groupID);

            var groupFilter = RepoFactory.GroupFilter.GetByID(filterID);
            if (groupFilter == null)
                return NotFound(FilterController.FilterNotFound);

            // Check if the group exists.
            var group = RepoFactory.AnimeGroup.GetByID(groupID);
            if (group == null)
                return NotFound(GroupController.GroupNotFound);

            // Just return early because the every gropup will be filtered out.
            if (!groupFilter.SeriesIds.TryGetValue(User.JMMUserID, out var seriesIDs))
                return new List<Group>();

            return group.GetChildGroups()
                .Where(subGroup =>
                {
                    if (subGroup == null)
                        return false;

                    if (User.AllowedGroup(subGroup))
                        return false;

                    if (groupFilter.ApplyToSeries != 1)
                        return true;

                    return subGroup.GetAllSeries().Any(series => seriesIDs.Contains(series.AnimeSeriesID));
                })
                .OrderByGroupFilter(groupFilter)
                .Select(group => new Group(HttpContext, group, randomImages))
                .ToList();
        }

        /// <summary>
        /// Get a list of all the <see cref="Series"/> for the <see cref="Group"/> within the <see cref="Filter"/>.
        /// </summary>
        /// <remarks>
        ///  Pass a <paramref name="filterID"/> of <code>0</code> to disable filter or .
        /// </remarks>
        /// <param name="filterID"><see cref="Filter"/> ID</param>
        /// <param name="groupID"><see cref="Group"/> ID</param>
        /// <param name="recursive">Show all the <see cref="Series"/> within the <see cref="Group"/>. Even the <see cref="Series"/> within the sub-<see cref="Group"/>s.</param>
        /// <param name="includeMissing">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the list.</param>
        /// <param name="randomImages">Randomise images shown for each <see cref="Series"/> within the <see cref="Group"/>.</param>
        /// /// <returns></returns>
        [HttpGet("Filter/{filterID}/Group/{groupID}/Series")]
        public ActionResult<List<Series>> GetSeriesInFilteredGroup([FromRoute] int filterID, [FromRoute] int groupID, [FromQuery] bool recursive = false, [FromQuery] bool includeMissing = false, [FromQuery] bool randomImages = false)
        {
            // Return the groups with no group filter applied.
            if (filterID == 0)
                return GetSeriesInGroup(groupID, recursive, includeMissing, randomImages);

            // Check if the group filter exists.
            var groupFilter = RepoFactory.GroupFilter.GetByID(filterID);
            if (groupFilter == null)
                return NotFound(FilterController.FilterNotFound);

            if (groupFilter.ApplyToSeries != 1)
                return GetSeriesInGroup(groupID, recursive, includeMissing, randomImages);

            // Check if the group exists.
            var group = RepoFactory.AnimeGroup.GetByID(groupID);
            if (group == null)
                return NotFound(GroupController.GroupNotFound);

            // Just return early because the every series will be filtered out.
            if (!groupFilter.SeriesIds.TryGetValue(User.JMMUserID, out var seriesIDs))
                return new List<Series>();

            return (recursive ? group.GetAllSeries() : group.GetSeries())
                .Where(series => seriesIDs.Contains(series.AnimeSeriesID))
                .OrderByAirDate()
                .Select(series => new Series(HttpContext, series))
                .Where(series => series.Size > 0 || includeMissing)
                .ToList();
        }

        #endregion
        #region Group

        /// <summary>
        /// Get a list of sub-<see cref="Group"/>s a the <see cref="Group"/>.
        /// </summary>
        /// <param name="groupID"></param>
        /// <param name="randomImages">Randomise images shown for the <see cref="Group"/>.</param>
        /// <returns></returns>
        [HttpGet("Group/{groupID}/Group")]
        public ActionResult<List<Group>> GetSubGroups([FromRoute] int groupID, [FromQuery] bool randomImages = false)
        {
            // Check if the group exists.
            var group = RepoFactory.AnimeGroup.GetByID(groupID);
            if (group == null)
                return NotFound(GroupController.GroupNotFound);

            return group.GetChildGroups()
                .Where(group => User.AllowedGroup(group))
                .OrderByName()
                .Select(group => new Group(HttpContext, group, randomImages))
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
        /// <param name="randomImages">Randomise images shown for each <see cref="Series"/> within the <see cref="Group"/>.</param>
        /// <returns></returns>
        [HttpGet("Group/{groupID}/Series")]
        public ActionResult<List<Series>> GetSeriesInGroup([FromRoute] int groupID, [FromQuery] bool recursive = false, [FromQuery] bool includeMissing = false, [FromQuery] bool randomImages = false)
        {
            // Check if the group exists.
            var group = RepoFactory.AnimeGroup.GetByID(groupID);
            if (group == null)
                return NotFound(GroupController.GroupNotFound);

            return (recursive ? group.GetAllSeries() : group.GetSeries())
                .Where(a => User.AllowedSeries(a))
                .OrderByAirDate()
                .Select(series => new Series(HttpContext, series, randomImages))
                .Where(series => series.Size > 0 || includeMissing)
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
        /// <param name="randomImages">Randomise images shown for the <see cref="Series"/>.</param>
        /// <returns></returns>
        [HttpGet("Group/{groupID}/MainSeries")]
        public ActionResult<Series> GetMainSeriesInGroup([FromRoute] int groupID,[FromQuery] bool randomImages = false)
        {
            // Check if the group exists.
            var group = RepoFactory.AnimeGroup.GetByID(groupID);
            if (group == null)
                return NotFound(GroupController.GroupNotFound);

            var user = User;
            if (user.AllowedGroup(group))
                return Forbid(GroupController.GroupForbiddenForUser);

            var mainSeries = group.GetMainSeries();
            if (mainSeries == null)
                return NotFound("Unable to find main series for group.");

            return new Series(HttpContext, mainSeries, randomImages);
        }

        #endregion
        #region Series

        /// <summary>
        /// Get the <see cref="Episode"/>s for the <see cref="Series"/> with <paramref name="seriesID"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="Filter"/> or <see cref="Group"/> is irrelevant at this level.
        /// </remarks>
        /// <param name="seriesID">Series ID</param>
        /// <param name="includeMissing">Include missing episodes in the list.</param>
        /// <returns></returns>
        [HttpGet("Series/{seriesID}/Episode")]
        public ActionResult<List<Episode>> GetEpisodes([FromRoute] int seriesID, [FromQuery] bool includeMissing = false)
        {
            var series = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (series == null)
                return NotFound(SeriesController.SeriesNotFoundWithSeriesID);
            if (!User.AllowedSeries(series))
                return Forbid(SeriesController.SeriesForbiddenForUser);

            return series.GetAnimeEpisodes()
                .Select(a => new Episode(HttpContext, a))
                .Where(a => a.Size > 0 || includeMissing)
                .ToList();
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
        /// <returns></returns>
        [HttpGet("Series/{seriesID}/NextUpEpisode")]
        public ActionResult<Episode> GetNextUnwatchedEpisode([FromRoute] int seriesID, [FromQuery] bool onlyUnwatched = true, [FromQuery] bool includeSpecials = true)
        {
            var user = User;
            var series = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (series == null)
                return NotFound(SeriesController.SeriesNotFoundWithSeriesID);
            if (!user.AllowedSeries(series))
                return Forbid(SeriesController.SeriesForbiddenForUser);

            var episode = series.GetNextEpisode(user.JMMUserID, onlyUnwatched, includeSpecials);
            if (episode == null)
                return null;

            return new Episode(HttpContext, episode);
        }

        #endregion
        #region Episode

        /// <summary>
        /// Get the <see cref="File.FileDetailed"/>s for the <see cref="Episode"/> with the given <paramref name="episodeID"/>.
        /// </summary>
        /// <param name="episodeID">Episode ID</param>
        /// <returns></returns>
        [HttpGet("Episode/{episodeID}/File")]
        public ActionResult<List<File.FileDetailed>> GetFiles([FromRoute] int episodeID)
        {
            var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
            if (episode == null)
                return NotFound(EpisodeController.EpisodeNotFoundWithEpisodeID);

            var series = episode.GetAnimeSeries();
            if (series == null)
                return InternalError("No Series entry for given Episode");
            if (!User.AllowedSeries(series))
                return Forbid(EpisodeController.EpisodeForbiddenForUser);

            return episode.GetVideoLocals()
                .Select(file => new File.FileDetailed(file))
                .ToList();
        }

        #endregion
    }
}