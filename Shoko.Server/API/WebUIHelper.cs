using System;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using SharpCompress.Common;
using SharpCompress.Readers;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.API;

public static class WebUIHelper
{
    /// <summary>
    /// Find the download url for the <paramref name="tagName"/>, then download
    /// and install the update.
    /// </summary>
    /// <param name="tagName">Tag name to download.</param>
    /// <param name="_channel">Deprecated.</param>
    /// <returns></returns>
    public static void GetUrlAndUpdate(string tagName, string _channel = null)
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
        var release = DownloadApiResponse($"/releases/tags/{tagName}");
        string url = null;
        foreach (var assets in release.assets)
        {
            // We don't care what the zip is named, only that it is attached.
            // This is because we changed the signature from "latest.zip" to
            // "Shoko-WebUI-{obj.tag_name}.zip" in the upgrade to web ui v2
            string fileName = assets.name;
            if (fileName == "latest.zip" || fileName == $"Shoko-WebUI-{release.tag_name}.zip")
            {
                url = assets.browser_download_url;
                break;
            }
        }

        // Check if we were able to get a release.
        if (string.IsNullOrWhiteSpace(url))
            throw new Exception("404 Not found");

        DownloadAndInstallUpdate(url);
    }

    /// <summary>
    /// Download and install update.
    /// </summary>
    /// <param name="url">direct link to version you want to install</param>
    /// <returns></returns>
    public static void DownloadAndInstallUpdate(string url)
    {
        var webuiDir = Path.Combine(ServerSettings.ApplicationPath, "webui");
        var backupDir = Path.Combine(webuiDir, "old");
        var zipFile = Path.Combine(webuiDir, "update.zip");
        var files = Directory.GetFiles(webuiDir);
        var directories = Directory.GetDirectories(webuiDir);

        // Make sure the base directory exists.
        if (!Directory.Exists(webuiDir))
            Directory.CreateDirectory(webuiDir);

        // Download the zip file.
        using (var client = new WebClient())
        {
            client.Headers.Add("User-Agent", $"ShokoServer/{Utils.GetApplicationVersion()}");
            client.DownloadFile(url, zipFile);
        }

        // Create the backup dictionary for later use.
        if (!Directory.Exists(backupDir))
            Directory.CreateDirectory(backupDir);

        // Move all directories and their files into the backup directory until the update is complete.
        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir) || dir == backupDir || dir == Path.Combine(webuiDir, "tweak"))
                continue;
            var newDir = dir.Replace(webuiDir, backupDir);
            Directory.Move(dir, newDir);
        }

        // Also move all the files directly in the base directory into the backup directory until the update is complete.
        foreach (var file in files)
        {
            if (file == zipFile || !File.Exists(file))
                continue;
            var newFile = file.Replace(webuiDir, backupDir);
            File.Move(file, newFile);
        }

        // Extract the zip contents into the folder.
        using (var stream = new FileStream(zipFile, FileMode.Open))
        using (var reader = ReaderFactory.Open(stream))
        {
            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    reader.WriteEntryToDirectory(webuiDir, new ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }
        }

        // Clean up the now unneeded backup and zip file because we have an updated install.
        Directory.Delete(backupDir, true);
        File.Delete(zipFile);
    }

    /// <summary>
    /// Find the latest version for the release channel.
    /// </summary>
    /// <param name="stable">do version have to be stable</param>
    /// <returns></returns>
    public static string WebUIGetLatestVersion(bool stable)
    {
        // The 'latest' release will always be a stable release, so we can skip
        // checking it if we're looking for a pre-release.
        if (!stable)
            return GetVersionTag(false);
        var release = DownloadApiResponse("/releases/latest");
        return release.tag_name;
    }

    /// <summary>
    /// Look through the release history to find the first matching version
    /// for the release channel.
    /// </summary>
    /// <param name="stable">do version have to be stable</param>
    /// <returns></returns>
    public static string GetVersionTag(bool stable)
    {
        var releases = DownloadApiResponse("/releases");
        foreach (var release in releases)
        {
            // Filter out pre-releases from the stable release channel, but don't
            // filter out stable releases from the dev channel.
            if (stable && release.prerelease != "False")
                continue;

            foreach (var asset in release.assets)
            {
                // We don't care what the zip is named, only that it is attached.
                // This is because we changed the signature from "latest.zip" to
                // "Shoko-WebUI-{obj.tag_name}.zip" in the upgrade to web ui v2
                string fileName = asset.name;
                if (fileName == "latest.zip" || fileName == $"Shoko-WebUI-{release.tag_name}.zip")
                    return release.tag_name;
            }
        }

        return null;
    }

    /// <summary>
    /// Download an api response from github.
    /// </summary>
    /// <param name="endpoint">Endpoint to probe for data.</param>
    /// <returns></returns>
    private static dynamic DownloadApiResponse(string endpoint)
    {
        var client = new WebClient();
        client.Headers.Add("Accept: application/vnd.github.v3+json");
        client.Headers.Add("User-Agent", $"ShokoServer/{Utils.GetApplicationVersion()}");
        var response = client.DownloadString(new Uri($"https://api.github.com/repos/shokoanime/shokoserver-webui{endpoint}"));
        dynamic result = JsonConvert.DeserializeObject(response);
        return result;
    }
}
