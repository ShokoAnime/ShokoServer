using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3
{
    [ApiController, Route("/api/{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class EpisodeController : BaseController
    {
        [HttpGet("{id}")]
        public ActionResult<Episode> GetEpisode(int id)
        {
            var ep = RepoFactory.AnimeEpisode.GetByID(id);
            if (ep == null) return BadRequest("No Episode with ID");
            return new Episode(HttpContext, ep);
        }

        [HttpPost("{id}/watched/{watched}")]
        public ActionResult SetWatchedStatusOnEpisode(int id, bool watched)
        {
            var ep = RepoFactory.AnimeEpisode.GetByID(id);
            if (ep == null) return BadRequest("Could not get episode with ID: " + id);
            
            ep.ToggleWatchedStatus(watched, true, DateTime.Now, true, User.JMMUserID, true);
            return Ok();
        }
    }
}