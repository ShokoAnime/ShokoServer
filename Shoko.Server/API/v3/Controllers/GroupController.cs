using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Tasks;

namespace Shoko.Server.API.v3.Controllers
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class GroupController : BaseController
    {
        /// <summary>
        /// Get a list of all groups available to the current user
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public ActionResult<List<Group>> GetAllGroups()
        {
            var allGroups = RepoFactory.AnimeGroup.GetAll().Where(a => User.AllowedGroup(a)).ToList();
            return allGroups.Select(a => new Group(HttpContext, a)).ToList();
        }

        /// <summary>
        /// Get the group with ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}")]
        public ActionResult<Group> GetGroup(int id)
        {
            var grp = RepoFactory.AnimeGroup.GetByID(id);
            if (grp == null) return BadRequest("No Group with ID");
            return new Group(HttpContext, grp);
        }

        /// <summary>
        /// Save or create a group.
        /// Use <see cref="SeriesController.MoveSeries"/> to move series to the group.
        /// </summary>
        /// <param name="group"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult<Group> SaveGroup(Group.FullGroup group)
        {
            SVR_AnimeGroup g = null;
            if (group.IDs.ID != 0)
            {
                g = RepoFactory.AnimeGroup.GetByID(group.IDs.ID);
                if (g == null) return BadRequest("No Group with ID");
            }
            g = group.ToServerModel(g);
            RepoFactory.AnimeGroup.Save(g);

            return new Group(HttpContext, g);
        }

        /// <summary>
        /// Recalculate all stats and contracts for a group
        /// </summary>
        /// <param name="groupID"></param>
        /// <returns></returns>
        [HttpPost("{groupID}/Recalculate")]
        public ActionResult RecalculateStats(int groupID)
        {
            var grp = RepoFactory.AnimeGroup.GetByID(groupID);
            if (grp == null) return BadRequest("No Group with ID");
            AnimeGroupCreator groupCreator = new AnimeGroupCreator();
            groupCreator.RecalculateStatsContractsForGroup(grp);
            return Ok();
        }
        
        
    }
}
