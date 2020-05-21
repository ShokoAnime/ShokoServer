using System;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class FileController : BaseController
    {
        /// <summary>
        /// Get File Details
        /// </summary>
        /// <param name="id">Shoko VideoLocalID</param>
        /// <returns></returns>
        [HttpGet("{id}")]
        public ActionResult<File> GetFile(int id)
        {
            var videoLocal = RepoFactory.VideoLocal.GetByID(id);
            if (videoLocal == null) return BadRequest("No File with ID");
            return new File(videoLocal);
        }
        
        /// <summary>
        /// Get the AniDB details for episode with Shoko ID
        /// </summary>
        /// <param name="id">Shoko ID</param>
        /// <returns></returns>
        [HttpGet("{id}/AniDB")]
        public ActionResult<File.AniDB> GetFileAniDBDetails(int id)
        {
            var videoLocal = RepoFactory.VideoLocal.GetByID(id);
            if (videoLocal == null) return BadRequest("No File with ID");
            var anidb = videoLocal.GetAniDBFile();
            if (anidb == null) return BadRequest("AniDB data not found");
            return v3.File.GetAniDBInfo(id);
        }
        
        /// <summary>
        /// Mark a file as watched or unwatched
        /// </summary>
        /// <param name="id">VideoLocal ID. Watched Status is kept per file, no matter how many copies or where they are.</param>
        /// <param name="watched">Is it watched?</param>
        /// <returns></returns>
        [HttpPost("{id}/watched/{watched}")]
        public ActionResult SetWatchedStatusOnFile(int id, bool watched)
        {
            var file = RepoFactory.VideoLocal.GetByID(id);
            if (file == null) return BadRequest("Could not get the videolocal with ID: " + id);
            
            file.ToggleWatchedStatus(watched, User.JMMUserID);
            return Ok();
        }

        /// <summary>
        /// Delete a file.
        /// </summary>
        /// <param name="id">The VideoLocal_Place ID. This cares about which location we are deleting from.</param>
        /// <param name="removeFolder">This causes the empty folder removal to skipped if set to false. 
        /// This significantly speeds up batch deleting if you are deleting many files in the same folder. 
        /// It may be specified in the query.</param>
        /// <returns></returns>
        [Authorize("admin")]
        [HttpDelete("{id}")]
        public ActionResult DeleteFile(int id, [FromQuery] bool removeFolder = true)
        {
            var file = RepoFactory.VideoLocalPlace.GetByID(id);
            if (file == null) return BadRequest("Could not get the VideoLocal_Place with ID: " + id);
            try
            {
                file.RemoveRecordAndDeletePhysicalFile(removeFolder);
                return Ok();
            }
            catch (Exception e)
            {
                return new APIMessage(HttpStatusCode.InternalServerError, e.Message);
            }
        }
    }
}