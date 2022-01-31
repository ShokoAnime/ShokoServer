using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Models.Enums;
using Shoko.Models.MediaInfo;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Commands;
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
        /// <param name="fileID">Shoko VideoLocalID</param>
        /// <returns></returns>
        [HttpGet("{fileID}")]
        public ActionResult<File> GetFile(int fileID)
        {
            var videoLocal = RepoFactory.VideoLocal.GetByID(fileID);
            if (videoLocal == null) return BadRequest("No File with ID");
            return new File(HttpContext, videoLocal);
        }
        
        /// <summary>
        /// Get the AniDB details for file with Shoko ID
        /// </summary>
        /// <param name="fileID">Shoko ID</param>
        /// <returns></returns>
        [HttpGet("{fileID}/AniDB")]
        public ActionResult<File.AniDB> GetFileAniDBDetails(int fileID)
        {
            var videoLocal = RepoFactory.VideoLocal.GetByID(fileID);
            if (videoLocal == null) return BadRequest("No File with ID");
            var anidb = videoLocal.GetAniDBFile();
            if (anidb == null) return BadRequest("AniDB data not found");
            return Models.Shoko.File.GetAniDBInfo(fileID);
        }
        
        /// <summary>
        /// Get the MediaInfo model for file with VideoLocal ID
        /// </summary>
        /// <param name="fileID">Shoko ID</param>
        /// <returns></returns>
        [HttpGet("{fileID}/MediaInfo")]
        public ActionResult<MediaContainer> GetFileMediaInfo(int fileID)
        {
            var videoLocal = RepoFactory.VideoLocal.GetByID(fileID);
            if (videoLocal == null) return BadRequest("No File with ID");
            return Models.Shoko.File.GetMedia(fileID);
        }
        
        /// <summary>
        /// Mark a file as watched or unwatched. Use the "Scrobble" endpoint instead.
        /// </summary>
        /// <param name="fileID">VideoLocal ID. Watched Status is kept per file, no matter how many copies or where they are.</param>
        /// <param name="watched">Is it watched?</param>
        /// <returns></returns>
        [HttpPost("{fileID}/watched/{watched}")]
        [Obsolete]
        public ActionResult SetWatchedStatusOnFile(int fileID, bool watched)
        {
            var file = RepoFactory.VideoLocal.GetByID(fileID);
            if (file == null) return BadRequest("Could not get the videolocal with ID: " + fileID);
            
            file.ToggleWatchedStatus(watched, User.JMMUserID);
            return Ok();
        }

        /// <summary>
        /// Update either watch status, resume position, or both.
        /// </summary>
        /// <param name="fileID">VideoLocal ID. Watch status and resume position is kept per file, regardless of how many duplicates the file has.</param>
        /// <param name="eventName">The name of the event that triggered the scrobble.</param>
        /// <param name="episodeID">The episode id to scrobble to trakt.</param>
        /// <param name="watched">True if file should be marked as watched, false if file should be unmarked, or null if it shall not be updated.</param>
        /// <param name="resumePosition">Number of ticks into the video to resume from, or null if it shall not be updated.</param>
        /// <returns></returns>
        [HttpPatch("{fileID}/Scrobble")]
        public ActionResult ScrobbleFileAndEpisode([FromRoute] int fileID, [FromQuery(Name = "event")] string eventName = null, [FromQuery] int? episodeID = null, [FromQuery] bool? watched = null, [FromQuery] long? resumePosition = null)
        {
            // Handle legacy scrobble events.
            if (string.IsNullOrEmpty(eventName)) {
                return ScrobbleStatusOnFile(fileID, watched, resumePosition);
            }

            var file = RepoFactory.VideoLocal.GetByID(fileID);
            if (file == null) return BadRequest("Could not get VideoLocal with ID: " + fileID);

            var episode = episodeID.HasValue ? RepoFactory.AnimeEpisode.GetByID(episodeID.Value) : file.GetAnimeEpisodes()?.FirstOrDefault();
            if (episode == null) return BadRequest("Could not get AnimeEpisode with ID: " + episodeID);
            
            var playbackPositionTicks = resumePosition ?? 0;
            var watchedTillCompletion = watched ?? false;
            if (playbackPositionTicks >= file.Duration) {
                watchedTillCompletion = true;
                playbackPositionTicks = 0;
            }

            switch (eventName) {
                // The playback was started.
                case "play":
                // The playback was resumed after a pause.
                case "resume":
                    ScrobbleToTrakt(file, episode, playbackPositionTicks, ScrobblePlayingStatus.Start);
                    break;
                // The playback was paused.
                case "pause":
                    ScrobbleToTrakt(file, episode, playbackPositionTicks, ScrobblePlayingStatus.Pause);
                    break;
                // The playback was ended.
                case "stop":
                    ScrobbleToTrakt(file, episode, playbackPositionTicks, ScrobblePlayingStatus.Stop);
                    break;
                // The playback is still active, but the playback position changed.
                case "scrobble":
                    break;
                // A user interaction caused the watch state to change.
                case "user-interaction":
                    break;
            }

            file.ToggleWatchedStatus(watchedTillCompletion, User.JMMUserID);
            file.SetResumePosition(playbackPositionTicks, User.JMMUserID);

            return NoContent();
        }

        [NonAction]
        private void ScrobbleToTrakt(SVR_VideoLocal file, SVR_AnimeEpisode episode, long position, ScrobblePlayingStatus status)
        {
            if (User.IsTraktUser == 0)
                return;
            
            float percentage = 100 * (position / file.Duration);
            ScrobblePlayingType scrobbleType = episode.GetAnimeSeries()?.GetAnime()?.AnimeType == (int) AnimeType.Movie
                ? ScrobblePlayingType.movie
                : ScrobblePlayingType.episode;
            
            TraktTVHelper.Scrobble(scrobbleType, episode.AnimeEpisodeID.ToString(), status, percentage);
        }
        
        [NonAction]
        private ActionResult ScrobbleStatusOnFile(int fileID, bool? watched, long? resumePosition)
        {
            var file = RepoFactory.VideoLocal.GetByID(fileID);
            if (file == null) return BadRequest("Could not get videolocal with ID: " + fileID);

            if (!(watched ?? false) && resumePosition != null)
            {
                var safeRP = resumePosition ?? 0;
                if (safeRP < 0) safeRP = 0;

                if (safeRP >= file.Duration)
                    watched = true;
                else
                    file.SetResumePosition(safeRP, User.JMMUserID);
            }

            if (watched != null)
            {
                var safeWatched = watched ?? false;
                file.ToggleWatchedStatus(safeWatched, User.JMMUserID);
                if (safeWatched)
                    file.SetResumePosition(0, User.JMMUserID);

            }

            return Ok();
        }

        /// <summary>
        /// Mark or unmark a file as ignored.
        /// </summary>
        /// <param name="fileID">VideoLocal ID</param>
        /// <param name="value">Thew new ignore value.</param>
        /// <returns></returns>
        [HttpPatch("{fileID}/Ignore")]
        public ActionResult IgnoreFile(int fileID, [FromQuery] bool value = true)
        {
            var vl = RepoFactory.VideoLocal.GetByID(fileID);
            if (vl == null) return NotFound();

            vl.IsIgnored = value ? 1 : 0;
            RepoFactory.VideoLocal.Save(vl, false);

            return Ok();
        }
        
        /// <summary>
        /// Run a file through AVDump and return the result.
        /// </summary>
        /// <param name="fileID">VideoLocal ID</param>
        /// <returns></returns>
        [HttpPost("{fileID}/avdump")]
        [Obsolete]
        public ActionResult<AVDumpResult> AvDumpFileObsolete(int fileID)
            => AvDumpFile(fileID);
        
        /// <summary>
        /// Run a file through AVDump and return the result.
        /// </summary>
        /// <param name="fileID">VideoLocal ID</param>
        /// <returns></returns>
        [HttpPost("{fileID}/AVDump")]
        public ActionResult<AVDumpResult> AvDumpFile(int fileID)
        {
            if (string.IsNullOrWhiteSpace(ServerSettings.Instance.AniDb.AVDumpKey))
                return BadRequest("Missing AVDump API key");
            
            var vl = RepoFactory.VideoLocal.GetByID(fileID);
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
        /// Rescan a file on AniDB.
        /// </summary>
        /// <param name="fileID">VideoLocal ID</param>
        /// <returns></returns>
        [HttpPost("{fileID}/Rescan")]
        public ActionResult RescanFile(int fileID)
        {
            var vl = RepoFactory.VideoLocal.GetByID(fileID);
            if (vl == null) return NotFound();

            var file = vl.GetBestVideoLocalPlace(true)?.FullServerPath;
            if (string.IsNullOrEmpty(file)) return this.NoContent();

            var command = new CommandRequest_ProcessFile(vl.VideoLocalID, true);
            command.Save();
            return Ok();
        }

        /// <summary>
        /// Rehash a file.
        /// </summary>
        /// <param name="fileID">VideoLocal ID</param>
        /// <returns></returns>
        [HttpPost("{fileID}/Rehash")]
        public ActionResult RehashFile(int fileID)
        {
            var vl = RepoFactory.VideoLocal.GetByID(fileID);
            if (vl == null) return NotFound();

            var file = vl.GetBestVideoLocalPlace(true)?.FullServerPath;
            if (string.IsNullOrEmpty(file)) return this.NoContent();

            var command = new CommandRequest_HashFile(file, true);
            command.Save();

            return Ok();
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
            query = query.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
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
            Regex regex;

            try
            {
                regex = new Regex(query, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            }
            catch (RegexParseException e)
            {
                return BadRequest(e.Message);
            }

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
        /// Get recently added files.
        /// </summary>
        /// <returns></returns>
        [HttpGet("Recent/{limit:int?}")]
        [Obsolete]
        public List<File.FileDetailed> GetRecentFilesObselete([FromRoute] int limit = 100)
            => GetRecentFiles(limit);

        /// <summary>
        /// Get recently added files.
        /// </summary>
        /// <param name="pageSize">Limits the number of results per page. Default is 100. Set to 0 to disable the limit.</param>
        /// <param name="page">Page number. Default is 0.</param>
        /// <returns></returns>
        [HttpGet("Recent")]
        public List<File.FileDetailed> GetRecentFiles([FromQuery] int pageSize = 100, [FromQuery] int page = 0)
        {
            if (pageSize <= 0) pageSize = -1;
            if (page <= 0) page = 0;
            var skip = pageSize == -1 ? 0 : pageSize * page;
            return RepoFactory.VideoLocal.GetMostRecentlyAdded(pageSize, skip, User.JMMUserID)
                .Select(file => new File.FileDetailed(HttpContext, file)).ToList();
        }

        /// <summary>
        /// Get ignored files.
        /// </summary>
        /// <param name="pageSize">Limits the number of results per page. Default is 100. Set to 0 to disable the limit.</param>
        /// <param name="page">Page number. Default is 0.</param>
        /// <returns></returns>
        [HttpGet("Ignored")]
        public List<File> GetIgnoredFiles([FromQuery] int pageSize = 100, [FromQuery] int page = 0)
        {
            if (pageSize <= 0)
                return RepoFactory.VideoLocal.GetIgnoredVideos().Select(a => new File(HttpContext, a)).ToList();
            if (page <= 0) page = 0;
            return RepoFactory.VideoLocal.GetIgnoredVideos().Skip(pageSize * page).Take(pageSize)
                .Select(a => new File(HttpContext, a)).ToList();
        }

        /// <summary>
        /// Get files with more than one location.
        /// </summary>
        /// <param name="pageSize">Limits the number of results per page. Default is 100. Set to 0 to disable the limit.</param>
        /// <param name="page">Page number. Default is 0.</param>
        /// <returns></returns>
        [HttpGet("Duplicates")]
        public List<File> GetExactDuplicateFileSets([FromQuery] int pageSize = 100, [FromQuery] int page = 0)
        {
            if (pageSize <= 0)
                return RepoFactory.VideoLocal.GetExactDuplicateVideos().Select(a => new File(HttpContext, a)).ToList();
            if (page <= 0) page = 0;
            return RepoFactory.VideoLocal.GetExactDuplicateVideos().Skip(pageSize * page).Take(pageSize)
                .Select(a => new File(HttpContext, a)).ToList();
        }

        /// <summary>
        /// Get unrecognized files. <see cref="File.FileDetailed"/> is not relevant here, as there will be no links.
        /// Use pageSize and page (index 0) in the query to enable pagination.
        /// </summary>
        /// <param name="pageSize">Limits the number of results per page. Default is 100. Set to 0 to disable the limit.</param>
        /// <param name="page">Page number. Default is 0.</param>
        [HttpGet("Unrecognized")]
        public List<File> GetUnrecognizedFiles(int pageSize = 100, int page = 0)
        {
            if (pageSize <= 0)
                return RepoFactory.VideoLocal.GetVideosWithoutEpisode().Select(a => new File(HttpContext, a)).ToList();
            if (page <= 0) page = 0;
            return RepoFactory.VideoLocal.GetVideosWithoutEpisode().Skip(pageSize * page).Take(pageSize)
                .Select(a => new File(HttpContext, a)).ToList();
        }

        /// <summary>
        /// Delete a file.
        /// </summary>
        /// <param name="fileID">The VideoLocal_Place ID. This cares about which location we are deleting from.</param>
        /// <param name="removeFolder">This causes the empty folder removal to skipped if set to false. 
        /// This significantly speeds up batch deleting if you are deleting many files in the same folder. 
        /// It may be specified in the query.</param>
        /// <returns></returns>
        [Authorize("admin")]
        [HttpDelete("{fileID}")]
        public ActionResult DeleteFile(int fileID, [FromQuery] bool removeFolder = true)
        {
            var file = RepoFactory.VideoLocalPlace.GetByID(fileID);
            if (file == null) return BadRequest("Could not get the VideoLocal_Place with ID: " + fileID);
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
