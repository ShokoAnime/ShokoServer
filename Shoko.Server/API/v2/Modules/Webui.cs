using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v2.Modules
{
    [Authorize]
    [ApiController]
    [Route("/api/webui")]
    [ApiVersion("2.0")]
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
            var indexLocation = Path.Combine(ServerSettings.ApplicationPath, "webui", "index.html");
            if (System.IO.File.Exists(indexLocation))
            {
                var index = System.IO.File.ReadAllText(indexLocation);
                var token = "\"Baka, baka, baka!! They found out I was peeking! Now my research is ruined!\" - Jiraiya";
                if (!index.Contains(token)) return Unauthorized("If trying to update, use api/webui/update");
            }
            WebUIHelper.GetUrlAndUpdate(WebUILatestStableVersion().version, "stable");
            return Redirect("/webui/index.html");
        }

        /// <summary>
        /// Download the latest stable version of WebUI
        /// </summary>
        /// <returns></returns>
        [HttpGet("update/stable")]
        public ActionResult WebUIStableUpdate()
        {
            WebUIHelper.GetUrlAndUpdate(WebUILatestStableVersion().version, "stable");
            return Ok();
        }

        /// <summary>
        /// Download the latest unstable version of WebUI
        /// </summary>
        /// <returns></returns>
        [HttpGet("update/unstable")]
        public ActionResult WebUIUnstableUpdate()
        {
            WebUIHelper.GetUrlAndUpdate(WebUILatestUnstableVersion().version, "dev");
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
            ComponentVersion version = new ComponentVersion {version = WebUIHelper.WebUIGetLatestVersion(true)};

            return version;
        }

        /// <summary>
        /// Check for newest unstable version and return object { version: string, url: string }
        /// </summary>
        /// <returns></returns>
        [HttpGet("latest/unstable")]
        public ComponentVersion WebUILatestUnstableVersion()
        {
            ComponentVersion version = new ComponentVersion();
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
            if (!string.IsNullOrEmpty(ServerSettings.Instance.WebUI_Settings))
            {
                try
                {
                    return JsonConvert.DeserializeObject<WebUI_Settings>(ServerSettings.Instance.WebUI_Settings);
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
        public object SetWebUIConfig(WebUI_Settings settings)
        {
            if (settings.Valid())
            {
                try
                {
                    ServerSettings.Instance.WebUI_Settings = JsonConvert.SerializeObject(settings);
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
        /// <returns>List<OSFile> with 'name' of css files</returns>
        private object GetWebUIThemes()
        {
            List<OSFile> files = new List<OSFile>();
            if (Directory.Exists(Path.Combine("webui", "tweak")))
            {
                DirectoryInfo dir_info = new DirectoryInfo(Path.Combine("webui", "tweak"));
                foreach (FileInfo info in dir_info.GetFiles("*.css"))
                {
                    OSFile file = new OSFile
                    {
                        name = info.Name
                    };
                    files.Add(file);
                }
            }
            return files;
        }
    }
}
