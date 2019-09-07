using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
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
        [HttpGet("install")]
        public ActionResult InstallWebUI()
        {
            return WebUIGetUrlAndUpdate(WebUILatestStableVersion().version, "stable");
        }

        /// <summary>
        /// Download the latest stable version of WebUI
        /// </summary>
        /// <returns></returns>
        [HttpGet("update/stable")]
        public ActionResult WebUIStableUpdate()
        {
            return WebUIGetUrlAndUpdate(WebUILatestStableVersion().version, "stable");
        }

        /// <summary>
        /// Download the latest unstable version of WebUI
        /// </summary>
        /// <returns></returns>
        [HttpGet("update/unstable")]
        public ActionResult WebUIUnstableUpdate()
        {
            return WebUIGetUrlAndUpdate(WebUILatestUnstableVersion().version, "dev");
        }

        /// <summary>
        /// Get url for update and start update
        /// </summary>
        /// <param name="tag_name"></param>
        /// <returns></returns>
        internal ActionResult WebUIGetUrlAndUpdate(string tag_name, string channel)
        {
            try
            {
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls12;
                var client = new System.Net.WebClient();
                client.Headers.Add("Accept: application/vnd.github.v3+json");
                client.Headers.Add("User-Agent", "jmmserver");
                var response = client.DownloadString(
                    new Uri("https://api.github.com/repos/japanesemediamanager/shokoserver-webui/releases/tags/" +
                            tag_name));

                dynamic result = Newtonsoft.Json.JsonConvert.DeserializeObject(response);
                string url = string.Empty;
                foreach (dynamic obj in result.assets)
                {
                    if (obj.name == "latest.zip")
                    {
                        url = obj.browser_download_url;
                        break;
                    }
                }

                //check if tag was parsed corrently as it make the url
                return url != string.Empty 
                    ? WebUIUpdate(url, channel, tag_name) 
                    : new APIMessage(204, "Content is missing");
            }
            catch
            {
                return APIStatus.InternalError();
            }
        }

        /// <summary>
        /// Update WebUI with version from given url
        /// </summary>
        /// <param name="url">direct link to version you want to install</param>
        /// <returns></returns>
        internal ActionResult WebUIUpdate(string url, string channel, string version)
        {
            //list all files from root /webui/ and all directories
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "webui");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            string[] files = Directory.GetFiles(path);
            string[] directories = Directory.GetDirectories(path);

            try
            {
                //download latest version
                var client = new System.Net.WebClient();
                client.Headers.Add("User-Agent", "shokoserver");
                client.DownloadFile(url, Path.Combine(path, "latest.zip"));

                //create 'old' dictionary
                if (!Directory.Exists(Path.Combine(path, "old")))
                {
                    System.IO.Directory.CreateDirectory(Path.Combine(path, "old"));
                }
                try
                {
                    //move all directories and files to 'old' folder as fallback recovery
                    foreach (string dir in directories)
                    {
                        if (Directory.Exists(dir) && dir != Path.Combine(path, "old") && dir != Path.Combine(path, "tweak"))
                        {
                            string n_dir = dir.Replace(path, Path.Combine(path, "old"));
                            Directory.Move(dir, n_dir);
                        }
                    }
                    foreach (string file in files)
                    {
                        if (System.IO.File.Exists(file))
                        {
                            string n_file = file.Replace(path, Path.Combine(path, "old"));
                            System.IO.File.Move(file, n_file);
                        }
                    }

                    try
                    {
                        //extract latest webui
                        System.IO.Compression.ZipFile.ExtractToDirectory(Path.Combine(path, "latest.zip"), path);

                        //clean because we already have working updated webui
                        Directory.Delete(Path.Combine(path, "old"), true);
                        System.IO.File.Delete(Path.Combine(path, "latest.zip"));

                        //save version type>version that was installed successful
                        if (System.IO.File.Exists(Path.Combine(path, "index.ver")))
                        {
                            System.IO.File.Delete(Path.Combine(path, "index.ver"));
                        }
                        System.IO.File.AppendAllText(Path.Combine(path, "index.ver"), channel + ">" + version);

                        return APIStatus.OK();
                    }
                    catch
                    {
                        //when extracting latest.zip failes
                        return new APIMessage(405, "MethodNotAllowed");
                    }
                }
                catch
                {
                    //when moving files to 'old' folder failed
                    return new APIMessage(423, "Locked");
                }
            }
            catch
            {
                //when download failed
                return new APIMessage(499, "download failed");
            }
        }

        /// <summary>
        /// Check for newest stable version and return object { version: string, url: string }
        /// </summary>
        /// <returns></returns>
        [HttpGet("latest/stable")]
        public ComponentVersion WebUILatestStableVersion()
        {
            ComponentVersion version = new ComponentVersion();
            version = WebUIGetLatestVersion(true);

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
            version = WebUIGetLatestVersion(false);

            return version;
        }

        /// <summary>
        /// Find version that match requirements
        /// </summary>
        /// <param name="stable">do version have to be stable</param>
        /// <returns></returns>
        internal ComponentVersion WebUIGetLatestVersion(bool stable)
        {
            var client = new System.Net.WebClient();
            client.Headers.Add("Accept: application/vnd.github.v3+json");
            client.Headers.Add("User-Agent", "jmmserver");
            var response = client.DownloadString(new Uri(
                "https://api.github.com/repos/japanesemediamanager/shokoserver-webui/releases/latest"));

            dynamic result = Newtonsoft.Json.JsonConvert.DeserializeObject(response);

            ComponentVersion version = new ComponentVersion();

            if (result.prerelease == "False")
            {
                //not pre-build
                if (stable)
                {
                    version.version = result.tag_name;
                }
                else
                {
                    version.version = WebUIGetVersionsTag(false);
                }
            }
            else
            {
                //pre-build
                if (stable)
                {
                    version.version = WebUIGetVersionsTag(true);
                }
                else
                {
                    version.version = result.tag_name;
                }
            }

            return version;
        }

        /// <summary>
        /// Return tag_name of version that match requirements and is not present in /latest/
        /// </summary>
        /// <param name="stable">do version have to be stable</param>
        /// <returns></returns>
        internal string WebUIGetVersionsTag(bool stable)
        {
            var client = new System.Net.WebClient();
            client.Headers.Add("Accept: application/vnd.github.v3+json");
            client.Headers.Add("User-Agent", "shokoserver");
            var response = client.DownloadString(new Uri(
                "https://api.github.com/repos/japanesemediamanager/shokoserver-webui/releases"));

            dynamic result = Newtonsoft.Json.JsonConvert.DeserializeObject(response);

            foreach (dynamic obj in result)
            {
                if (stable)
                {
                    if (obj.prerelease == "False")
                    {
                        foreach (dynamic file in obj.assets)
                        {
                            if ((string) file.name == "latest.zip")
                            {
                                return obj.tag_name;
                            }
                        }
                    }
                }
                else
                {
                    if (obj.prerelease == "True")
                    {
                        foreach (dynamic file in obj.assets)
                        {
                            if ((string) file.name == "latest.zip")
                            {
                                return obj.tag_name;
                            }
                        }
                    }
                }
            }
            return string.Empty;
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
            else
                return new APIMessage(HttpStatusCode.NoContent, "");
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
