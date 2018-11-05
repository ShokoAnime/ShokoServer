using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3
{
    [ApiController]
    [Authorize]
    [Route("/apiv3/file")]
    public class FileController : BaseController
    {
        [HttpGet("{id}/watched/{watched}")]
        public ActionResult SetWatchedStatusOnFile(int id, bool watched)
        {
            var file = Repo.Instance.VideoLocal.GetByID(id);
            if (file == null) return BadRequest("Could not get the videolocal with ID: " + id);
            
            file.ToggleWatchedStatus(watched, User.JMMUserID);
            return Ok();
        }
    }
}