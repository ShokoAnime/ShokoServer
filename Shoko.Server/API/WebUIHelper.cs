using System;
using System.IO;
using System.Net;
using System.Reflection;
using Newtonsoft.Json;
using SharpCompress.Common;
using SharpCompress.Readers;
using Shoko.Server.Settings;

namespace Shoko.Server.API
{
    public static class WebUIHelper
    {
        /// <summary>
        /// Get url for update and start update
        /// </summary>
        /// <param name="tagName"></param>
        /// <param name="channel"></param>
        /// <returns></returns>
        public static void GetUrlAndUpdate(string tagName, string channel)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            var client = new WebClient();
            client.Headers.Add("Accept: application/vnd.github.v3+json");
            client.Headers.Add("User-Agent", "shokoserver");
            var response = client.DownloadString(
                new Uri("https://api.github.com/repos/shokoanime/shokoserver-webui/releases/tags/" +
                        tagName));

            dynamic result = JsonConvert.DeserializeObject(response);
            string url = string.Empty;
            foreach (dynamic obj in result.assets)
            {
                if (obj.name == "latest.zip")
                {
                    url = obj.browser_download_url;
                    break;
                }
            }

            //check if tag was parsed correctly as it makes the url
            if (string.IsNullOrWhiteSpace(url)) throw new Exception("204 not found"); 
            WebUIUpdate(url, channel, tagName);
        }

        /// <summary>
        /// Update WebUI with version from given url
        /// </summary>
        /// <param name="url">direct link to version you want to install</param>
        /// <param name="channel"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public static void WebUIUpdate(string url, string channel, string version)
        {
            // TODO New path
            //list all files from root /webui/ and all directories
            string path = Path.Combine(ServerSettings.ApplicationPath, "webui");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            string[] files = Directory.GetFiles(path);
            string[] directories = Directory.GetDirectories(path);

            try
            {
                //download latest version
                var client = new WebClient();
                client.Headers.Add("User-Agent", "shokoserver");
                client.DownloadFile(url, Path.Combine(path, "latest.zip"));
            }
            catch (Exception e)
            {
                //when download failed
                throw new Exception($"Unable to download WebUI: {e}");
            }

            try
            {
                //create 'old' dictionary
                if (!Directory.Exists(Path.Combine(path, "old")))
                {
                    Directory.CreateDirectory(Path.Combine(path, "old"));
                }

                //move all directories and files to 'old' folder as fallback recovery
                foreach (string dir in directories)
                {
                    if (!Directory.Exists(dir) || dir == Path.Combine(path, "old") ||
                        dir == Path.Combine(path, "tweak")) continue;
                    string n_dir = dir.Replace(path, Path.Combine(path, "old"));
                    Directory.Move(dir, n_dir);
                }
                foreach (string file in files)
                {
                    if (!File.Exists(file)) continue;
                    string n_file = file.Replace(path, Path.Combine(path, "old"));
                    File.Move(file, n_file);
                }
            }
            catch (Exception e)
            {
                //when moving files to 'old' folder failed
                throw new Exception($"Unable to move old WebUI: {e}");
            }

            try
            {
                //extract latest webui
                // TODO Extract with SharpCompress to path
                using (var stream = new FileStream(Path.Combine(path, "latest.zip"), FileMode.Open))
                using (var reader = ReaderFactory.Open(stream))
                {
                    while (reader.MoveToNextEntry())
                    {
                        if (!reader.Entry.IsDirectory)
                        {
                            reader.WriteEntryToDirectory(path, new ExtractionOptions
                            {
                                // This may have serious problems in the future, but for now, AVDump is flat
                                ExtractFullPath = true,
                                Overwrite = true
                            });
                        }
                    }
                }

                //clean because we already have working updated webui
                Directory.Delete(Path.Combine(path, "old"), true);
                File.Delete(Path.Combine(path, "latest.zip"));

                //save version type>version that was installed successful
                if (File.Exists(Path.Combine(path, "index.ver")))
                {
                    File.Delete(Path.Combine(path, "index.ver"));
                }
                File.AppendAllText(Path.Combine(path, "index.ver"), channel + ">" + version);
            }
            catch (Exception e)
            {
                //when extracting latest.zip fails
                throw new Exception($"Unable to extract WebUI: {e}");
            }
        }
        
        /// <summary>
        /// Find version that match requirements
        /// </summary>
        /// <param name="stable">do version have to be stable</param>
        /// <returns></returns>
        public static string WebUIGetLatestVersion(bool stable)
        {
            var client = new WebClient();
            client.Headers.Add("Accept: application/vnd.github.v3+json");
            client.Headers.Add("User-Agent", "shokoserver");
            var response = client.DownloadString(new Uri(
                "https://api.github.com/repos/shokoanime/shokoserver-webui/releases/latest"));

            dynamic result = JsonConvert.DeserializeObject(response);

            string version;

            if (result?.prerelease == "False")
            {
                //not pre-build
                version = stable ? result.tag_name : WebUIGetVersionsTag(false);
            }
            else
            {
                //pre-build
                version = stable ? WebUIGetVersionsTag(true) : result?.tag_name;
            }

            return version;
        }

        /// <summary>
        /// Return tag_name of version that match requirements and is not present in /latest/
        /// </summary>
        /// <param name="stable">do version have to be stable</param>
        /// <returns></returns>
        public static string WebUIGetVersionsTag(bool stable)
        {
            var client = new WebClient();
            client.Headers.Add("Accept: application/vnd.github.v3+json");
            client.Headers.Add("User-Agent", "shokoserver");
            var response = client.DownloadString(new Uri(
                "https://api.github.com/repos/shokoanime/shokoserver-webui/releases"));

            dynamic result = JsonConvert.DeserializeObject(response);

            foreach (dynamic obj in result)
            {
                if (stable)
                {
                    if (obj.prerelease != "False") continue;
                    foreach (dynamic file in obj.assets)
                    {
                        if ((string) file.name == "latest.zip")
                        {
                            return obj.tag_name;
                        }
                    }
                }
                else
                {
                    if (obj.prerelease != "True") continue;
                    foreach (dynamic file in obj.assets)
                    {
                        if ((string) file.name == "latest.zip")
                        {
                            return obj.tag_name;
                        }
                    }
                }
            }
            return null;
        }
    }
}