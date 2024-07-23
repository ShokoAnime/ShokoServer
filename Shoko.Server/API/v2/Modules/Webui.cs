using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.API.WebUI;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.v2.Modules;

[Authorize]
[ApiController]
[Route("/api/webui")]
[ApiVersion("2.0")]
[InitFriendly]
[DatabaseBlockedExempt]
public class Webui : BaseController
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

        WebUIHelper.GetUrlAndUpdate(WebUILatestStableVersion().version);
        return Redirect("/webui/index.html");
    }

    /// <summary>
    /// Download the latest stable version of WebUI
    /// </summary>
    /// <returns></returns>
    [HttpGet("update/stable")]
    public ActionResult WebUIStableUpdate()
    {
        WebUIHelper.GetUrlAndUpdate(WebUILatestStableVersion().version);
        return Ok();
    }

    /// <summary>
    /// Download the latest unstable version of WebUI
    /// </summary>
    /// <returns></returns>
    [HttpGet("update/unstable")]
    public ActionResult WebUIUnstableUpdate()
    {
        WebUIHelper.GetUrlAndUpdate(WebUILatestUnstableVersion().version);
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
        var version = new ComponentVersion { version = WebUIHelper.WebUIGetLatestVersion(true) };

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
        version.version = WebUIHelper.WebUIGetLatestVersion(false);

        return version;
    }

    /// <summary>
    /// Read json file that is converted into string from .config file of jmmserver
    /// </summary>
    /// <returns></returns>
    [HttpGet("config")]
    public ActionResult<WebUI_Settings> GetWebUIConfig()
    {
        var setings = HttpContext.RequestServices.GetRequiredService<ISettingsProvider>().GetSettings();
        if (!string.IsNullOrEmpty(setings.WebUI_Settings))
        {
            try
            {
                return JsonConvert.DeserializeObject<WebUI_Settings>(setings.WebUI_Settings);
            }
            catch
            {
                return APIStatus.InternalError("error while reading webui settings");
            }
        }

        return APIStatus.OK();
    }

    /// <summary>
    /// Save webui settings as json converted into string inside .config file of jmmserver
    /// </summary>
    /// <returns></returns>
    [HttpPost("config")]
    public object SetWebUIConfig(WebUI_Settings webuiSettings)
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

    /// <summary>
    /// List all available themes to use inside webui
    /// </summary>
    /// <returns>List&lt;OSFile&gt; with 'name' of css files</returns>
    private object GetWebUIThemes()
    {
        var files = new List<OSFile>();
        if (Directory.Exists(Path.Combine("webui", "tweak")))
        {
            var dir_info = new DirectoryInfo(Path.Combine("webui", "tweak"));
            foreach (var info in dir_info.GetFiles("*.css"))
            {
                var file = new OSFile { name = info.Name };
                files.Add(file);
            }
        }

        return files;
    }

    public Webui(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
