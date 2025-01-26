using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.v2.Modules;

[Authorize]
[ApiController]
[Route("/api/webui")]
[ApiVersion("2.0")]
[InitFriendly]
[DatabaseBlockedExempt]
public class Webui(ISettingsProvider settingsProvider, WebUIUpdateService updateService) : BaseController(settingsProvider)
{
    /// <summary>
    /// Download and install latest stable version of WebUI
    /// </summary>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpGet("install")]
    public ActionResult InstallWebUI()
    {
        var indexLocation = Path.Combine(Utils.ApplicationPath, "webui", "index.html");
        if (System.IO.File.Exists(indexLocation))
        {
            var index = System.IO.File.ReadAllText(indexLocation);
            var token = "install-web-ui";
            if (!index.Contains(token))
            {
                return Unauthorized("If trying to update, use api/webui/update");
            }
        }

        updateService.GetUrlAndUpdate(WebUILatestStableVersion().version);
        return Redirect("/webui/index.html");
    }

    /// <summary>
    /// Download the latest stable version of WebUI
    /// </summary>
    /// <returns></returns>
    [HttpGet("update/stable")]
    public ActionResult WebUIStableUpdate()
    {
        updateService.GetUrlAndUpdate(WebUILatestStableVersion().version);
        return Ok();
    }

    /// <summary>
    /// Download the latest unstable version of WebUI
    /// </summary>
    /// <returns></returns>
    [HttpGet("update/unstable")]
    public ActionResult WebUIUnstableUpdate()
    {
        updateService.GetUrlAndUpdate(WebUILatestUnstableVersion().version);
        return Ok();
    }

    /// <summary>
    /// Check for newest stable version and return object { version: string, url: string }
    /// </summary>
    /// <returns></returns>
    [HttpGet("latest/stable")]
    [HttpGet("latest")]
    public ComponentVersion WebUILatestStableVersion()
    {
        var version = new ComponentVersion { version = updateService.WebUIGetLatestVersion(true) };

        return version;
    }

    /// <summary>
    /// Check for newest unstable version and return object { version: string, url: string }
    /// </summary>
    /// <returns></returns>
    [HttpGet("latest/unstable")]
    public ComponentVersion WebUILatestUnstableVersion()
    {
        var version = new ComponentVersion();
        version.version = updateService.WebUIGetLatestVersion(false);

        return version;
    }

    /// <summary>
    /// Read json file that is converted into string from .config file of Shoko.
    /// </summary>
    /// <returns></returns>
    [HttpGet("config")]
    public ActionResult<WebUI_Settings> GetWebUIConfig()
    {
        var settings = HttpContext.RequestServices.GetRequiredService<ISettingsProvider>().GetSettings();
        if (!string.IsNullOrEmpty(settings.WebUI_Settings))
        {
            try
            {
                return JsonConvert.DeserializeObject<WebUI_Settings>(settings.WebUI_Settings);
            }
            catch
            {
                return APIStatus.InternalError("error while reading webui settings");
            }
        }

        return APIStatus.OK();
    }

    /// <summary>
    /// Save webui settings as json converted into string inside .config file of Shoko.
    /// </summary>
    /// <returns></returns>
    [HttpPost("config")]
    public ActionResult SetWebUIConfig(WebUI_Settings webuiSettings)
    {
        if (webuiSettings.Valid())
        {
            try
            {
                var settingsProvider = HttpContext.RequestServices.GetRequiredService<ISettingsProvider>();
                var settings = settingsProvider.GetSettings();
                settings.WebUI_Settings = JsonConvert.SerializeObject(webuiSettings);
                settingsProvider.SaveSettings();
                return APIStatus.OK();
            }
            catch
            {
                return APIStatus.InternalError("error at saving webui settings");
            }
        }

        return new APIMessage(400, "Config is not a Valid.");
    }
}
