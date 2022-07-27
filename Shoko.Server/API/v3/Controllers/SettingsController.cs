using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize(Roles = "admin,init")]
    [InitFriendly]
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
        /// <param name="skipValidation">Skip Model Validation. Use with caution</param>
        /// <returns></returns>
        [HttpPatch]
        public ActionResult SetSettings([FromBody] JsonPatchDocument<ServerSettings> settings, bool skipValidation = false)
        {
            if (settings == null) return BadRequest("The settings object is invalid.");
            settings.ApplyTo(ServerSettings.Instance, ModelState);
            if (!skipValidation)
            {
                TryValidateModel(ServerSettings.Instance);
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
            }

            ServerSettings.Instance.SaveSettings();
            return Ok();
        }

        /// <summary>
        /// Tests a Login with the given Credentials. This does not save the credentials.
        /// </summary>
        /// <param name="credentials">POST the body as a <see cref="Credentials"/> object</param>
        /// <returns></returns>
        [HttpPost("AniDB/TestLogin")]
        public ActionResult TestAniDB([FromBody] Credentials credentials)
        {
            if (string.IsNullOrWhiteSpace(credentials.Username) || string.IsNullOrWhiteSpace(credentials.Password))
                return BadRequest("AniDB needs both a username and a password");

            var handler = HttpContext.RequestServices.GetRequiredService<IUDPConnectionHandler>();
            handler.ForceLogout();

            if (!handler.TestLogin(credentials.Username, credentials.Password)) return BadRequest("Failed to log in");

            return Ok();
        }
    }
}
