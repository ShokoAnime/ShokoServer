using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize(Roles = "admin,init")]
    public class SettingsController : BaseController
    {
        // As far as I can tell, only GET and PATCH should be supported, as we don't support unset settings.
        // Some may be patched to "", though.
        
        // TODO some way of distinguishing what a normal user vs an admin can set.
        
        /// <summary>
        /// Get all settings
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public ActionResult<ServerSettings> GetSettings()
        {
            return ServerSettings.Instance;
        }

        /// <summary>
        /// JsonPatch the settings
        /// </summary>
        /// <param name="settings">JsonPatch operations</param>
        /// <returns></returns>
        [HttpPatch]
        public ActionResult SetSettings([FromBody] JsonPatchDocument<ServerSettings> settings)
        {
            if (settings == null) return BadRequest("The settings object is invalid.");
            settings.ApplyTo(ServerSettings.Instance, ModelState);
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            ServerSettings.Instance.SaveSettings();
            return Ok();
        }
    }
}
