using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SharpCompress.Common;
using SharpCompress.Readers;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

#nullable enable
namespace Shoko.Server.Services;

public partial class WebUIUpdateService
{
    [GeneratedRegex(@"^[a-z0-9_\-\.]+/[a-z0-9_\-\.]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CompiledRepoNameRegex();

    [GeneratedRegex(@"^[Vv]?(?<version>(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+))(?:-dev.(?<buildNumber>\d+))?$", RegexOptions.Compiled, "en-US")]
    private static partial Regex ServerReleaseVersionRegex();

    private readonly ISettingsProvider _settingsProvider;

    private readonly IApplicationPaths _applicationPaths;

    private readonly HttpClient _httpClient;

    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions()
    {
        ExpirationScanFrequency = TimeSpan.FromMinutes(50),
    });

    private readonly TimeSpan _cacheTTL = TimeSpan.FromHours(1);

    public event EventHandler? UpdateInstalled;

    public readonly string ClientRepoName;

    public readonly string ServerRepoName;

    private const string MinimumServerVersionPrefix = "Minimum Server Version: **";

    private const string MinimumServerVersionSuffix = "**";

    public WebUIUpdateService(ISettingsProvider settingsProvider, IApplicationPaths applicationPaths)
    {
        _settingsProvider = settingsProvider;
        _applicationPaths = applicationPaths;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        ClientRepoName = Environment.GetEnvironmentVariable("SHOKO_CLIENT_REPO") is { } clientRepoName && CompiledRepoNameRegex().IsMatch(clientRepoName)
            ? clientRepoName
            : "ShokoAnime/Shoko-WebUI";
        ServerRepoName = Environment.GetEnvironmentVariable("SHOKO_SERVER_REPO") is { } serverRepoName && CompiledRepoNameRegex().IsMatch(serverRepoName)
            ? serverRepoName
            : "ShokoAnime/ShokoServer";
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"ShokoServer/{Utils.GetApplicationVersion()} (https://github.com/{ServerRepoName})");
        if (Environment.GetEnvironmentVariable("GITHUB_TOKEN") is { Length: > 0 } githubToken)
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {githubToken}");
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
    /// Find the download url for the found version for the given channel, then download
    /// and install the update.
    /// </summary>
    /// <param name="channel">Channel to download the update for.</param>
    /// <param name="allowIncompatible">Allow incompatible updates.</param>
    /// <exception cref="WebException">An error occurred while downloading the resource.</exception>
    /// <returns></returns>
    public void InstallUpdateForChannel(ReleaseChannel channel, bool allowIncompatible = false)
    {
        var version = GetLatestVersion(channel, allowIncompatible: allowIncompatible);
        var release = DownloadApiResponse($"releases/tags/{version.Tag}", ClientRepoName);
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

        DownloadAndInstallUpdate(url, version);
    }

    public void ReactToManualUpdate()
    {
        Task.Run(() => UpdateInstalled?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>
    /// Download and install update.
    /// </summary>
    /// <param name="url">direct link to version you want to install</param>
    /// <param name="version">Version to download.</param>
    /// <exception cref="WebException">An error occurred while downloading the resource.</exception>
    /// <returns></returns>
    private void DownloadAndInstallUpdate(string url, ComponentVersion version)
    {
        var webuiDir = _applicationPaths.WebPath;
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

        // Update cached version info.
        UpdateCachedVersionInfo(version);

        Task.Run(() => UpdateInstalled?.Invoke(this, EventArgs.Empty));
    }

    private void UpdateCachedVersionInfo(ComponentVersion version)
    {
        // Load the web ui version info from disk.
        var webUIFileInfo = new FileInfo(Path.Join(_applicationPaths.WebPath, "version.json"));
        if (!webUIFileInfo.Exists || JsonConvert.DeserializeObject<WebUIVersionInfo>(File.ReadAllText(webUIFileInfo.FullName)) is not { } webuiVersion)
            webuiVersion = new();

        var changed = false;
        if (webuiVersion.Version is not { Length: > 0 } || webuiVersion.Version != version.Version)
        {
            webuiVersion.Version = version.Version;
            changed = true;
        }
        if (version.MinimumServerVersion is null ? webuiVersion.MinimumServerVersion is not null : (webuiVersion.MinimumServerVersion is not { } || webuiVersion.MinimumServerVersion != version.MinimumServerVersion))
        {
            webuiVersion.MinimumServerVersion = version.MinimumServerVersion;
            changed = true;
        }
        if (webuiVersion.Tag is not { Length: > 0 } || webuiVersion.Tag != version.Tag)
        {
            webuiVersion.Tag = version.Tag;
            changed = true;
        }
        if (webuiVersion.Commit is not { Length: > 0 } || webuiVersion.Commit != version.Commit)
        {
            webuiVersion.Commit = version.Commit;
            changed = true;
        }
        if (webuiVersion.Date is null || webuiVersion.Date != version.ReleaseDate)
        {
            webuiVersion.Date = version.ReleaseDate;
            changed = true;
        }
        if (webuiVersion.Channel != version.ReleaseChannel)
        {
            webuiVersion.Channel = version.ReleaseChannel;
            changed = true;
        }
        if (webuiVersion.IsDebug.HasValue)
        {
            webuiVersion.IsDebug = false;
            changed = true;
        }
        if (changed)
            File.WriteAllText(webUIFileInfo.FullName, JsonConvert.SerializeObject(webuiVersion));
    }

    /// <summary>
    /// Check for latest version for the selected <paramref name="channel"/> and
    /// return a <see cref="ComponentVersion"/> containing the version
    /// information.
    /// </summary>
    /// <param name="channel">The release channel to use.</param>
    /// <param name="force">Bypass the cache and search for a new version online.</param>
    /// <param name="allowIncompatible">Allow incompatible updates.</param>
    /// <exception cref="WebException">An error occurred while downloading the resource.</exception>
    /// <returns></returns>
    public ComponentVersion GetLatestVersion(ReleaseChannel channel = ReleaseChannel.Auto, bool force = false, bool allowIncompatible = false)
    {
        if (channel == ReleaseChannel.Auto)
            channel = GetCurrentWebUIReleaseChannel();
        var key = $"webui:{channel}";
        if (!force && _cache.TryGetValue<ComponentVersion>(key, out var componentVersion))
            return componentVersion!;
        var isNotDev = channel is not ReleaseChannel.Dev;
        var currentServerVersion = Assembly.GetExecutingAssembly().GetName().Version!;
        switch (channel)
        {
            // Check for dev channel updates.
            case ReleaseChannel.Dev:
            {
                var releases = DownloadApiResponse("releases?per_page=100&page=1", ClientRepoName);
                foreach (var release in releases)
                {
                    string tagName = release.tag_name;
                    var version = tagName[0] == 'v' ? tagName[1..] : tagName;
                    if (isNotDev && version.Contains("-dev"))
                        continue;

                    string? description = release.body;
                    var minServerVersion = GetMinimumServerVersion(description);
                    _versionComparer ??= new();
                    if (!allowIncompatible && (minServerVersion is null || _versionComparer.Compare(minServerVersion, currentServerVersion) > 0))
                        continue;

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
                            return _cache.Set(key, new ComponentVersion
                            {
                                Version = version,
                                MinimumServerVersion = minServerVersion,
                                Commit = commit,
                                ReleaseChannel = channel,
                                ReleaseDate = releaseDate,
                                Tag = tagName,
                                Description = description?.Trim() ?? string.Empty,
                            }, _cacheTTL);
                        }
                    }
                }

                // Stop now if we got here from the default case.
                if (isNotDev)
                {
                    var webuiVersion = LoadWebUIVersionInfo(_applicationPaths);
                    return _cache.Set(key, new ComponentVersion
                    {
                        Version = webuiVersion?.Version ?? "1.0.0",
                        MinimumServerVersion = webuiVersion?.MinimumServerVersion,
                        Commit = webuiVersion?.Commit ?? "0000000000000000000000000000000000000000",
                        ReleaseChannel = channel,
                        ReleaseDate = webuiVersion?.Date ?? DateTime.MinValue,
                        Tag = webuiVersion?.Tag ?? "1.0.0",
                        Description = string.Empty,
                    }, _cacheTTL);
                }

                // Fallback to stable.
                goto default;
            }

            // Check for stable channel updates.
            default:
            {
                var latestRelease = DownloadApiResponse("releases/latest", ClientRepoName);
                string? description = latestRelease.body;
                var minServerVersion = GetMinimumServerVersion(description);
                _versionComparer ??= new();
                if (!allowIncompatible && (minServerVersion is null || _versionComparer.Compare(minServerVersion, currentServerVersion) > 0))
                    goto case ReleaseChannel.Dev;

                string tagName = latestRelease.tag_name;
                var version = tagName[0] == 'v' ? tagName[1..] : tagName;
                var tag = DownloadApiResponse($"git/ref/tags/{tagName}", ClientRepoName);
                string commit = tag["object"].sha;
                DateTime releaseDate = latestRelease.published_at;
                releaseDate = releaseDate.ToUniversalTime();
                return _cache.Set(key, new ComponentVersion
                {
                    Version = version,
                    MinimumServerVersion = minServerVersion,
                    Commit = commit,
                    ReleaseChannel = channel,
                    ReleaseDate = releaseDate,
                    Tag = tagName,
                    Description = description?.Trim() ?? string.Empty,
                }, _cacheTTL);
            }
        }
    }

    private Version? GetMinimumServerVersion(string? description)
    {
        if (string.IsNullOrEmpty(description))
            return null;

        if (description.IndexOf(MinimumServerVersionPrefix) is var index && index is -1)
            return null;

        var startIndex = index + MinimumServerVersionPrefix.Length;
        var endIndex = description.IndexOf(MinimumServerVersionSuffix, startIndex);
        if (endIndex is -1 || !Version.TryParse(description[startIndex..endIndex].Trim().TrimStart(['v', 'V']).Replace("-dev", ""), out var minVersion))
            return null;

        return minVersion;
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
    public ComponentVersion? GetLatestServerVersion(ReleaseChannel channel = ReleaseChannel.Auto, bool force = false)
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
                        _versionComparer ??= new();
                        if (_versionComparer.Compare(localVersion, version) > 0)
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
                string? description = latestRelease.body;
                return _cache.Set(key, new ComponentVersion
                {
                    Version = version,
                    Commit = commit,
                    ReleaseChannel = ReleaseChannel.Stable,
                    ReleaseDate = releaseDate,
                    Tag = tagName,
                    Description = description?.Trim() ?? string.Empty,
                }, _cacheTTL);
            }
        }
    }

    internal class SemverVersionComparer : IComparer<Version>
    {
        public int Compare(Version? x, Version? y)
        {
            if (x is null)
                return y is null ? 0 : -1;
            if (y is null)
                return 1;

            var value = x.Major.CompareTo(y.Major);
            if (value != 0)
                return value;

            value = x.Minor.CompareTo(y.Minor);
            if (value != 0)
                return value;

            if (x.Build != y.Build)
                return x.Build.CompareTo(y.Build);

            if (x.Revision is 0 && y.Revision is 0)
                return 0;

            if (x.Revision is 0 && y.Revision is not 0)
                return 1;

            if (x.Revision is not 0 && y.Revision is 0)
                return -1;

            return x.Revision.CompareTo(y.Revision);
        }
    }

    private SemverVersionComparer? _versionComparer;

    private ReleaseChannel GetCurrentWebUIReleaseChannel()
    {
        var webuiVersion = LoadWebUIVersionInfo(_applicationPaths);
        if (webuiVersion != null)
            return webuiVersion.Channel;
        return GetCurrentServerReleaseChannel();
    }

    private static ReleaseChannel GetCurrentServerReleaseChannel()
    {
        var extraVersionDict = Utils.GetApplicationExtraVersion();
        if (extraVersionDict.TryGetValue("channel", out var rawChannel) && Enum.TryParse<ReleaseChannel>(rawChannel, true, out var channel))
            return channel;
        return ReleaseChannel.Stable;
    }

    public static WebUIVersionInfo? LoadWebUIVersionInfo(IApplicationPaths? applicationPaths = null)
    {
        applicationPaths ??= ApplicationPaths.Instance;
        var webUIFileInfo = new FileInfo(Path.Join(applicationPaths.WebPath, "version.json"));
        if (webUIFileInfo.Exists)
            return JsonConvert.DeserializeObject<WebUIVersionInfo>(File.ReadAllText(webUIFileInfo.FullName));
        return null;
    }

    public static WebUIVersionInfo? LoadIncludedWebUIVersionInfo(IApplicationPaths? applicationPaths = null)
    {
        applicationPaths ??= ApplicationPaths.Instance;
        var webUIFileInfo = new FileInfo(Path.Join(applicationPaths.ApplicationPath, "webui/version.json"));
        if (webUIFileInfo.Exists)
            return JsonConvert.DeserializeObject<WebUIVersionInfo>(File.ReadAllText(webUIFileInfo.FullName));
        return null;
    }

    /// <summary>
    /// Web UI Version Info.
    /// </summary>
    public class WebUIVersionInfo
    {
        /// <summary>
        /// Package version.
        /// </summary>
        [JsonProperty("package")]
        public string Version { get; set; } = "1.0.0";

        [JsonIgnore]
        public Version VersionAsVersion => new(Version.Replace("-dev", ""));

        /// <summary>
        /// Minimum Shoko Server version compatible with the Web UI.
        /// </summary>
        [JsonProperty("minimumServerVersion", NullValueHandling = NullValueHandling.Ignore)]
        public string? MinimumServerVersionAsString { get; set; }

        /// <summary>
        /// Minimum Shoko Server version compatible with the Web UI.
        /// </summary>
        [JsonIgnore]
        public Version? MinimumServerVersion
        {
            get => MinimumServerVersionAsString is { Length: > 0 }
                ? new(MinimumServerVersionAsString.Replace("-dev", ""))
                : null;
            set
            {
                if (value is null)
                    MinimumServerVersionAsString = null;
                else if (value is not { Revision: > 0 })
                    MinimumServerVersionAsString = $"{value.Major}.{value.Minor}.{value.Build}";
                else
                    MinimumServerVersionAsString = $"{value.Major}.{value.Minor}.{value.Build}-dev.{value.Revision}";
            }
        }

        /// <summary>
        /// Git tag.
        /// </summary>
        [JsonProperty("tag")]
        public string Tag { get; set; } = "v1.0.0";

        /// <summary>
        /// Long-form git commit sha digest.
        /// </summary>
        [JsonProperty("git")]
        public string Commit { get; set; } = "0000000";

        /// <summary>
        /// Release date for web ui release.
        /// </summary>
        [JsonProperty("date", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Date { get; set; } = null;

        [JsonIgnore]
        private ReleaseChannel? _channel = null;

        /// <summary>
        /// Cached release channel.
        /// </summary>
        [JsonProperty("channel")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ReleaseChannel Channel
        {
            get => _channel ??= IsDebug.HasValue && IsDebug.Value ? ReleaseChannel.Debug : Version.Contains("-dev") ? ReleaseChannel.Dev : ReleaseChannel.Stable;
            set => _channel = value;
        }

        /// <summary>
        /// True if this is a debug package.
        /// </summary>
        [JsonProperty("debug", NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? IsDebug { get; set; }
    }

    public class ComponentVersion
    {
        /// <summary>
        /// Version number.
        /// </summary>
        public required string Version { get; set; }

        /// <summary>
        /// Minimum Shoko Server version compatible with the Web UI.
        /// </summary>
        public Version? MinimumServerVersion { get; set; }

        /// <summary>
        /// Commit SHA.
        /// </summary>
        public required string Commit { get; set; }

        /// <summary>
        /// Release channel.
        /// </summary>
        public required ReleaseChannel ReleaseChannel { get; set; }

        /// <summary>
        /// Release date.
        /// </summary>
        public required DateTime ReleaseDate { get; set; }

        /// <summary>
        /// Git Tag.
        /// </summary>
        public required string Tag { get; set; }

        /// <summary>
        /// A short description about this release/version.
        /// </summary>
        public required string? Description { get; set; }
    }
}
