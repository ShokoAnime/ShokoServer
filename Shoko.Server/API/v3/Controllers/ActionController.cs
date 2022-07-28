using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Tasks;

namespace Shoko.Server.API.v3.Controllers
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class ActionController : BaseController
    {
        public ActionController(ILogger<ActionController> logger)
        {
            Logger = logger;
        }
        
        private readonly ILogger<ActionController> Logger;

        #region Common Actions
        /// <summary>
        /// Run Import. This checks for new files, hashes them etc, scans Drop Folders, checks and scans for community site links (tvdb, trakt, moviedb, etc), and downloads missing images.
        /// </summary>
        /// <returns></returns>
        [HttpGet("RunImport")]
        public ActionResult RunImport()
        {
            ShokoServer.RunImport();
            return Ok();
        }

        /// <summary>
        /// Queues a task to import only new files found in the import folder
        /// </summary>
        /// <returns></returns>
        [HttpGet("ImportNewFiles")]
        public ActionResult ImportNewFiles()
        {
            Importer.RunImport_NewFiles();
            return Ok();
        }

        /// <summary>
        /// This was for web cache hash syncing, and will be for perceptual hashing maybe eventually.
        /// </summary>
        /// <returns></returns>
        [HttpGet("SyncHashes")]
        public ActionResult SyncHashes()
        {
            return BadRequest();
        }

        /// <summary>
        /// Sync the votes from Shoko to AniDB.
        /// </summary>
        /// <returns></returns>
        [HttpGet("SyncVotes")]
        public ActionResult SyncVotes()
        {
            new CommandRequest_SyncMyVotes().Save();
            return Ok();
        }

        /// <summary>
        /// Sync Trakt states. Requires Trakt to be set up, obviously
        /// </summary>
        /// <returns></returns>
        [HttpGet("SyncTrakt")]
        public ActionResult SyncTrakt()
        {
            if (!ServerSettings.Instance.TraktTv.Enabled ||
                string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken)) return BadRequest();
            new CommandRequest_TraktSyncCollection(true).Save();

            return Ok();
        }

        /// <summary>
        /// Remove Entries in the Shoko Database for Files that are no longer accessible
        /// </summary>
        /// <returns></returns>
        [HttpGet("RemoveMissingFiles/{removeFromMyList:bool?}")]
        public ActionResult RemoveMissingFiles(bool removeFromMyList = true)
        {
            ShokoServer.RemoveMissingFiles(removeFromMyList);
            return Ok();
        }

        /// <summary>
        /// Update All TvDB Series Info
        /// </summary>
        /// <returns></returns>
        [HttpGet("UpdateAllTvDBInfo")]
        public ActionResult UpdateAllTvDBInfo()
        {
            Importer.RunImport_UpdateTvDB(false);
            return Ok();
        }

        /// <summary>
        /// Updates and Downloads Missing Images
        /// </summary>
        /// <returns></returns>
        [HttpGet("UpdateAllImages")]
        public ActionResult UpdateAllImages()
        {
            ShokoServer.Instance.DownloadAllImages();
            return Ok();
        }

        /// <summary>
        /// Updates All MovieDB Info
        /// </summary>
        /// <returns></returns>
        [HttpGet("UpdateAllMovieDBInfo")]
        public ActionResult UpdateAllMovieDBInfo()
        {
            Task.Factory.StartNew(() => MovieDBHelper.UpdateAllMovieInfo(true));
            return Ok();
        }

        /// <summary>
        /// Update all Trakt info. Right now, that's not much.
        /// </summary>
        /// <returns></returns>
        [HttpGet("UpdateAllTraktInfo")]
        public ActionResult UpdateTraktInfo()
        {
            if (!ServerSettings.Instance.TraktTv.Enabled ||
                string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken)) return BadRequest();
            TraktTVHelper.UpdateAllInfo();
            return Ok();
        }

        /// <summary>
        /// Validates invalid images and redownloads them
        /// </summary>
        /// <returns></returns>
        [HttpGet("ValidateAllImages")]
        public ActionResult ValidateAllImages()
        {
            new CommandRequest_ValidateAllImages().Save();
            return Ok();
        }
        #endregion

        #region Admin Actions

        /// <summary>
        /// Gets files whose data does not match AniDB
        /// </summary>
        /// <returns></returns>
        [Authorize("admin")]
        [HttpGet("AVDumpMismatchedFiles")]
        public ActionResult AVDumpMismatchedFiles()
        {
            var allvids = RepoFactory.VideoLocal.GetAll().Where(vid => !vid.IsEmpty() && vid.Media != null)
                .ToDictionary(a => a, a => a.GetAniDBFile());
            Task.Factory.StartNew(() =>
            {
                var list = allvids.Keys.Select(vid => new {vid, anidb = allvids[vid]})
                    .Where(_tuple => _tuple.anidb != null)
                    .Where(_tuple => _tuple.anidb.IsDeprecated != 1)
                    .Where(_tuple => _tuple.vid.Media?.MenuStreams.Any() != (_tuple.anidb.IsChaptered == 1))
                    .Select(_tuple => _tuple.vid.GetBestVideoLocalPlace(true)?.FullServerPath)
                    .Where(path => !string.IsNullOrEmpty(path)).ToList();
                int index = 0;
                foreach (var path in list)
                {
                    Logger.LogInformation($"AVDump Start {index + 1}/{list.Count}: {path}");
                    AVDumpHelper.DumpFile(path);
                    Logger.LogInformation($"AVDump Finished {index + 1}/{list.Count}: {path}");
                    index++;
                    Logger.LogInformation($"AVDump Progress: {list.Count - index} remaining");
                }
            });

            return Ok();
        }

        /// <summary>
        /// This Downloads XML data from AniDB where there is none. This should only happen:
        /// A. If someone deleted or corrupted them.
        /// B. If the server closed unexpectedly at the wrong time (it'll only be one).
        /// C. If there was a catastrophic error.
        /// </summary>
        /// <returns></returns>
        [Authorize("admin")]
        [HttpGet("DownloadMissingAniDBAnimeData")]
        public ActionResult UpdateMissingAniDBXML()
        {
            try
            {
                var allAnime = RepoFactory.AniDB_Anime.GetAll().Select(a => a.AnimeID).OrderBy(a => a).ToList();
                Logger.LogInformation("Starting the check for {AllAnimeCount} anime XML files", allAnime.Count);
                int updatedAnime = 0;
                for (var i = 0; i < allAnime.Count; i++)
                {
                    var animeID = allAnime[i];
                    if (i % 10 == 1) Logger.LogInformation("Checking anime {I}/{AllAnimeCount} for XML file", i + 1, allAnime.Count);

                    var xmlUtils = HttpContext.RequestServices.GetRequiredService<HttpXmlUtils>();
                    var rawXml = xmlUtils.LoadAnimeHTTPFromFile(animeID);

                    if (rawXml != null) continue;
                    Series.QueueAniDBRefresh(HttpContext, animeID, true, false, false);
                    updatedAnime++;
                }
                Logger.LogInformation("Updating {UpdatedAnime} anime", updatedAnime);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error checking and queuing AniDB XML Updates: {E}", e);
                return InternalError(e.Message);
            }
            return Ok();
        }

        /// <summary>
        /// Regenerate All Episode Matchings for TvDB. Generally, don't do this unless there was an error that was fixed.
        /// In those cases, you'd be told to.
        /// </summary>
        /// <returns></returns>
        [Authorize("admin")]
        [HttpGet("RegenerateAllTvDBEpisodeMatchings")]
        public ActionResult RegenerateAllEpisodeLinks()
        {
            try
            {
                RepoFactory.CrossRef_AniDB_TvDB_Episode.DeleteAllUnverifiedLinks();
                RepoFactory.AnimeSeries.GetAll().ToList().AsParallel().ForAll(animeseries =>
                    TvDBLinkingHelper.GenerateTvDBEpisodeMatches(animeseries.AniDB_ID, true));
            }
            catch (Exception e)
            {
                Logger.LogError(e, e.Message);
                return InternalError(e.Message);
            }

            return Ok();
        }

        /// <summary>
        /// BEWARE this is a dangerous command!
        /// It syncs all of the states in Shoko's library to AniDB.
        /// ONE WAY. THIS CAN ERASE ANIDB DATA IRREVERSIBLY
        /// </summary>
        /// <returns></returns>
        [Authorize("admin")]
        [HttpGet("SyncMyList")]
        public ActionResult SyncMyList()
        {
            ShokoServer.SyncMyList();
            return Ok();
        }

        /// <summary>
        /// Update All AniDB Series Info
        /// </summary>
        /// <returns></returns>
        [Authorize("admin")]
        [HttpGet("UpdateAllAniDBInfo")]
        public ActionResult UpdateAllAniDBInfo()
        {
            Importer.RunImport_UpdateAllAniDB();
            return Ok();
        }

        /// <summary>
        /// Queues a task to Update all media info
        /// </summary>
        /// <returns></returns>
        [Authorize("admin")]
        [HttpGet("UpdateAllMediaInfo")]
        public ActionResult UpdateAllMediaInfo()
        {
            ShokoServer.RefreshAllMediaInfo();
            return Ok();
        }

        /// <summary>
        /// Queues commands to Update All Series Stats and Force a Recalculation of All Group Filters
        /// </summary>
        /// <returns></returns>
        [Authorize("admin")]
        [HttpGet("UpdateSeriesStats")]
        public ActionResult UpdateSeriesStats()
        {
            Importer.UpdateAllStats();
            return Ok();
        }

        /// <summary>
        /// Update the AniDB Calendar data for use on the dashboard.
        /// </summary>
        /// <returns></returns>
        [Authorize("admin")]
        [HttpGet("UpdateAniDBCalendar")]
        public ActionResult UpdateAniDBCalendarData()
        {
            Importer.CheckForCalendarUpdate(true);
            return Ok();
        }

        /// <summary>
        /// Recreate all <see cref="Group"/>s. This will delete any and all existing groups.
        /// </summary>
        /// <remarks>
        /// This action requires an admin account because it's a destructive action.
        /// </remarks>
        /// <returns></returns>
        [Authorize("admin")]
        [HttpGet("RecreateAllGroups")]
        public ActionResult RecreateAllGroups()
        {
            Task.Run(() => new AnimeGroupCreator().RecreateAllGroups());
            return Ok("Check the server status via init/status or SignalR's Events hub");
        }
        #endregion
    }
}
