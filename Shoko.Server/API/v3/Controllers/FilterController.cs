using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        [HttpPost("Preview")]
        public ActionResult<List<Group>> PreviewFilterChanges(Filter.FullFilter filter)
        {
            SVR_GroupFilter gf = filter.ToGroupFilter();
            gf.CalculateGroupsAndSeries();

            if (!gf.GroupsIds.ContainsKey(User.JMMUserID)) return new List<Group>();
            return gf.GroupsIds[User.JMMUserID].Select(a => RepoFactory.AnimeGroup.GetByID(a))
                .Where(a => a != null).GroupFilterSort(gf).Select(a => new Group(HttpContext, a)).ToList();
        }
        
        /// <summary>
        /// Create or update a filter
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult SaveFilter(Filter.FullFilter filter)
        {
            SVR_GroupFilter gf = null;
            if (filter.ID != 0)
            {
                gf = RepoFactory.GroupFilter.GetByID(filter.ID);
                if (gf == null) return BadRequest("No Filter with ID");
            }
            gf = filter.ToGroupFilter(gf);
            gf.CalculateGroupsAndSeries();
            RepoFactory.GroupFilter.Save(gf);

            return Ok();
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