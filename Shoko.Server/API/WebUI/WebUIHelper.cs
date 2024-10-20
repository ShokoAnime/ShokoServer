using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using SharpCompress.Common;
using SharpCompress.Readers;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.API.WebUI;

public static partial class WebUIHelper
{
    [GeneratedRegex(@"^[a-z0-9_\-\.]+/[a-z0-9_\-\.]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CompiledRepoNameRegex();

    private static string? _clientRepoName = null;

    public static string ClientRepoName =>
        _clientRepoName ??= Environment.GetEnvironmentVariable("SHOKO_CLIENT_REPO") is { } envVar && CompiledRepoNameRegex().IsMatch(envVar) ? envVar : "ShokoAnime/Shoko-WebUI";

    private static string? _serverRepoName = null;

    public static string ServerRepoName =>
        _serverRepoName ??= Environment.GetEnvironmentVariable("SHOKO_SERVER_REPO") is { } envVar && CompiledRepoNameRegex().IsMatch(envVar) ? envVar : "ShokoAnime/ShokoServer";

    /// <summary>
    /// Web UI Version Info.
    /// </summary>
    public record WebUIVersionInfo
    {
        /// <summary>
        /// Package version.
        /// </summary>
        [JsonProperty("package")]
        public string Package { get; set; } = "1.0.0";
        /// <summary>
        /// Short-form git commit sha digest.
        /// </summary>
        [JsonProperty("git")]
        public string Git { get; set; } = "0000000";
        /// <summary>
        /// True if this is a debug package.
        /// </summary>
        [JsonProperty("debug")]
        public bool Debug { get; set; } = false;
        /// <summary>
        /// Release date for web ui release.
        /// </summary>
        [JsonProperty("date")]
        public DateTime? Date { get; set; } = null;
    }

    /// <summary>
    /// Find the download url for the <paramref name="tagName"/>, then download
    /// and install the update.
    /// </summary>
    /// <param name="tagName">Tag name to download.</param>
    /// <exception cref="WebException">An error occurred while downloading the resource.</exception>
    /// <returns></returns>
    public static void GetUrlAndUpdate(string tagName)
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
        var release = DownloadApiResponse($"releases/tags/{tagName}");
        if (release is null)
            return;

        string? url = null;
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

        DateTime releaseDate = release.published_at;
        DownloadAndInstallUpdate(url, releaseDate);
    }

    /// <summary>
    /// Download and install update.
    /// </summary>
    /// <param name="url">direct link to version you want to install</param>
    /// <param name="releaseDate">the release date from the api response</param>
    /// <exception cref="WebException">An error occurred while downloading the resource.</exception>
    /// <returns></returns>
    private static void DownloadAndInstallUpdate(string url, DateTime releaseDate)
    {
        var webuiDir = Path.Combine(Utils.ApplicationPath, "webui");
        var backupDir = Path.Combine(webuiDir, "old");
        var files = Directory.GetFiles(webuiDir);
        var directories = Directory.GetDirectories(webuiDir);

        // Make sure the base directory exists.
        if (!Directory.Exists(webuiDir))
            Directory.CreateDirectory(webuiDir);

        // Download the zip file.
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", $"ShokoServer/{Utils.GetApplicationVersion()}");
        var zipContent = client.GetByteArrayAsync(url).ConfigureAwait(false).GetAwaiter().GetResult();

        // Remove any old lingering backups.
        if (Directory.Exists(backupDir))
            Directory.Delete(backupDir, true);

        // Create the backup dictionary for later use.
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
            var newFile = file.Replace(webuiDir, backupDir);
            File.Move(file, newFile);
        }

        // Extract the zip contents into the folder.
        using var stream = new MemoryStream(zipContent);
        using var reader = ReaderFactory.Open(stream);
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

        // Clean up the now unneeded backup and zip file because we have an updated install.
        Directory.Delete(backupDir, true);

        // Add release date to json
        AddReleaseDate(releaseDate);
    }

    private static void AddReleaseDate(DateTime releaseDate)
    {
        var webUIFileInfo = new FileInfo(Path.Combine(Utils.ApplicationPath, "webui/version.json"));
        if (webUIFileInfo.Exists)
        {
            // Load the web ui version info from disk.
            var webuiVersion = JsonConvert.DeserializeObject<WebUIVersionInfo>(System.IO.File.ReadAllText(webUIFileInfo.FullName));
            // Set the release data and save the info again if the date is not set.
            if (webuiVersion is not null && !webuiVersion.Date.HasValue)
            {
                webuiVersion.Date = releaseDate;
                File.WriteAllText(webUIFileInfo.FullName, JsonConvert.SerializeObject(webuiVersion));
            }
        }
    }

    public static WebUIVersionInfo? LoadWebUIVersionInfo()
    {
        var webUIFileInfo = new FileInfo(Path.Combine(Utils.ApplicationPath, "webui/version.json"));
        if (webUIFileInfo.Exists)
            return JsonConvert.DeserializeObject<WebUIVersionInfo>(File.ReadAllText(webUIFileInfo.FullName));
        return null;
    }

    /// <summary>
    /// Find the latest version for the release channel.
    /// </summary>
    /// <param name="stable">do version have to be stable</param>
    /// <exception cref="WebException">An error occurred while downloading the resource.</exception>
    /// <returns></returns>
    public static string? WebUIGetLatestVersion(bool stable)
    {
        // The 'latest' release will always be a stable release, so we can skip
        // checking it if we're looking for a pre-release.
        if (!stable)
            return GetVersionTag(false);
        var release = DownloadApiResponse("releases/latest");
        return release?.tag_name;
    }

    /// <summary>
    /// Look through the release history to find the first matching version
    /// for the release channel.
    /// </summary>
    /// <param name="stable">do version have to be stable</param>
    /// <exception cref="WebException">An error occurred while downloading the resource.</exception>
    /// <returns></returns>
    private static string? GetVersionTag(bool stable)
    {
        var releases = DownloadApiResponse("releases");
        if (releases is null)
            return null;

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
    /// <param name="repoName">Repository name.</param>
    /// <returns></returns>
    /// <exception cref="WebException">An error occurred while downloading the resource.</exception>
    internal static dynamic? DownloadApiResponse(string endpoint, string? repoName = null)
    {
        repoName ??= ClientRepoName;
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        client.DefaultRequestHeaders.Add("User-Agent", $"ShokoServer/{Utils.GetApplicationVersion()}");
        var response = client.GetStringAsync(new Uri($"https://api.github.com/repos/{repoName}/{endpoint}"))
            .ConfigureAwait(false).GetAwaiter().GetResult();
        return JsonConvert.DeserializeObject(response);
    }
}
