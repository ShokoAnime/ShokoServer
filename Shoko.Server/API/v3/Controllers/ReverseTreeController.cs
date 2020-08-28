using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Models.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Controllers
{
    /// <summary>
    /// This Controller is intended to provide the reverse tree. It is used to get the series from episodes, etc.
    /// This is to support filtering with Apply At Series Level and any other situations that might involve the need for it.
    /// </summary>
    [ApiController, Route("/api/v{version:apiVersion}"), ApiV3]
    [Authorize]
    public class ReverseTreeController : BaseController
    {
        /// <summary>
        /// Get Group for Series with seriesID.
        /// </summary>
        /// <returns></returns>
        [HttpGet("Series/{seriesID}/Group")]
        public ActionResult<Group> GetGroupFromSeries(int seriesID)
        {
            var ser = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (ser == null) return BadRequest("No Series with ID");
            if (!User.AllowedSeries(ser)) return BadRequest("Series not allowed for current user");
            var group = ser.AnimeGroup;
            if (group == null) return BadRequest("Group not found for series");
            return new Group(HttpContext, group);
        }

        /// <summary>
        /// Get Series for episode with epID.
        /// </summary>
        /// <returns></returns>
        [HttpGet("Episode/{epID}/Series")]
        public ActionResult<Series> GetSeriesFromEpisode(int epID)
        {
            var episode = RepoFactory.AnimeEpisode.GetByID(epID);
            if (episode == null) return BadRequest("No episode with ID");
            var ser = episode.GetAnimeSeries();
            if (!User.AllowedSeries(ser)) return BadRequest("Series not allowed for current user");
            return new Series(HttpContext, ser);
        }

        /// <summary>
        /// Get Episode for file with fileID.
        /// </summary>
        /// <returns></returns>
        [HttpGet("File/{fileID}/Episode")]
        public ActionResult<List<Episode>> GetEpisodeFromFile(int fileID)
        {
            var videoLocal = RepoFactory.VideoLocal.GetByID(fileID);
            if (videoLocal == null) return BadRequest("No file with ID");
            var eps = videoLocal.GetAnimeEpisodes();
            if (!eps.All(a => User.AllowedSeries(a.GetAnimeSeries()))) return BadRequest("Series not allowed for current user");
            return eps.Select(a => new Episode(HttpContext, a)).ToList();
        }
    }
}