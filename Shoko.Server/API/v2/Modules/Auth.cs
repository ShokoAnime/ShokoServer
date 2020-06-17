using System;
using Microsoft.AspNetCore.Mvc;
using NLog;
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
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        
        /// <summary>
        /// Get an authentication token for the user.
        /// </summary>
        /// <param name="auth">The authentication details for the user.</param>
        /// <returns>HTTP 400, 401 or 200 with an APIKey response.</returns>
        [HttpPost, ProducesResponseType(400), ProducesResponseType(401), ProducesResponseType(200)]
        public ActionResult<dynamic> Login(AuthUser auth)
        {
            if (!ModelState.IsValid || string.IsNullOrEmpty(auth.user?.Trim()))
                return BadRequest(ModelState);

            if (auth.pass == null) auth.pass = string.Empty;

            //create and save new token for authenticated user or return known one
            string apiKey = RepoFactory.AuthTokens.ValidateUser(auth.user.Trim(), auth.pass.Trim(), auth.device.Trim());

            if (!string.IsNullOrEmpty(apiKey)) return Ok(new { apikey = apiKey });

            return Unauthorized();
        }

        /// <summary>
        /// Change the password. Invalidates the current user's apikeys. Reauth after using this!
        /// </summary>
        /// <param name="newPassword"></param>
        /// <returns></returns>
        [HttpPost("ChangePassword")]
        public ActionResult ChangePassword([FromBody] string newPassword)
        {
            try
            {
                User.Password = Digest.Hash(newPassword.Trim());
                RepoFactory.JMMUser.Save(User, false);
                RepoFactory.AuthTokens.DeleteAllWithUserID(User.JMMUserID);
                return Ok();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return InternalError();
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