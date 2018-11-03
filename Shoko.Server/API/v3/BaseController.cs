using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Shoko.Server.API.v3
{
    /// <summary>
    /// This controller should be the base for every other controller. It has overrides to do anything before or after requests.
    /// An example is made for a request wide Random, solving the issue of a static Random somewhere/
    /// </summary>
    public class BaseController : Controller
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
            HttpContext.Items.Add("Random", new Random());
        }
    }
}