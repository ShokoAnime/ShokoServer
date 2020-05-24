using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class EpisodeController : BaseController
    {
        /// <summary>
        /// Get an Episode by Shoko ID
        /// </summary>
        /// <param name="id">Shoko ID</param>
        /// <returns></returns>
        [HttpGet("{id}")]
        public ActionResult<Episode> GetEpisode(int id)
        {
            var ep = RepoFactory.AnimeEpisode.GetByID(id);
            if (ep == null) return BadRequest("No Episode with ID");
            return new Episode(HttpContext, ep);
        }

        /// <summary>
        /// Get the AniDB details for episode with Shoko ID
        /// </summary>
        /// <param name="id">Shoko ID</param>
        /// <returns></returns>
        [HttpGet("{id}/AniDB")]
        public ActionResult<Episode.AniDB> GetEpisodeAniDBDetails(int id)
        {
            var ep = RepoFactory.AnimeEpisode.GetByID(id);
            if (ep == null) return BadRequest("No Episode with ID");
            var anidb = ep.AniDB_Episode;
            if (anidb == null) return BadRequest("AniDB data not found");
            return Episode.GetAniDBInfo(anidb);
        }
        
        /// <summary>
        /// Get the TvDB details for episode with Shoko ID
        /// </summary>
        /// <param name="id">Shoko ID</param>
        /// <returns></returns>
        [HttpGet("{id}/TvDB")]
        public ActionResult<List<Episode.TvDB>> GetEpisodeTvDBDetails(int id)
        {
            var ep = RepoFactory.AnimeEpisode.GetByID(id);
            if (ep == null) return BadRequest("No Episode with ID");
            return Episode.GetTvDBInfo(ep.AniDB_EpisodeID);
        }
        
        /// <summary>
        /// Set the watched status on an episode
        /// </summary>
        /// <param name="id">Shoko ID</param>
        /// <param name="watched"></param>
        /// <returns></returns>
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