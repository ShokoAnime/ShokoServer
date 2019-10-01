using System;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3
{
    [ApiController, Route("/api/{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class FileController : BaseController
    {
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