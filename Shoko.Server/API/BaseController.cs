using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Shoko.Server.Models;

namespace Shoko.Server.API
{
    /// <summary>
    /// This controller should be the base for every other controller. It has overrides to do anything before or after requests.
    /// An example is made for a request wide Random, solving the issue of a static Random somewhere/
    /// </summary>
    public class BaseController : Controller
    {
        // Override Controller.User to be the SVR_JMMUser, since we'll almost never need HttpContext.User
        protected new SVR_JMMUser User => HttpContext.GetUser();
        
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            context.HttpContext.Items.Add("Random", new Random());
            base.OnActionExecuting(context);
        }

        protected ActionResult InternalError(string message = null)
        {
            if (message == null) return StatusCode(StatusCodes.Status500InternalServerError);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}