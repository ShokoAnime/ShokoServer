using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Core.API.Models;
using Shoko.Core.API.Services;
using Shoko.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Shoko.Core.API.Controllers
{
    [Authorize, ApiController, Route("/api/auth")]
    public class AuthenticationController : ControllerBase
    {
        private IUserService _userService;

        public AuthenticationController(IUserService userService)
        {
            _userService = userService;
        }

        [AllowAnonymous, HttpPost]
        public ActionResult<ShokoUser> Authenticate([FromBody] AuthenticationModel model)
        {
            var user = _userService.Authenticate(model.Username, model.Password);

            if (user == null) return BadRequest("Username or password is incorrect");

            return user;
        }
    }
}
