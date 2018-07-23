using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Nancy.ModelBinding;
using Nancy.Security;
using Newtonsoft.Json;
using Shoko.Server.API.v2.Models.core;

namespace Shoko.Server.API.v2.Modules
{
    public class Webui : Nancy.NancyModule
    {
        public Webui() : base("/api/webui")
        {
            this.RequiresAuthentication();

            Get("/install", async (x,ct) => await Task.Factory.StartNew(InstallWebUI, ct));
            Get("/update/stable", async (x,ct) => await Task.Factory.StartNew(WebUIStableUpdate, ct));
            Get("/latest/stable", async (x,ct) => await Task.Factory.StartNew(WebUILatestStableVersion, ct));
            Get("/update/unstable", async (x,ct) => await Task.Factory.StartNew(WebUIUnstableUpdate, ct));
            Get("/latest/unstable", async (x,ct) => await Task.Factory.StartNew(WebUILatestUnstableVersion, ct));
            Get("/config", async (x,ct) => await Task.Factory.StartNew(GetWebUIConfig, ct));
            Post("/config", async (x,ct) => await Task.Factory.StartNew(SetWebUIConfig, ct));
            Get("/theme", async (x,ct) => await Task.Factory.StartNew(GetWebUIThemes, ct));
        }

        /// <summary>
        /// Download and install latest stable version of WebUI
        /// </summary>
        /// <returns></returns>
        private object InstallWebUI()
        {
            return WebUIGetUrlAndUpdate(WebUILatestStableVersion().version, "stable");
        }

        /// <summary>
        /// Download the latest stable version of WebUI
        /// </summary>
        /// <returns></returns>
        private object WebUIStableUpdate()
        {
            return WebUIGetUrlAndUpdate(WebUILatestStableVersion().version, "stable");
        }

        /// <summary>
        /// Download the latest unstable version of WebUI
        /// </summary>
        /// <returns></returns>
        private object WebUIUnstableUpdate()
        {
            return WebUIGetUrlAndUpdate(WebUILatestUnstableVersion().version, "dev");
        }

        /// <summary>
        /// Get url for update and start update
        /// </summary>
        /// <param name="tag_name"></param>
        /// <returns></returns>
        internal object WebUIGetUrlAndUpdate(string tag_name, string channel)
        {
            try
            {
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
        internal object WebUIUpdate(string url, string channel, string version)
        {
            //list all files from root /webui/ and all directories
            string[] files = Directory.GetFiles("webui");
            string[] directories = Directory.GetDirectories("webui");

            try
            {
                //download latest version
                var client = new System.Net.WebClient();
                client.Headers.Add("User-Agent", "shokoserver");
                client.DownloadFile(url, Path.Combine("webui", "latest.zip"));

                //create 'old' dictionary
                if (!Directory.Exists(Path.Combine("webui", "old")))
                {
                    System.IO.Directory.CreateDirectory(Path.Combine("webui", "old"));
                }
                try
                {
                    //move all directories and files to 'old' folder as fallback recovery
                    foreach (string dir in directories)
                    {
                        if (Directory.Exists(dir) && dir != Path.Combine("webui", "old") && dir != Path.Combine("webui", "tweak"))
                        {
                            string n_dir = dir.Replace("webui", Path.Combine("webui", "old"));
                            Directory.Move(dir, n_dir);
                        }
                    }
                    foreach (string file in files)
                    {
                        if (File.Exists(file))
                        {
                            string n_file = file.Replace("webui", Path.Combine("webui", "old"));
                            File.Move(file, n_file);
                        }
                    }

                    try
                    {
                        //extract latest webui
                        System.IO.Compression.ZipFile.ExtractToDirectory(Path.Combine("webui", "latest.zip"), "webui");

                        //clean because we already have working updated webui
                        Directory.Delete(Path.Combine("webui", "old"), true);
                        File.Delete(Path.Combine("webui", "latest.zip"));

                        //save version type>version that was installed successful
                        if (File.Exists(Path.Combine("webui", "index.ver")))
                        {
                            File.Delete(Path.Combine("webui", "index.ver"));
                        }
                        File.AppendAllText(Path.Combine("webui", "index.ver"), channel + ">" + version);

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
        private ComponentVersion WebUILatestStableVersion()
        {
            ComponentVersion version = new ComponentVersion();
            version = WebUIGetLatestVersion(true);

            return version;
        }

        /// <summary>
        /// Check for newest unstable version and return object { version: string, url: string }
        /// </summary>
        /// <returns></returns>
        private ComponentVersion WebUILatestUnstableVersion()
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
        private object GetWebUIConfig()
        {
            if (!String.IsNullOrEmpty(ServerSettings.Instance.WebUI_Settings))
            {
                try
                {
                    WebUI_Settings settings =
                        JsonConvert.DeserializeObject<WebUI_Settings>(ServerSettings.Instance.WebUI_Settings);
                    return settings;
                }
                catch
                {
                    return APIStatus.InternalError("error while reading webui settings");
                }
            }
            else
                return APIStatus.NotFound();
        }

        /// <summary>
        /// Save webui settings as json converted into string inside .config file of jmmserver
        /// </summary>
        /// <returns></returns>
        private object SetWebUIConfig()
        {
            WebUI_Settings settings = this.Bind();
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
            List<v2.Models.core.OSFile> files = new List<v2.Models.core.OSFile>();
            if (Directory.Exists(Path.Combine("webui", "tweak")))
            {
                DirectoryInfo dir_info = new DirectoryInfo(Path.Combine("webui", "tweak"));
                foreach (FileInfo info in dir_info.GetFiles("*.css"))
                {
                    v2.Models.core.OSFile file = new v2.Models.core.OSFile
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