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
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Controllers
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class FilterController : BaseController
    {
        internal static string FilterNotFound = "No Filter entry for the given filterID";

        /// <summary>
        /// Get All <see cref="Filter"/>s
        /// </summary>
        /// <param name="includeEmpty">Include empty filters.</param>
        /// <param name="showHidden">Show hidden filters.</param>
        /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
        /// <param name="page">The page index.</param>
        /// <returns></returns>
        [HttpGet]
        public ActionResult<ListResult<Filter>> GetAllFilters([FromQuery] bool includeEmpty = false, [FromQuery] bool showHidden = false, [FromQuery] [Range(0, 100)] int pageSize = 10, [FromQuery] [Range(1, int.MaxValue)] int page = 1)
        {
            return RepoFactory.GroupFilter.GetTopLevel()
                .Where(filter =>
                {
                    if (!showHidden && filter.InvisibleInClients == 1)
                        return false;
                    if (!includeEmpty && filter.GroupsIds.ContainsKey(User.JMMUserID) && filter.GroupsIds[User.JMMUserID].Count == 0)
                        return false;
                    return ((GroupFilterType)filter.FilterType).HasFlag(GroupFilterType.Directory);
                })
                .OrderBy(filter => filter.GroupFilterName)
                .ToListResult(filter => new Filter(HttpContext, filter), page, pageSize);
        }

        /// <summary>
        /// Create or update a filter
        /// </summary>
        /// <param name="body"></param>
        /// <returns>The resulting Filter, with ID</returns>
        [HttpPost]
        public ActionResult<Filter> SaveFilter(Filter.FullFilter body)
        {
            SVR_GroupFilter groupFilter = null;
            if (body.IDs.ID != 0)
            {
                groupFilter = RepoFactory.GroupFilter.GetByID(body.IDs.ID);
                if (groupFilter == null)
                    return NotFound(FilterNotFound);
                if (groupFilter.Locked == 1)
                    return Forbid("Filter is Locked");
            }
            groupFilter = body.ToServerModel(groupFilter);
            groupFilter.CalculateGroupsAndSeries();
            RepoFactory.GroupFilter.Save(groupFilter);

            return new Filter(HttpContext, groupFilter);
        }

        /// <summary>
        /// Preview the Groups that will be in the filter if the changes are applied
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        [HttpPost("Preview")]
        public ActionResult<List<Group>> PreviewFilterChanges(Filter.FullFilter body)
        {
            var groupFilter = body.ToServerModel();
            groupFilter.CalculateGroupsAndSeries();

            if (!groupFilter.GroupsIds.TryGetValue(User.JMMUserID, out var groupIDs))
                return new List<Group>();

            return groupIDs
                .Select(a => RepoFactory.AnimeGroup.GetByID(a))
                .Where(a => a != null)
                .OrderByGroupFilter(groupFilter)
                .Select(a => new Group(HttpContext, a))
                .ToList();
        }

        /// <summary>
        /// Get the <see cref="Filter"/> for the given <paramref name="filterID"/>.
        /// </summary>
        /// <param name="filterID">Filter ID</param>
        /// <returns></returns>
        [HttpGet("{filterID}")]
        public ActionResult<Filter> GetFilter(int filterID)
        {
            var groupFilter = RepoFactory.GroupFilter.GetByID(filterID);
            if (groupFilter == null)
                return NotFound(FilterNotFound);

            return new Filter(HttpContext, groupFilter);
        }

        /// <summary>
        /// Delete a filter
        /// </summary>
        /// <param name="filterID"></param>
        /// <returns></returns>
        [Authorize("admin")]
        [HttpDelete("{filterID}")]
        public ActionResult DeleteFilter(int filterID)
        {
            var groupFilter = RepoFactory.GroupFilter.GetByID(filterID);
            if (groupFilter == null)
                return NotFound(FilterNotFound);

            RepoFactory.GroupFilter.Delete(groupFilter);
            return NoContent();
        }

        /// <summary>
        /// Get Conditions for Filter with id
        /// </summary>
        /// <param name="filterID"></param>
        /// <returns></returns>
        [HttpGet("{filterID}/Conditions")]
        public ActionResult<Filter.FilterConditions> GetFilterConditions(int filterID)
        {
            var groupFilter = RepoFactory.GroupFilter.GetByID(filterID);
            if (groupFilter == null)
                return NotFound(FilterNotFound);

            return Filter.GetConditions(groupFilter);
        }

        /// <summary>
        /// Get Sorting Criteria for Filter with id
        /// </summary>
        /// <param name="filterID"></param>
        /// <returns></returns>
        [HttpGet("{filterID}/Sorting")]
        public ActionResult<List<Filter.SortingCriteria>> GetFilterSortingCriteria(int filterID)
        {
            var groupFilter = RepoFactory.GroupFilter.GetByID(filterID);
            if (groupFilter == null)
                return NotFound(FilterNotFound);

            return Filter.GetSortingCriteria(groupFilter);
        }
    }
}
