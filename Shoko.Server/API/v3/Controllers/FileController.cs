using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3
{
    [ApiController, Route("/api/{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class FileController : BaseController
    {
        [HttpPost("{id}/watched/{watched}")]
        public ActionResult SetWatchedStatusOnFile(int id, bool watched)
        {
            var file = Repo.Instance.VideoLocal.GetByID(id);
            if (file == null) return BadRequest("Could not get the videolocal with ID: " + id);
            
            file.ToggleWatchedStatus(watched, User.JMMUserID);
            return Ok();
        }
    }
}