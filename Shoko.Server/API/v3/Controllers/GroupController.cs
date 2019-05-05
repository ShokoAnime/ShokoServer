using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3
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
        /// Get Default series for group with ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}/DefaultSeries")]
        [ProducesResponseType(typeof(Group), StatusCodes.Status200OK), ProducesResponseType(StatusCodes.Status202Accepted)]
        public ActionResult<Series> GetDefaultSeries(int id)
        {
            var grp = RepoFactory.AnimeGroup.GetByID(id);
            if (grp == null) return BadRequest("No Group with ID");
            int? defaultSeriesID = grp.DefaultAnimeSeriesID;
            if (defaultSeriesID == null) return Accepted("Group does not have a default series");
            var ser = RepoFactory.AnimeSeries.GetByID(defaultSeriesID.Value);
            if (ser == null) return BadRequest("No Series with ID");

            return new Series(HttpContext, ser);
        }
    }
}
