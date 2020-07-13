using System;
using System.Linq;
using System.Threading.Tasks;
using AniDBAPI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NLog;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Commands;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class ActionController : BaseController
    {
        
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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
                    Logger.Info($"AVDump Start {index + 1}/{list.Count}: {path}");
                    AVDumpHelper.DumpFile(path);
                    Logger.Info($"AVDump Finished {index + 1}/{list.Count}: {path}");
                    index++;
                    Logger.Info($"AVDump Progress: {list.Count - index} remaining");
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
        [HttpGet("DownloadMissingAniDBAnimeData")]
        public ActionResult UpdateMissingAniDBXML()
        {
            try
            {
                var allAnime = RepoFactory.AniDB_Anime.GetAll().Select(a => a.AnimeID).OrderBy(a => a).ToList();
                Logger.Info($"Starting the check for {allAnime.Count} anime XML files");
                int updatedAnime = 0;
                for (var i = 0; i < allAnime.Count; i++)
                {
                    var animeID = allAnime[i];
                    if (i % 10 == 1) Logger.Info($"Checking anime {i + 1}/{allAnime.Count} for XML file");

                    var xml = APIUtils.LoadAnimeHTTPFromFile(animeID);
                    if (xml == null)
                    {
                        CommandRequest_GetAnimeHTTP cmd = new CommandRequest_GetAnimeHTTP(animeID, true, false);
                        cmd.Save();
                        updatedAnime++;
                        continue;
                    }

                    var rawAnime = AniDBHTTPHelper.ProcessAnimeDetails(xml, animeID);
                    if (rawAnime == null)
                    {
                        CommandRequest_GetAnimeHTTP cmd = new CommandRequest_GetAnimeHTTP(animeID, true, false);
                        cmd.Save();
                        updatedAnime++;
                    }
                }
                Logger.Info($"Updating {updatedAnime} anime");
            }
            catch (Exception e)
            {
                Logger.Error($"Error checking and queuing AniDB XML Updates: {e}");
                return APIStatus.InternalError(e.Message);
            }
            return APIStatus.OK();
        }
        
        /// <summary>
        /// Regenerate All Episode Matchings for TvDB. Generally, don't do this unless there was an error that was fixed.
        /// In those cases, you'd be told to.
        /// </summary>
        /// <returns></returns>
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
                Logger.Error(e);
                return APIStatus.InternalError(e.Message);
            }

            return APIStatus.OK();
        }
        
        /// <summary>
        /// BEWARE this is a dangerous command!
        /// It syncs all of the states in Shoko's library to AniDB.
        /// ONE WAY. THIS CAN ERASE ANIDB DATA IRREVERSIBLY
        /// </summary>
        /// <returns></returns>
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
        [HttpGet("UpdateSeriesStats")]
        public ActionResult UpdateSeriesStats()
        {
            Importer.UpdateAllStats();
            return Ok();
        }
        #endregion
    }
}