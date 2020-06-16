using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.Commands;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class ActionController : BaseController
    {

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
        /// Sync the votes from Shoko to AniDB.
        /// </summary>
        /// <returns></returns>
        [HttpGet("SyncVotes")]
        public ActionResult SyncVotes()
        {
            CommandRequest_SyncMyVotes cmdVotes = new CommandRequest_SyncMyVotes();
            cmdVotes.Save();
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
            CommandRequest_TraktSyncCollection cmd = new CommandRequest_TraktSyncCollection(true);
            cmd.Save();

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
        /// Queues commands to Update All Series Stats and Force a Recalculation of All Group Filters
        /// </summary>
        /// <returns></returns>
        [HttpGet("UpdateSeriesStats")]
        public ActionResult UpdateSeriesStats()
        {
            Importer.UpdateAllStats();
            return Ok();
        }
    }
}