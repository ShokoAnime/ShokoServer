using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        /// <summary>
        /// Delete a group recursively.
        /// </summary>
        /// <param name="groupID">The ID of the group to delete</param>
        /// <param name="deleteSeries">Whether to delete the series in the group. It will error if this is false and the group is not empty.</param>
        /// <param name="deleteFiles">Whether to delete the all of the files in the group from the disk.</param>
        /// <returns></returns>
        [Authorize("admin")]
        [HttpDelete("{groupID}")]
        public ActionResult DeleteGroup(int groupID, bool deleteSeries = false, bool deleteFiles = false)
        {
            var grp = RepoFactory.AnimeGroup.GetByID(groupID);
            if (grp == null) return BadRequest("No Group with ID");
            if (!deleteSeries && grp.GetAllSeries().Any())
                return BadRequest(
                    $"{nameof(deleteSeries)} is not true, and the group contains series. Move them, or set {nameof(deleteSeries)} to true");

            foreach (var series in grp.GetAllSeries())
            {
                series.DeleteSeries(deleteFiles, false);
            }

            grp.DeleteGroup();

            return Ok();
        }

        [Authorize("admin")]
        [HttpGet("RecreateAllGroups")]
        public ActionResult RecreateAllGroups()
        {
            Task.Run(() => new AnimeGroupCreator().RecreateAllGroups());
            return Ok("Check the server status via init/status or SignalR's Events hub");
        }
    }
}
