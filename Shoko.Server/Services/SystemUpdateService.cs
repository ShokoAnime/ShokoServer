using System;
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
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Plugin;

using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

#nullable enable
namespace Shoko.Server.Services;

public partial class SystemUpdateService : ISystemUpdateService
{
    private const string MinimumServerVersionPrefix = "Minimum Server Version: **";

    private const string MinimumServerVersionSuffix = "**";

    [GeneratedRegex(@"^[a-z0-9_\-\.]+/[a-z0-9_\-\.]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CompiledRepoNameRegex();

    [GeneratedRegex(@"^[Vv]?(?<version>(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+))(?:-dev.(?<buildNumber>\d+))?$", RegexOptions.Compiled, "en-US")]
    private static partial Regex ServerReleaseVersionRegex();

    private readonly ISettingsProvider _settingsProvider;

    private readonly ISystemService _systemService;

    private readonly IHttpClientFactory _httpClientFactory;

    private readonly IApplicationPaths _applicationPaths;

    private SemverVersionComparer? _versionComparer;

    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions()
    {
        ExpirationScanFrequency = TimeSpan.FromMinutes(50),
    });

    private readonly TimeSpan _cacheTTL = TimeSpan.FromHours(1);

    public SystemUpdateService(ISettingsProvider settingsProvider, ISystemService systemService, IHttpClientFactory httpClientFactory, IApplicationPaths applicationPaths)
    {
        _settingsProvider = settingsProvider;
        _systemService = systemService;
        _httpClientFactory = httpClientFactory;
        _applicationPaths = applicationPaths;
    }

    #region Helpers

    /// <summary>
    /// Download an api response from github.
    /// </summary>
    /// <param name="endpoint">Endpoint to probe for data.</param>
    /// <param name="repoName">Repository name.</param>
    /// <returns></returns>
    /// <exception cref="WebException">An error occurred while downloading the resource.</exception>
    private async Task<dynamic> DownloadApiResponse(string endpoint, string repoName)
    {
        repoName ??= ClientRepositoryName;
        var client = _httpClientFactory.CreateClient("GitHub");
        var response = await client.GetStringAsync(new Uri($"https://api.github.com/repos/{repoName}/{endpoint}")).ConfigureAwait(false);
        return JsonConvert.DeserializeObject(response)!;
    }

    #endregion

    #region Web Component

    /// <inheritdoc />
    public event EventHandler? WebComponentUpdated;

    /// <inheritdoc />
    public string ClientRepositoryName
    {
        get => _settingsProvider.GetSettings().Web.ClientRepoName;
        set
        {
            // If validation fails then catch and swallow the exception.
            try
            {
                var copy = _settingsProvider.GetSettings(true);
                copy.Web.ClientRepoName = value;
                _settingsProvider.SaveSettings(copy);
            }
            catch { }
        }
    }

    /// <inheritdoc />
    public WebReleaseVersionInformation? LoadWebComponentVersionInformation()
    {
        if (LoadWebUIVersionInfo(_applicationPaths) is not { } webVer)
            return null;
        return new()
        {
            Version = webVer.VersionAsVersion,
            Channel = webVer.Channel,
            Description = null,
            ReleasedAt = webVer.Date ?? DateTime.MinValue,
            ReleaseTag = webVer.Tag,
            SourceRevision = webVer.Commit,
        };
    }

    /// <inheritdoc />
    public WebReleaseVersionInformation? LoadIncludedWebComponentVersionInformation()
    {
        if (LoadIncludedWebUIVersionInfo(_applicationPaths) is not { } webVer)
            return null;
        return new()
        {
            Version = webVer.VersionAsVersion,
            Channel = webVer.Channel,
            Description = null,
            ReleasedAt = webVer.Date ?? DateTime.MinValue,
            ReleaseTag = webVer.Tag,
            SourceRevision = webVer.Commit,
        };
    }

    /// <inheritdoc />
    public async Task<bool> UpdateWebComponent(ReleaseChannel channel = ReleaseChannel.Auto, bool allowIncompatible = false)
    {
        var version = await GetLatestWebComponentVersion(channel, allowIncompatible: allowIncompatible).ConfigureAwait(false);
        var release = await DownloadApiResponse($"releases/tags/{version.ReleaseTag}", ClientRepositoryName).ConfigureAwait(false);
        if (release is null)
            return false;

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

        await DownloadAndInstallUpdate(url, version);

        return true;
    }

    /// <inheritdoc />
    public void ReactToManualWebComponentUpdate()
    {
        Task.Run(() => WebComponentUpdated?.Invoke(this, EventArgs.Empty));
    }

    /// <inheritdoc />
    public async Task<WebReleaseVersionInformation> GetLatestWebComponentVersion(ReleaseChannel channel = ReleaseChannel.Auto, bool force = false, bool allowIncompatible = false)
    {
        if (channel == ReleaseChannel.Auto)
            channel = GetCurrentWebUIReleaseChannel();
        var key = $"webui:{channel}";
        if (!force && _cache.TryGetValue<WebReleaseVersionInformation>(key, out var componentVersion))
            return componentVersion!;
        var isNotDev = channel is not ReleaseChannel.Dev;
        var currentServerVersion = Assembly.GetExecutingAssembly().GetName().Version!;
        switch (channel)
        {
            // Check for dev channel updates.
            case ReleaseChannel.Dev:
            {
                var releases = await DownloadApiResponse("releases?per_page=100&page=1", ClientRepositoryName).ConfigureAwait(false);
                foreach (var release in releases)
                {
                    string tagName = release.tag_name;
                    var plainTagName = tagName[0] == 'v' || tagName[0] == 'V' ? tagName[1..] : tagName;
                    if (isNotDev && plainTagName.Contains("-dev"))
                        continue;
                    if (!Version.TryParse(plainTagName.Replace("-dev", ""), out var version))
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
                            var tag = await DownloadApiResponse($"git/ref/tags/{tagName}", ClientRepositoryName).ConfigureAwait(false);
                            string commit = tag["object"].sha;
                            DateTime releaseDate = release.published_at;
                            releaseDate = releaseDate.ToUniversalTime();
                            return _cache.Set(key, new WebReleaseVersionInformation
                            {
                                Version = version,
                                MinimumServerVersion = minServerVersion,
                                SourceRevision = commit,
                                Channel = channel,
                                ReleasedAt = releaseDate,
                                ReleaseTag = tagName,
                                Description = description?.Trim() ?? string.Empty,
                            }, _cacheTTL);
                        }
                    }
                }

                // Stop now if we got here from the default case.
                if (isNotDev)
                {
                    var webuiVersion = LoadWebUIVersionInfo();
                    return _cache.Set(key, new WebReleaseVersionInformation
                    {
                        Version = webuiVersion?.VersionAsVersion ?? new(1, 0, 0),
                        MinimumServerVersion = webuiVersion?.MinimumServerVersion,
                        SourceRevision = webuiVersion?.Commit ?? "0000000000000000000000000000000000000000",
                        Channel = channel,
                        ReleasedAt = webuiVersion?.Date ?? DateTime.MinValue,
                        ReleaseTag = webuiVersion?.Tag ?? "1.0.0",
                        Description = string.Empty,
                    }, _cacheTTL);
                }

                // Fallback to stable.
                goto default;
            }

            // Check for stable channel updates.
            default:
            {
                var latestRelease = await DownloadApiResponse("releases/latest", ClientRepositoryName).ConfigureAwait(false);
                string? description = latestRelease.body;
                var minServerVersion = GetMinimumServerVersion(description);
                _versionComparer ??= new();
                if (!allowIncompatible && (minServerVersion is null || _versionComparer.Compare(minServerVersion, currentServerVersion) > 0))
                    goto case ReleaseChannel.Dev;

                string tagName = latestRelease.tag_name;
                var plainTagName = tagName[0] == 'v' || tagName[0] == 'V' ? tagName[1..] : tagName;
                var version = Version.Parse(plainTagName.Replace("-dev", ""));
                var tag = await DownloadApiResponse($"git/ref/tags/{tagName}", ClientRepositoryName).ConfigureAwait(false);
                string commit = tag["object"].sha;
                DateTime releaseDate = latestRelease.published_at;
                releaseDate = releaseDate.ToUniversalTime();
                return _cache.Set(key, new WebReleaseVersionInformation
                {
                    Version = version,
                    MinimumServerVersion = minServerVersion,
                    SourceRevision = commit,
                    Channel = channel,
                    ReleasedAt = releaseDate,
                    ReleaseTag = tagName,
                    Description = description?.Trim() ?? string.Empty,
                }, _cacheTTL);
            }
        }
    }

    #region Web Component | Internals

    /// <summary>
    /// Download and install update.
    /// </summary>
    /// <param name="url">direct link to version you want to install</param>
    /// <param name="version">Version to download.</param>
    /// <exception cref="WebException">An error occurred while downloading the resource.</exception>
    /// <returns></returns>
    private async Task DownloadAndInstallUpdate(string url, WebReleaseVersionInformation version)
    {
        var webuiDir = _applicationPaths.WebPath;
        var backupDir = Path.Combine(webuiDir, "old");
        var files = Directory.GetFiles(webuiDir);
        var directories = Directory.GetDirectories(webuiDir);

        // Make sure the base directory exists.
        if (!Directory.Exists(webuiDir))
            Directory.CreateDirectory(webuiDir);

        // Download the zip file.
        var client = _httpClientFactory.CreateClient("GitHub");
        var zipContent = await client.GetByteArrayAsync(url).ConfigureAwait(false);

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

        _ = Task.Run(() => WebComponentUpdated?.Invoke(this, EventArgs.Empty));
    }

    private void UpdateCachedVersionInfo(WebReleaseVersionInformation version)
    {
        // Load the web ui version info from disk.
        var webUIFileInfo = new FileInfo(Path.Join(_applicationPaths.WebPath, "version.json"));
        if (!webUIFileInfo.Exists || JsonConvert.DeserializeObject<WebUIVersionInfo>(File.ReadAllText(webUIFileInfo.FullName)) is not { } webuiVersion)
            webuiVersion = new();

        var changed = false;
        if (webuiVersion.Version is not { Length: > 0 } || webuiVersion.Version != version.Version.ToSemanticVersioningString())
        {
            webuiVersion.Version = version.Version.ToSemanticVersioningString();
            changed = true;
        }
        if (version.MinimumServerVersion is null ? webuiVersion.MinimumServerVersion is not null : (webuiVersion.MinimumServerVersion is not { } || webuiVersion.MinimumServerVersion != version.MinimumServerVersion))
        {
            webuiVersion.MinimumServerVersion = version.MinimumServerVersion;
            changed = true;
        }
        if (webuiVersion.Tag is not { Length: > 0 } || webuiVersion.Tag != version.ReleaseTag)
        {
            webuiVersion.Tag = version.ReleaseTag;
            changed = true;
        }
        if (webuiVersion.Commit is not { Length: > 0 } || webuiVersion.Commit != version.SourceRevision)
        {
            webuiVersion.Commit = version.SourceRevision;
            changed = true;
        }
        if (webuiVersion.Date is null || webuiVersion.Date != version.ReleasedAt)
        {
            webuiVersion.Date = version.ReleasedAt;
            changed = true;
        }
        if (webuiVersion.Channel != version.Channel)
        {
            webuiVersion.Channel = version.Channel;
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

    private ReleaseChannel GetCurrentWebUIReleaseChannel()
    {
        var webuiVersion = LoadWebUIVersionInfo();
        if (webuiVersion != null)
            return webuiVersion.Channel;
        return GetCurrentServerReleaseChannel();
    }

    private static WebUIVersionInfo? LoadWebUIVersionInfo(IApplicationPaths? applicationPaths = null)
    {
        applicationPaths ??= ApplicationPaths.Instance;
        var webUIFileInfo = new FileInfo(Path.Join(applicationPaths.WebPath, "version.json"));
        if (webUIFileInfo.Exists)
            return JsonConvert.DeserializeObject<WebUIVersionInfo>(File.ReadAllText(webUIFileInfo.FullName));
        return null;
    }

    private static WebUIVersionInfo? LoadIncludedWebUIVersionInfo(IApplicationPaths? applicationPaths = null)
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

    #endregion

    #endregion

    #region Server

    /// <inheritdoc />
    public string ServerRepositoryName
    {
        get => _settingsProvider.GetSettings().Web.ServerRepoName;
        set
        {
            // If validation fails then catch and swallow the exception.
            try
            {
                var copy = _settingsProvider.GetSettings(true);
                copy.Web.ServerRepoName = value;
                _settingsProvider.SaveSettings(copy);
            }
            catch { }
        }
    }
    /// <inheritdoc />
    public async Task<ReleaseVersionInformation?> GetLatestServerVersion(ReleaseChannel channel = ReleaseChannel.Auto, bool force = false)
    {
        if (channel == ReleaseChannel.Auto)
            channel = GetCurrentServerReleaseChannel();
        var key = $"server:{channel}";
        if (!force && _cache.TryGetValue<ReleaseVersionInformation>(key, out var componentVersion))
            return componentVersion!;
        switch (channel)
        {
            // Check for dev channel updates.
            case ReleaseChannel.Dev:
            {
                var latestTags = await DownloadApiResponse($"tags?per_page=100&page=1", ServerRepositoryName).ConfigureAwait(false);
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

                var latestCommit = await DownloadApiResponse($"commits/{commitSha}", ServerRepositoryName).ConfigureAwait(false);
                DateTime releaseDate = latestCommit.commit.author.date;
                releaseDate = releaseDate.ToUniversalTime();
                string description;
                // We're on a local build.
                if (PluginManager.GetVersionInformation().SourceRevision is not { Length: > 0 } currentCommit)
                {
                    description = "Local build detected. Unable to determine the relativeness of the latest daily release.";
                }
                // We're not on the latest daily release.
                else if (!string.Equals(currentCommit, commitSha))
                {
                    var diff = await DownloadApiResponse($"compare/{commitSha}...{currentCommit}", ServerRepositoryName).ConfigureAwait(false);
                    var aheadBy = (int)diff.ahead_by;
                    var behindBy = (int)diff.behind_by;
                    description = $"You are currently {aheadBy} commits ahead and {behindBy} commits behind the latest daily release.";
                }
                // We're on the latest daily release.
                else
                {
                    description = "All caught up! You are running the latest daily release.";
                }
                return _cache.Set(key, new ReleaseVersionInformation
                {
                    Version = version,
                    SourceRevision = commitSha,
                    Channel = ReleaseChannel.Dev,
                    ReleasedAt = releaseDate,
                    ReleaseTag = tagName,
                    Description = description,
                }, _cacheTTL);
            }

            // Check for stable channel updates.
            default:
            {
                var latestRelease = await DownloadApiResponse("releases/latest", ServerRepositoryName).ConfigureAwait(false);
                string tagName = latestRelease.tag_name;
                var tagResponse = await DownloadApiResponse($"git/ref/tags/{tagName}", ServerRepositoryName).ConfigureAwait(false);
                var plainTagName = tagName[0] == 'v' || tagName[0] == 'V' ? tagName[1..] : tagName;
                var version = Version.Parse(plainTagName.Replace("-dev", ""));
                string commit = tagResponse["object"].sha;
                DateTime releaseDate = latestRelease.published_at;
                releaseDate = releaseDate.ToUniversalTime();
                string? description = latestRelease.body;
                return _cache.Set(key, new ReleaseVersionInformation
                {
                    Version = version,
                    SourceRevision = commit,
                    Channel = ReleaseChannel.Stable,
                    ReleasedAt = releaseDate,
                    ReleaseTag = tagName,
                    Description = description?.Trim() ?? string.Empty,
                }, _cacheTTL);
            }
        }
    }

    #region Server | Internals

    private ReleaseChannel GetCurrentServerReleaseChannel()
    {
        if (_systemService.Version.Channel is ReleaseChannel.Debug)
            return ReleaseChannel.Stable;
        return _systemService.Version.Channel;
    }

    #endregion

    #endregion
}
