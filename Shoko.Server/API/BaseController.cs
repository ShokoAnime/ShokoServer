using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Shoko.Server.Models;
using Shoko.Server.Settings;

namespace Shoko.Server.API;

/// <summary>
/// This controller should be the base for every other controller. It has overrides to do anything before or after requests.
/// An example is made for a request wide Random, solving the issue of a static Random somewhere/
/// </summary>
public class BaseController(ISettingsProvider settingsProvider) : Controller
{
    // Override Controller.User to be the SVR_JMMUser, since we'll almost never need HttpContext.User
    protected new SVR_JMMUser User => HttpContext.GetUser();

    protected readonly ISettingsProvider SettingsProvider = settingsProvider;

    [NonAction]
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        context.HttpContext.Items.Add("Random", new Random());
        base.OnActionExecuting(context);
    }

    [NonAction]
    protected ActionResult Forbid(string message = null)
    {
        if (message == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return StatusCode(StatusCodes.Status403Forbidden, message);
    }

    [NonAction]
    protected ActionResult InternalError(string message = null)
    {
        if (message == null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return StatusCode(StatusCodes.Status500InternalServerError, message);
    }

    [NonAction]
    protected ActionResult ValidationProblem(string message, string fieldName = "Body")
    {
        ModelState.AddModelError(fieldName, message);
        return ValidationProblem(ModelState);
    }

    [NonAction]
    protected ActionResult ValidationProblem(IEnumerable<KeyValuePair<string, IReadOnlyList<string>>> errors)
    {
        foreach (var (key, errorsList) in errors)
            foreach (var error in errorsList)
                ModelState.AddModelError(key, error);
        return ValidationProblem(ModelState);
    }
}
