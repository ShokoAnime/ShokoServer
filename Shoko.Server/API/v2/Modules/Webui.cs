using System;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Plugin.Abstractions;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Server;
using Shoko.Server.Services;

using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

namespace Shoko.Server.API.v2.Modules;

[Authorize]
[ApiController]
[Route("/api/webui")]
[ApiVersion("2.0")]
[InitFriendly]
[DatabaseBlockedExempt]
public class Webui(ISettingsProvider settingsProvider, IApplicationPaths applicationPaths, WebUIUpdateService updateService) : BaseController(settingsProvider)
{
    /// <summary>
    /// Download and install latest stable version of WebUI
    /// </summary>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpGet("install")]
    public ActionResult InstallWebUI()
    {
        var indexLocation = Path.Join(applicationPaths.WebPath, "index.html");
        if (System.IO.File.Exists(indexLocation))
        {
            var index = System.IO.File.ReadAllText(indexLocation);
            var token = "install-web-ui";
            if (!index.Contains(token))
            {
                return Unauthorized("If trying to update, use api/webui/update");
            }
        }

        updateService.InstallUpdateForChannel(ReleaseChannel.Stable);
        return Redirect("/webui/index.html");
    }

    /// <summary>
    /// Download the latest stable version of WebUI
    /// </summary>
    /// <returns></returns>
    [HttpGet("update/stable")]
    public ActionResult WebUIStableUpdate()
    {
        updateService.InstallUpdateForChannel(ReleaseChannel.Stable);
        return Ok();
    }

    /// <summary>
    /// Download the latest unstable version of WebUI
    /// </summary>
    /// <returns></returns>
    [HttpGet("update/unstable")]
    public ActionResult WebUIUnstableUpdate()
    {
        updateService.InstallUpdateForChannel(ReleaseChannel.Dev);
        return Ok();
    }

    /// <summary>
    /// Check for newest stable version and return object { version: string, url: string }
    /// </summary>
    /// <returns></returns>
    [HttpGet("latest/stable")]
    [HttpGet("latest")]
    public ComponentVersion WebUILatestStableVersion()
        => new() { version = updateService.GetLatestVersion(ReleaseChannel.Stable).Version };

    /// <summary>
    /// Check for newest unstable version and return object { version: string, url: string }
    /// </summary>
    /// <returns></returns>
    [HttpGet("latest/unstable")]
    public ComponentVersion WebUILatestUnstableVersion()
        => new() { version = updateService.GetLatestVersion(ReleaseChannel.Dev).Version };

    /// <summary>
    /// Read json file that is converted into string from .config file of Shoko.
    /// </summary>
    /// <returns></returns>
    [Obsolete("Use APIv3 to get web ui settings")]
    [HttpGet("config")]
    public ActionResult<WebUI_Settings> GetWebUIConfig()
        => new APIMessage(400, "Config is not a Valid.");

    /// <summary>
    /// Save webui settings as json converted into string inside .config file of Shoko.
    /// </summary>
    /// <returns></returns>
    [Obsolete("Use APIv3 to get web ui settings")]
    [HttpPost("config")]
    public ActionResult SetWebUIConfig(WebUI_Settings webuiSettings)
        => new APIMessage(400, "Config is not a Valid.");
}
