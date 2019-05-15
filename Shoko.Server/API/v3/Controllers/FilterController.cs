using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Models.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class FilterController : BaseController
    {
        /// <summary>
        /// Get Filter with id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}")]
        public ActionResult<Filter> GetFilter(int id)
        {
            var gf = RepoFactory.GroupFilter.GetByID(id);
            if (gf == null) return BadRequest("No filter with id");
            return new Filter(HttpContext, gf);
        }

        /// <summary>
        /// Get Filter with id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}/Filter")]
        public ActionResult<List<Filter>> GetSubFilters(int id)
        {
            var gf = RepoFactory.GroupFilter.GetByID(id);
            if (gf == null) return BadRequest("No filter with id");
            if (!((GroupFilterType) gf.FilterType).HasFlag(GroupFilterType.Directory))
                return BadRequest("Filter should be a Directory Filter");
            return RepoFactory.GroupFilter.GetByParentID(id).Select(a => new Filter(HttpContext, a))
                .OrderBy(a => a.Name).ToList();
        }
        
        /// <summary>
        /// Get Conditions for Filter with id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}/Conditions")]
        public ActionResult<Filter.FilterConditions> GetFilterConditions(int id)
        {
            var gf = RepoFactory.GroupFilter.GetByID(id);
            if (gf == null) return BadRequest("No filter with id");
            return Filter.GetConditions(gf);
        }
        
        /// <summary>
        /// Get Sorting Criteria for Filter with id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}/Sorting")]
        public ActionResult<List<Filter.SortingCriteria>> GetFilterSortingCriteria(int id)
        {
            var gf = RepoFactory.GroupFilter.GetByID(id);
            if (gf == null) return BadRequest("No filter with id");
            return Filter.GetSortingCriteria(gf);
        }

        /// <summary>
        /// Preview the Groups that will be in the filter if the changes are applied
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        [HttpPost("Preview")]
        public ActionResult<List<Group>> PreviewFilterChanges(Filter.FullFilter filter)
        {
            SVR_GroupFilter gf = filter.ToServerModel();
            gf.CalculateGroupsAndSeries();

            if (!gf.GroupsIds.ContainsKey(User.JMMUserID)) return new List<Group>();
            return gf.GroupsIds[User.JMMUserID].Select(a => RepoFactory.AnimeGroup.GetByID(a))
                .Where(a => a != null).GroupFilterSort(gf).Select(a => new Group(HttpContext, a)).ToList();
        }
        
        /// <summary>
        /// Create or update a filter
        /// </summary>
        /// <param name="filter"></param>
        /// <returns>The resulting Filter, with ID</returns>
        [HttpPost]
        public ActionResult<Filter> SaveFilter(Filter.FullFilter filter)
        {
            SVR_GroupFilter gf = null;
            if (filter.IDs.ID != 0)
            {
                gf = RepoFactory.GroupFilter.GetByID(filter.IDs.ID);
                if (gf == null) return BadRequest("No Filter with ID");
            }
            gf = filter.ToServerModel(gf);
            gf.CalculateGroupsAndSeries();
            RepoFactory.GroupFilter.Save(gf);

            return new Filter(HttpContext, gf);
        }

        /// <summary>
        /// Delete a filter
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        public ActionResult DeleteFilter(int id)
        {
            var gf = RepoFactory.GroupFilter.GetByID(id);
            if (gf == null) return BadRequest("No filter with id");
            RepoFactory.GroupFilter.Delete(gf);
            return Ok();
        }
    }
}