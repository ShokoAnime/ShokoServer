using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3
{
    [ApiController]
    [Authorize]
    [Route("/apiv3/episode")]
    public class EpisodeController : BaseController
    {
        [HttpGet("{id}/watched/{watched}")]
        public ActionResult SetWatchedStatusOnEpisode(int id, bool watched)
        {
            var ep = Repo.Instance.AnimeEpisode.GetByID(id);
            if (ep == null) return BadRequest("Could not get episode with ID: " + id);
            
            ep.ToggleWatchedStatus(watched, true, DateTime.Now, true, User.JMMUserID, true);
            return Ok();
        }
    }
}