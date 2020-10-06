using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Models.MediaInfo;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using File = Shoko.Server.API.v3.Models.Shoko.File;

namespace Shoko.Server.API.v3.Controllers
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
            return new File(HttpContext, videoLocal);
        }
        
        /// <summary>
        /// Get the AniDB details for file with Shoko ID
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
            return Models.Shoko.File.GetAniDBInfo(id);
        }
        
        /// <summary>
        /// Get the MediaInfo model for file with VideoLocal ID
        /// </summary>
        /// <param name="id">Shoko ID</param>
        /// <returns></returns>
        [HttpGet("{id}/MediaInfo")]
        public ActionResult<MediaContainer> GetFileMediaInfo(int id)
        {
            var videoLocal = RepoFactory.VideoLocal.GetByID(id);
            if (videoLocal == null) return BadRequest("No File with ID");
            return Models.Shoko.File.GetMedia(id);
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
        /// Update either watch status, resume position, or both.
        /// </summary>
        /// <param name="id">VideoLocal ID. Watch status and resume position is kept per file, regardless of how many duplicates the file has.</param>
        /// <param name="watched">True if file should be marked as watched, false if file should be unmarked, or null if it shall not be updated.</param>
        /// <param name="resumePosition">Number of ticks into the video to resume from, or null if it shall not be updated.</param>
        /// <returns></returns>
        [HttpPatch("{id}/Scrobble")]
        public ActionResult ScrobbleStatusOnFile(int id, [FromQuery] bool? watched = null, [FromQuery] long? resumePosition = null)
        {
            var file = RepoFactory.VideoLocal.GetByID(id);
            if (file == null) return BadRequest("Could not get videolocal with ID: " + id);

            if (watched != null)
            {
                file.ToggleWatchedStatus(watched ?? false, User.JMMUserID);
                file.SetResumePosition(0, User.JMMUserID);
            } 
            else if (resumePosition != null)
            {
                file.SetResumePosition(resumePosition ?? 0, User.JMMUserID);
            }
            return Ok();
        }
        
        /// <summary>
        /// Run a file through AVDump
        /// </summary>
        /// <param name="id">VideoLocal ID</param>
        /// <returns></returns>
        [HttpPost("{id}/avdump")]
        public ActionResult<AVDumpResult> AvDumpFile(int id)
        {
            if (string.IsNullOrWhiteSpace(ServerSettings.Instance.AniDb.AVDumpKey))
                return BadRequest("Missing AVDump API key");
            
            var vl = RepoFactory.VideoLocal.GetByID(id);
            if (vl == null) return NotFound();
            
            var file = vl.GetBestVideoLocalPlace(true)?.FullServerPath;
            if (string.IsNullOrEmpty(file)) return this.NoContent();
            
            var result = AVDumpHelper.DumpFile(file).Replace("\r", "");

            return new AVDumpResult()
            {
                FullOutput = result,
                Ed2k = result.Split('\n').FirstOrDefault(s => s.Trim().Contains("ed2k://"))
            };
        }

        /// <summary>
        /// Search for a file by path or name. Internally, it will convert / to the system directory separator and match against the string
        /// </summary>
        /// <param name="path">a path to search for. URL Encoded</param>
        /// <returns></returns>
        [HttpGet("PathEndsWith/{*path}")]
        public ActionResult<List<File.FileDetailed>> SearchByFilename(string path)
        {
            var query = path;
            if (query.Contains("%") || query.Contains("+")) query = Uri.UnescapeDataString(query);
            if (query.Contains("%")) query = Uri.UnescapeDataString(query);
            query = query.Replace('/', Path.DirectorySeparatorChar);
            var results = RepoFactory.VideoLocalPlace.GetAll().AsParallel()
                .Where(a => a.FullServerPath.EndsWith(query, StringComparison.OrdinalIgnoreCase)).Select(a => a.VideoLocal)
                .Distinct()
                .Where(a =>
                {
                    var ser = a?.GetAnimeEpisodes().FirstOrDefault()?.GetAnimeSeries();
                    return ser == null || User.AllowedSeries(ser);
                }).Select(a => new File.FileDetailed(HttpContext, a)).ToList();
            return results;
        }
        
        /// <summary>
        /// Search for a file by path or name via regex. Internally, it will convert \/ to the system directory separator and match against the string
        /// </summary>
        /// <param name="path">a path to search for. URL Encoded</param>
        /// <returns></returns>
        [HttpGet("PathRegex/{*path}")]
        public ActionResult<List<File.FileDetailed>> RegexSearchByFilename(string path)
        {
            var query = path;
            if (query.Contains("%") || query.Contains("+")) query = Uri.UnescapeDataString(query);
            if (query.Contains("%")) query = Uri.UnescapeDataString(query);
            if (Path.DirectorySeparatorChar == '\\') query = query.Replace("\\/", "\\\\");
            var regex = new Regex(query, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

            var results = RepoFactory.VideoLocalPlace.GetAll().AsParallel()
                .Where(a => regex.IsMatch(a.FullServerPath)).Select(a => a.VideoLocal)
                .Distinct()
                .Where(a =>
                {
                    var ser = a?.GetAnimeEpisodes().FirstOrDefault()?.GetAnimeSeries();
                    return ser == null || User.AllowedSeries(ser);
                }).Select(a => new File.FileDetailed(HttpContext, a)).ToList();
            return results;
        }
        
        /// <summary>
        /// Get Recently Added Files
        /// </summary>
        /// <returns></returns>
        [HttpGet("Recent/{limit:int?}")]
        public List<File.FileDetailed> GetRecentFiles(int limit = 100)
        {
            if (limit <= 0) limit = -1;
            return RepoFactory.VideoLocal.GetMostRecentlyAdded(limit, User.JMMUserID)
                .Select(file => new File.FileDetailed(HttpContext, file)).ToList();
        }

        /// <summary>
        /// Get Unrecognized Files. <see cref="File.FileDetailed"/> is not relevant here, as there will be no links.
        /// Use pageSize and page (index 0) in the query to enable pagination.
        /// </summary>
        /// <returns></returns>
        [HttpGet("Unrecognized")]
        public List<File> GetUnrecognizedFiles(int pageSize = 100, int page = 0)
        {
            if (pageSize <= 0)
                return RepoFactory.VideoLocal.GetVideosWithoutEpisode().Select(a => new File(HttpContext, a)).ToList();
            return RepoFactory.VideoLocal.GetVideosWithoutEpisode().Skip(pageSize * page).Take(pageSize)
                .Select(a => new File(HttpContext, a)).ToList();
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
