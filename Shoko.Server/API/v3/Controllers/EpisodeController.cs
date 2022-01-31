using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Commons.Extensions;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Controllers
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class EpisodeController : BaseController
    {
        /// <summary>
        /// Get an Episode by Shoko ID
        /// </summary>
        /// <param name="episodeID">Shoko ID</param>
        /// <returns></returns>
        [HttpGet("{episodeID}")]
        public ActionResult<Episode> GetEpisode([FromRoute] int episodeID)
        {
            var ep = RepoFactory.AnimeEpisode.GetByID(episodeID);
            if (ep == null) return BadRequest("No Episode with ID");
            return new Episode(HttpContext, ep);
        }

        /// <summary>
        /// Get the AniDB details for episode with Shoko ID
        /// </summary>
        /// <param name="episodeID">Shoko ID</param>
        /// <returns></returns>
        [HttpGet("{episodeID}/AniDB")]
        public ActionResult<Episode.AniDB> GetEpisodeAniDBDetails([FromRoute] int episodeID)
        {
            var ep = RepoFactory.AnimeEpisode.GetByID(episodeID);
            if (ep == null) return BadRequest("No Episode with ID");
            var anidb = ep.AniDB_Episode;
            if (anidb == null) return BadRequest("AniDB data not found");
            return Episode.GetAniDBInfo(anidb);
        }

        /// <summary>
        /// Add a permanent user-submitted rating for the episode.
        /// </summary>
        /// <param name="episodeID"></param>
        /// <param name="vote"></param>
        /// <returns></returns>
        [HttpPost("{episodeID}/Vote")]
        public ActionResult PostEpisodeVote([FromRoute] int episodeID, [FromBody] Vote vote)
        {
            if (vote.Value < 0)
                return BadRequest("Value must be greater than or equal to 0.");
            if (vote.Value > vote.MaxValue)
                return BadRequest($"Value must be less than or equal to the set max value ({vote.MaxValue}).");
            if (vote.MaxValue <= 0)
                return BadRequest("Max value must be an integer above 0.");
            var ep = RepoFactory.AnimeEpisode.GetByID(episodeID);
            if (ep == null) return BadRequest("No Episode with ID");
            Episode.AddEpisodeVote(HttpContext, ep, User.JMMUserID, vote);
            return NoContent();
        }

        /// <summary>
        /// Get the TvDB details for episode with Shoko ID
        /// </summary>
        /// <param name="episodeID">Shoko ID</param>
        /// <returns></returns>
        [HttpGet("{episodeID}/TvDB")]
        public ActionResult<List<Episode.TvDB>> GetEpisodeTvDBDetails([FromRoute] int episodeID)
        {
            var ep = RepoFactory.AnimeEpisode.GetByID(episodeID);
            if (ep == null) return BadRequest("No Episode with ID");
            return Episode.GetTvDBInfo(ep.AniDB_EpisodeID);
        }

        /// <summary>
        /// Set the watched status on an episode
        /// </summary>
        /// <param name="episodeID">Shoko ID</param>
        /// <param name="watched"></param>
        /// <returns></returns>
        [HttpPost("{episodeID}/watched/{watched}")]
        public ActionResult SetWatchedStatusOnEpisode([FromRoute] int episodeID, [FromRoute] bool watched)
        {
            var ep = RepoFactory.AnimeEpisode.GetByID(episodeID);
            if (ep == null) return BadRequest("Could not get episode with ID: " + episodeID);

            ep.ToggleWatchedStatus(watched, true, DateTime.Now, true, User.JMMUserID, true);
            return Ok();
        }
        
        /// <summary>
        /// Get episodes with multiple files attached.
        /// </summary>
        /// <param name="ignoreVariations">Ignore manually toggled variations in the results. Defaults to false.</param>
        /// <param name="onlyFinishedSeries">Only show finished series. Defaults to true.</param>
        /// <param name="pageSize">Limits the number of results per page. Default is 100. Set to 0 to disable the limit.</param>
        /// <param name="page">Page number. Default is 0.</param>
        /// <returns></returns>
        [HttpGet("WithMultipleFiles")]
        public List<Episode> GetSoftDuplicatesForEpisode([FromQuery] bool ignoreVariations = true, [FromQuery] bool onlyFinishedSeries = false, [FromQuery] int pageSize = 100, [FromQuery] int page = 0)
        {
            IEnumerable<SVR_AnimeEpisode> enumerable = RepoFactory.AnimeEpisode.GetEpisodesWithMultipleFiles(ignoreVariations);
            if (onlyFinishedSeries)
            {
                var dictSeriesFinishedAiring = RepoFactory.AnimeSeries.GetAll()
                    .Select(a => new Tuple<int, bool>(a.AnimeSeriesID, a.GetAnime().GetFinishedAiring())).ToDictionary(k => k.Item1, v => v.Item2);
                enumerable = enumerable.Where(episode => !(dictSeriesFinishedAiring.TryGetValue(episode.AnimeSeriesID, out var finishedAiring) && finishedAiring));
            }

            if (pageSize <= 0)
                return enumerable.Select(a => new Episode(HttpContext, a)).ToList();
            if (page <= 0) page = 0;
            return enumerable.Skip(pageSize * page).Take(pageSize)
                .Select(a => new Episode(HttpContext, a)).ToList();
        }
    }
}
