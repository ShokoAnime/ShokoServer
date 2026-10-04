using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Settings;

namespace Shoko.Server.API;

/// <summary>
/// This controller should be the base for every other controller. It has overrides to do anything before or after requests.
/// An example is made for a request wide Random, solving the issue of a static Random somewhere/
/// </summary>
public class BaseController(ISettingsProvider settingsProvider) : Controller
{
    // Override Controller.User to be the SVR_JMMUser, since we'll almost never need HttpContext.User
    protected new JMMUser User => HttpContext.GetUser();

    protected readonly ISettingsProvider SettingsProvider = settingsProvider;

    [NonAction]
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        context.HttpContext.Items.Add("Random", new Random());
        base.OnActionExecuting(context);
    }

    [NonAction]
    protected ActionResult Forbid(string? message = null)
    {
        if (message == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return StatusCode(StatusCodes.Status403Forbidden, message);
    }

    [NonAction]
    protected ActionResult InternalError(string? message = null)
    {
        if (message == null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return StatusCode(StatusCodes.Status500InternalServerError, message);
    }

    /// <summary>
    /// Sends an icon a plugin ships, with an ETag, so that an SVG opened on
    /// its own runs no script.
    /// </summary>
    /// <param name="icon">The icon, if there is one.</param>
    /// <param name="applicationPaths">Resolves the icon's path.</param>
    /// <param name="notFound">The message sent when there is no icon.</param>
    /// <returns>
    /// The icon, <c>304 Not Modified</c> when the client's copy has the same
    /// ETag, or <c>404 Not Found</c> when there is none.
    /// </returns>
    [NonAction]
    protected ActionResult PackageIcon(PackageImageInfo? icon, IApplicationPaths applicationPaths, string notFound)
    {
        if (icon is null || icon.GetStream(applicationPaths) is not { } stream)
            return NotFound(notFound);

        byte[] bytes;
        using (stream)
        {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            bytes = buffer.ToArray();
        }

        // The file result answers a matching If-None-Match with 304 by itself.
        Response.Headers.CacheControl = "private, max-age=86400";
        SetSandboxHeaders();
        var etag = new EntityTagHeaderValue($"\"{Convert.ToHexStringLower(SHA256.HashData(bytes), 0, 8)}\"");
        return File(bytes, icon.MimeType, null, etag);
    }

    /// <summary>
    /// Sends a stored image's file, so that an SVG opened on its own runs no
    /// script.
    /// </summary>
    /// <param name="stream">The image's file.</param>
    /// <param name="contentType">The image's media type.</param>
    /// <returns>The file.</returns>
    [NonAction]
    protected FileStreamResult ImageFile(Stream stream, string contentType)
    {
        SetSandboxHeaders();
        return File(stream, contentType);
    }

    /// <summary>
    /// Keeps the browser from guessing the response's type and runs a
    /// document opened from it, an SVG among them, in a sandbox.
    /// </summary>
    private void SetSandboxHeaders()
    {
        var headers = Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.ContentSecurityPolicy = "sandbox";
    }

    [NonAction]
    protected ActionResult ValidationProblem(IEnumerable<KeyValuePair<string, IReadOnlyList<string>>> errors, string? fieldName = null)
    {
        var prefix = string.IsNullOrEmpty(fieldName) ? string.Empty : fieldName + ".";
        foreach (var (key, errorsList) in errors)
            foreach (var error in errorsList)
                ModelState.AddModelError(prefix + key, error);
        return ValidationProblem(ModelState);
    }
}
