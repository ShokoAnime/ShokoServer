using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Core.API.Models;
using Shoko.Core.API.Services;
using System.Linq;
using Microsoft.AspNetCore.Http;
using System;

namespace Shoko.Core.API.Controllers
{
    [Authorize("admin"), ApiController, Route("/api/auth")]
    public class AuthenticationController : ControllerBase
    {
        private IUserService _userService;

        public AuthenticationController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// Authenticate with the model.
        /// This runs a standard password OAuth2 flow for password grants
        /// </summary>
        /// <param name="model">The OAuth2 request params.</param>
        /// <returns></returns>
        [AllowAnonymous, HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesDefaultResponseType]
        public ActionResult<OAuthResponse> Authenticate([FromForm] AuthenticationModel model)
        {
            if (model.GrantType.Equals("password", StringComparison.InvariantCultureIgnoreCase))
                return BadRequest("Must be password grant_type");

            var response = _userService.Authenticate(model.Username, model.Password);

            if (response == null) return Unauthorized("Username or password is incorrect");

            return response;
        }

        /// <summary>
        /// Get the current User ID
        /// </summary>
        /// <returns>The GUID user iD</returns>
        [HttpGet("me/id")]
        public string GetUser()
        {
            return HttpContext.User.Claims.First().Value;
        }
    }
}
