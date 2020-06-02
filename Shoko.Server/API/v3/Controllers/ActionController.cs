using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;

namespace Shoko.Server.API.v3
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class ActionController : BaseController
    {
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
    }
}