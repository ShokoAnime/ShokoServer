using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using SharpCompress.Common;
using SharpCompress.Readers;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Services;

public partial class WebUIUpdateService
{
    [GeneratedRegex(@"^[a-z0-9_\-\.]+/[a-z0-9_\-\.]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CompiledRepoNameRegex();

    [GeneratedRegex(@"^[Vv]?(?<version>(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+))(?:-dev.(?<buildNumber>\d+))?$", RegexOptions.Compiled, "en-US")]
    private static partial Regex ServerReleaseVersionRegex();

    private readonly HttpClient _httpClient;

    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions()
    {
        ExpirationScanFrequency = TimeSpan.FromMinutes(50),
    });

    private readonly TimeSpan _cacheTTL = TimeSpan.FromHours(1);

    public readonly string ClientRepoName;

    public readonly string ServerRepoName;


    public WebUIUpdateService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"ShokoServer/{Utils.GetApplicationVersion()}");
        ClientRepoName = Environment.GetEnvironmentVariable("SHOKO_CLIENT_REPO") is { } clientRepoName && CompiledRepoNameRegex().IsMatch(clientRepoName)
            ? clientRepoName
            : "ShokoAnime/Shoko-WebUI";
        ServerRepoName = Environment.GetEnvironmentVariable("SHOKO_SERVER_REPO") is { } serverRepoName && CompiledRepoNameRegex().IsMatch(serverRepoName)
            ? serverRepoName
            : "ShokoAnime/ShokoServer";
    }

    /// <summary>
    /// Download an api response from github.
    /// </summary>
    /// <param name="endpoint">Endpoint to probe for data.</param>
    /// <param name="repoName">Repository name.</param>
    /// <returns></returns>
    /// <exception cref="WebException">An error occurred while downloading the resource.</exception>
    private dynamic DownloadApiResponse(string endpoint, string repoName)
    {
        repoName ??= ClientRepoName;
        var response = _httpClient.GetStringAsync(new Uri($"https://api.github.com/repos/{repoName}/{endpoint}"))
            .ConfigureAwait(false).GetAwaiter().GetResult();
        return JsonConvert.DeserializeObject(response)!;
    }

    /// <summary>
    /// Find the download url for the <paramref name="tagName"/>, then download
    /// and install the update.
    /// </summary>
    /// <param name="tagName">Tag name to download.</param>
    /// <exception cref="WebException">An error occurred while downloading the resource.</exception>
    /// <returns></returns>
    public void GetUrlAndUpdate(string tagName)
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
        var release = DownloadApiResponse($"releases/tags/{tagName}", ClientRepoName);
        if (release is null)
            return;

        string? url = null;
        foreach (var assets in release.assets)
        {
            // We don't care what the zip is named, only that it is attached.
            string fileName = assets.name;
            if (Path.GetExtension(fileName) is ".zip")
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
    private void DownloadAndInstallUpdate(string url, DateTime releaseDate)
    {
        var webuiDir = Path.Combine(Utils.ApplicationPath, "webui");
        var backupDir = Path.Combine(webuiDir, "old");
        var files = Directory.GetFiles(webuiDir);
        var directories = Directory.GetDirectories(webuiDir);

        // Make sure the base directory exists.
        if (!Directory.Exists(webuiDir))
            Directory.CreateDirectory(webuiDir);

        // Download the zip file.
        var zipContent = _httpClient.GetByteArrayAsync(url).ConfigureAwait(false).GetAwaiter().GetResult();

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

    /// <summary>
    /// Find the latest version for the release channel.
    /// </summary>
    /// <param name="stable">do version have to be stable</param>
    /// <exception cref="WebException">An error occurred while downloading the resource.</exception>
    /// <returns></returns>
    public string? WebUIGetLatestVersion(bool stable)
    {
        // The 'latest' release will always be a stable release, so we can skip
        // checking it if we're looking for a pre-release.
        if (!stable)
            return GetVersionTag(false);
        var release = DownloadApiResponse("releases/latest", ClientRepoName);
        return release?.tag_name;
    }

    /// <summary>
    /// Look through the release history to find the first matching version
    /// for the release channel.
    /// </summary>
    /// <param name="stable">do version have to be stable</param>
    /// <exception cref="WebException">An error occurred while downloading the resource.</exception>
    /// <returns></returns>
    private string? GetVersionTag(bool stable)
    {
        var releases = DownloadApiResponse("releases", ClientRepoName);
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
    /// Check for latest version for the selected <paramref name="channel"/> and
    /// return a <see cref="ComponentVersion"/> containing the version
    /// information.
    /// </summary>
    /// <param name="channel">The release channel to use.</param>
    /// <param name="force">Bypass the cache and search for a new version online.</param>
    /// <exception cref="WebException">An error occurred while downloading the resource.</exception>
    /// <returns></returns>
    public ComponentVersion LatestWebUIVersion([FromQuery] ReleaseChannel channel = ReleaseChannel.Auto, [FromQuery] bool force = false)
    {
        if (channel == ReleaseChannel.Auto)
            channel = GetCurrentWebUIReleaseChannel();
        var key = $"webui:{channel}";
        if (!force && _cache.TryGetValue<ComponentVersion>(key, out var componentVersion))
            return componentVersion!;
        switch (channel)
        {
            // Check for dev channel updates.
            case ReleaseChannel.Dev:
            {
                var releases = DownloadApiResponse("releases?per_page=10&page=1", ClientRepoName);
                foreach (var release in releases)
                {
                    string tagName = release.tag_name;
                    var version = tagName[0] == 'v' ? tagName[1..] : tagName;
                    foreach (var asset in release.assets)
                    {
                        // We don't care what the zip is named, only that it is attached.
                        string fileName = asset.name;
                        if (Path.GetExtension(fileName) is ".zip")
                        {
                            var tag = DownloadApiResponse($"git/ref/tags/{tagName}", ClientRepoName);
                            string commit = tag["object"].sha;
                            DateTime releaseDate = release.published_at;
                            releaseDate = releaseDate.ToUniversalTime();
                            string description = release.body;
                            return _cache.Set(key, new ComponentVersion
                            {
                                Version = version,
                                Commit = commit[..7],
                                ReleaseChannel = ReleaseChannel.Dev,
                                ReleaseDate = releaseDate,
                                Tag = tagName,
                                Description = description.Trim(),
                            }, _cacheTTL);
                        }
                    }
                }

                // Fallback to stable.
                goto default;
            }

            // Check for stable channel updates.
            default:
            {
                var latestRelease = DownloadApiResponse("releases/latest", ClientRepoName);
                string tagName = latestRelease.tag_name;
                var version = tagName[0] == 'v' ? tagName[1..] : tagName;
                var tag = DownloadApiResponse($"git/ref/tags/{tagName}", ClientRepoName);
                string commit = tag["object"].sha;
                DateTime releaseDate = latestRelease.published_at;
                releaseDate = releaseDate.ToUniversalTime();
                string description = latestRelease.body;
                return _cache.Set(key, new ComponentVersion
                {
                    Version = version,
                    Commit = commit[0..7],
                    ReleaseChannel = ReleaseChannel.Stable,
                    ReleaseDate = releaseDate,
                    Tag = tagName,
                    Description = description.Trim(),
                }, _cacheTTL);
            }
        }
    }

    /// <summary>
    /// Check for latest version for the selected <paramref name="channel"/> and
    /// return a <see cref="ComponentVersion"/> containing the version
    /// information.
    /// </summary>
    /// <param name="channel">The release channel to use.</param>
    /// <param name="force">Bypass the cache and search for a new version online.</param>
    /// <exception cref="WebException">An error occurred while downloading the resource.</exception>
    /// <returns></returns>
    public ComponentVersion? LatestServerWebUIVersion([FromQuery] ReleaseChannel channel = ReleaseChannel.Auto, [FromQuery] bool force = false)
    {
        if (channel == ReleaseChannel.Auto)
            channel = GetCurrentServerReleaseChannel();
        var key = $"server:{channel}";
        if (!force && _cache.TryGetValue<ComponentVersion>(key, out var componentVersion))
            return componentVersion!;
        switch (channel)
        {
            // Check for dev channel updates.
            case ReleaseChannel.Dev:
            {
                var latestTags = DownloadApiResponse($"tags?per_page=100&page=1", ServerRepoName);
                Version version = new("0.0.0.0");
                var tagName = string.Empty;
                var commitSha = string.Empty;
                var regex = ServerReleaseVersionRegex();
                foreach (var tagInfo in latestTags)
                {
                    string localTagName = tagInfo.name;
                    if (regex.Match(localTagName) is { Success: true } regexResult)
                    {
                        Version localVersion;
                        if (regexResult.Groups["buildNumber"].Success)
                            localVersion = new Version(regexResult.Groups["version"].Value + "." + regexResult.Groups["buildNumber"].Value);
                        else
                            localVersion = new Version(regexResult.Groups["version"].Value + ".0");
                        if (localVersion > version)
                        {
                            version = localVersion;
                            tagName = localTagName;
                            commitSha = tagInfo.commit.sha;
                        }
                    }
                }

                if (string.IsNullOrEmpty(commitSha))
                {
                    return null;
                }

                var latestCommit = DownloadApiResponse($"commits/{commitSha}", ServerRepoName);
                DateTime releaseDate = latestCommit.commit.author.date;
                releaseDate = releaseDate.ToUniversalTime();
                string description;
                // We're on a local build.
                if (!Utils.GetApplicationExtraVersion().TryGetValue("commit", out var currentCommit))
                {
                    description = "Local build detected. Unable to determine the relativeness of the latest daily release.";
                }
                // We're not on the latest daily release.
                else if (!string.Equals(currentCommit, commitSha))
                {
                    var diff = DownloadApiResponse($"compare/{commitSha}...{currentCommit}", ServerRepoName);
                    var aheadBy = (int)diff.ahead_by;
                    var behindBy = (int)diff.behind_by;
                    description = $"You are currently {aheadBy} commits ahead and {behindBy} commits behind the latest daily release.";
                }
                // We're on the latest daily release.
                else
                {
                    description = "All caught up! You are running the latest daily release.";
                }
                return _cache.Set(key, new ComponentVersion
                {
                    Version = version.ToString(),
                    Commit = commitSha,
                    ReleaseChannel = ReleaseChannel.Dev,
                    ReleaseDate = releaseDate,
                    Tag = tagName,
                    Description = description,
                }, _cacheTTL);
            }

#if DEBUG
            // Spoof update if debugging and requesting the latest debug version.
            case ReleaseChannel.Debug:
            {
                componentVersion = new ComponentVersion() { Version = Utils.GetApplicationVersion(), Description = "Local debug version." };
                var extraVersionDict = Utils.GetApplicationExtraVersion();
                if (extraVersionDict.TryGetValue("tag", out var tag))
                    componentVersion.Tag = tag;
                if (extraVersionDict.TryGetValue("commit", out var commit))
                    componentVersion.Commit = commit;
                if (extraVersionDict.TryGetValue("channel", out var rawChannel))
                    if (Enum.TryParse<ReleaseChannel>(rawChannel, true, out var parsedChannel))
                        componentVersion.ReleaseChannel = parsedChannel;
                    else
                        componentVersion.ReleaseChannel = ReleaseChannel.Debug;
                if (extraVersionDict.TryGetValue("date", out var dateText) && DateTime.TryParse(dateText, out var releaseDate))
                    componentVersion.ReleaseDate = releaseDate.ToUniversalTime();
                return _cache.Set<ComponentVersion>(key, componentVersion, _cacheTTL);
            }
#endif

            // Check for stable channel updates.
            default:
            {
                var latestRelease = DownloadApiResponse("releases/latest", ServerRepoName);
                string tagName = latestRelease.tag_name;
                var tagResponse = DownloadApiResponse($"git/ref/tags/{tagName}", ServerRepoName);
                var version = tagName[1..] + ".0";
                string commit = tagResponse["object"].sha;
                DateTime releaseDate = latestRelease.published_at;
                releaseDate = releaseDate.ToUniversalTime();
                string description = latestRelease.body;
                return _cache.Set(key, new ComponentVersion
                {
                    Version = version,
                    Commit = commit,
                    ReleaseChannel = ReleaseChannel.Stable,
                    ReleaseDate = releaseDate,
                    Tag = tagName,
                    Description = description.Trim(),
                }, _cacheTTL);
            }
        }
    }

    public ReleaseChannel GetCurrentWebUIReleaseChannel()
    {
        var webuiVersion = LoadWebUIVersionInfo();
        if (webuiVersion != null)
            return webuiVersion.Debug ? ReleaseChannel.Debug : webuiVersion.Package.Contains("-dev") ? ReleaseChannel.Dev : ReleaseChannel.Stable;
        return GetCurrentServerReleaseChannel();
    }

    private static ReleaseChannel GetCurrentServerReleaseChannel()
    {
        var extraVersionDict = Utils.GetApplicationExtraVersion();
        if (extraVersionDict.TryGetValue("channel", out var rawChannel) && Enum.TryParse<ReleaseChannel>(rawChannel, true, out var channel))
            return channel;
        return ReleaseChannel.Stable;
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

    public class ComponentVersion
    {
        /// <summary>
        /// Version number.
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// Commit SHA.
        /// </summary>
        public string? Commit { get; set; }

        /// <summary>
        /// Release channel.
        /// </summary>
        public ReleaseChannel? ReleaseChannel { get; set; }

        /// <summary>
        /// Release date.
        /// </summary>
        public DateTime? ReleaseDate { get; set; }

        /// <summary>
        /// Git Tag.
        /// </summary>
        public string? Tag { get; set; }

        /// <summary>
        /// A short description about this release/version.
        /// </summary>
        public string? Description { get; set; }
    }
}
