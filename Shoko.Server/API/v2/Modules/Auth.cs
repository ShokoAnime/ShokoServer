using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v2.Modules
{
    [ApiController]
    [Route("/api/auth")]
    //[ApiVersion("2.0")]
    [ApiVersionNeutral]
    public class Auth : BaseController
    {
        /// <summary>
        /// Authentication module
        /// </summary>
        public Auth()// : base("/api/auth")
        {
        }

        /// <summary>
        /// Get an authentication token for the user.
        /// </summary>
        /// <param name="auth">The authentiction details for the user.</param>
        /// <returns>HTTP 400, 401 or 200 with an APIKey response.</returns>
        [HttpPost(""), ProducesResponseType(400), ProducesResponseType(401), ProducesResponseType(200)]
        public ActionResult<dynamic> Login([FromBody] AuthUser auth)
        {
            if (!ModelState.IsValid || string.IsNullOrEmpty(auth.user?.Trim()))
                return BadRequest(ModelState);

            if (auth.pass == null) auth.pass = string.Empty;

            //create and save new token for authenticated user or return known one
            string apiKey = RepoFactory.AuthTokens.ValidateUser(auth.user.Trim(), auth.pass.Trim(), auth.device.Trim());

            if (!string.IsNullOrEmpty(apiKey)) return Ok(new { apikey = apiKey });

            return Unauthorized();
        }

        ///<summary>
        ///Delete an APIKey from the database.
        ///</summary>
        ///<param name="apikey">The API key to delete.</param>
        [HttpDelete("")]
        public ActionResult Delete(string apikey)
        {
            RepoFactory.AuthTokens.DeleteWithToken(apikey);
            return Ok();
        }
    }
}