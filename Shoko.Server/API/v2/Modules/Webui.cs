using Nancy.ModelBinding;
using Nancy.Security;
using Newtonsoft.Json;
using Pri.LongPath;
using System;
using Shoko.Server.API.Model.core;
using System.Collections.Generic;

namespace Shoko.Server.API.Module.apiv2
{
    public class Webui : Nancy.NancyModule
    {
        public static int version = 1;

        public Webui() : base("/api/webui")
        {
            this.RequiresAuthentication();

            Get["/install"] = _ => { return InstallWebUI(); };
            Get["/update/stable"] = _ => { return WebUIStableUpdate(); };
            Get["/latest/stable"] = _ => { return WebUILatestStableVersion(); };
            Get["/update/unstable"] = _ => { return WebUIUnstableUpdate(); };
            Get["/latest/unstable"] = _ => { return WebUILatestUnstableVersion(); };
            Get["/config"] = _ => { return GetWebUIConfig(); };
            Post["/config"] = _ => { return SetWebUIConfig(); };
            Get["/theme"] = _ => { return GetWebUIThemes(); };
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
                var response = client.DownloadString(new Uri("https://api.github.com/repos/japanesemediamanager/shokoserver-webui/releases/tags/" + tag_name));

                dynamic result = Newtonsoft.Json.JsonConvert.DeserializeObject(response);
                string url = "";
                foreach (dynamic obj in result.assets)
                {
                    if (obj.name == "latest.zip")
                    {
                        url = obj.browser_download_url;
                        break;
                    }
                }

                //check if tag was parsed corrently as it make the url
                if (url != "")
                {
                    return WebUIUpdate(url, channel, tag_name);
                }
                else
                {
                    return new APIMessage(204, "Content is missing");
                }
            }
            catch
            {
                return APIStatus.internalError();
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
                client.DownloadFile(url, "webui\\latest.zip");

                //create 'old' dictionary
                if (!Directory.Exists("webui\\old")) { System.IO.Directory.CreateDirectory("webui\\old"); }
                try
                {
                    //move all directories and files to 'old' folder as fallback recovery
                    foreach (string dir in directories)
                    {
                        if (Directory.Exists(dir) && dir != "webui\\old" && dir != "webui\\tweak")
                        {
                            string n_dir = dir.Replace("webui", "webui\\old");
                            Directory.Move(dir, n_dir);
                        }
                    }
                    foreach (string file in files)
                    {
                        if (File.Exists(file))
                        {
                            string n_file = file.Replace("webui", "webui\\old");
                            File.Move(file, n_file);
                        }
                    }

                    try
                    {
                        //extract latest webui
                        System.IO.Compression.ZipFile.ExtractToDirectory("webui\\latest.zip", "webui");

                        //clean because we already have working updated webui
                        Directory.Delete("webui\\old", true);
                        File.Delete("webui\\latest.zip");

                        //save version type>version that was installed successful
                        if (File.Exists("webui\\index.ver")) { File.Delete("webui\\index.ver"); }
                        File.AppendAllText("webui\\index.ver", channel + ">" + version);

                        return APIStatus.statusOK();
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
            var response = client.DownloadString(new Uri("https://api.github.com/repos/japanesemediamanager/shokoserver-webui/releases/latest"));

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
            var response = client.DownloadString(new Uri("https://api.github.com/repos/japanesemediamanager/shokoserver-webui/releases"));

            dynamic result = Newtonsoft.Json.JsonConvert.DeserializeObject(response);

            foreach (dynamic obj in result)
            {
                if (stable)
                {
                    if (obj.prerelease == "False")
                    {
                        foreach (dynamic file in obj.assets)
                        {
                            if ((string)file.name == "latest.zip")
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
                            if ((string)file.name == "latest.zip")
                            {
                                return obj.tag_name;
                            }
                        }
                    }
                }
            }
            return "";
        }

        /// <summary>
        /// Read json file that is converted into string from .config file of jmmserver
        /// </summary>
        /// <returns></returns>
        private object GetWebUIConfig()
        {
            if (!String.IsNullOrEmpty(ServerSettings.WebUI_Settings))
            {
                try
                {
                    WebUI_Settings settings = JsonConvert.DeserializeObject<WebUI_Settings>(ServerSettings.WebUI_Settings);
                    return settings;
                }
                catch
                {
                    return APIStatus.internalError("error while reading webui settings");
                }

            }
            else
            {
                return APIStatus.notFound404();
            }
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
                    ServerSettings.WebUI_Settings = JsonConvert.SerializeObject(settings);
                    return APIStatus.statusOK();
                }
                catch
                {
                    return APIStatus.internalError("error at saving webui settings");
                }
            }
            else
            {
                return new APIMessage(400, "Config is not a Valid.");
            }
        }

        /// <summary>
        /// List all available themes to use inside webui
        /// </summary>
        /// <returns>List<OSFile> with 'name' of css files</returns>
        private object GetWebUIThemes()
        {
            List<v2.Models.core.OSFile> files = new List<v2.Models.core.OSFile>();
            if (Directory.Exists("webui\\tweak"))
            {
                DirectoryInfo dir_info = new DirectoryInfo("webui\\tweak");
                foreach (FileInfo info in dir_info.GetFiles("*.css"))
                {
                    v2.Models.core.OSFile file = new v2.Models.core.OSFile();
                    file.name = info.Name;
                    files.Add(file);
                }
            }
            return files;
        }
    }
}
