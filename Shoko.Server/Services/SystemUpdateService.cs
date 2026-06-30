using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SharpCompress.Common;
using SharpCompress.Readers;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Utilities;

using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

namespace Shoko.Server.Services;

public partial class SystemUpdateService(
    ILogger<SystemUpdateService> logger,
    ISettingsProvider settingsProvider,
    ISystemService systemService,
    IHttpClientFactory httpClientFactory,
    IApplicationPaths applicationPaths
) : ISystemUpdateService
{
    private readonly SemverVersionComparer _versionComparer = new();

    private static readonly TimeSpan _cacheTTL = TimeSpan.FromHours(1);

    #region Manifest Models

    internal class ManifestModel
    {
        [JsonProperty("Stable")]
        public List<ManifestEntry> Stable { get; set; } = [];

        [JsonProperty("Dev")]
        public List<ManifestEntry> Dev { get; set; } = [];
    }

    internal class ManifestEntry
    {
        [JsonProperty("version")]
        public string Version { get; set; } = string.Empty;

        [JsonProperty("minServerVersion")]
        public string? MinServerVersion { get; set; }

        [JsonProperty("maxServerVersion")]
        public string? MaxServerVersion { get; set; }

        [JsonProperty("downloadUrl")]
        public string DownloadUrl { get; set; } = string.Empty;

        [JsonProperty("checksum")]
        public string? Checksum { get; set; }

        [JsonProperty("releaseNotes")]
        public string? ReleaseNotes { get; set; }

        [JsonProperty("commit")]
        public string? Commit { get; set; }

        [JsonProperty("tag")]
        public string? Tag { get; set; }

        [JsonProperty("date")]
        public DateTime? Date { get; set; }
    }

    #endregion

    #region Helpers

    private async Task<IEnumerable<(ManifestEntry Entry, ReleaseChannel Channel, Version Version)>> FetchManifestAsync(string manifestUrl, string localName, bool force)
    {
        using var client = httpClientFactory.CreateClient("Default");
        var cachedFilePath = Path.Join(applicationPaths.DataPath, localName);
        ManifestModel? manifest = null;
        try
        {
            if (!force && File.Exists(cachedFilePath) && File.GetLastWriteTimeUtc(cachedFilePath) > DateTime.UtcNow.Subtract(_cacheTTL))
            {
                var cached = File.ReadAllText(cachedFilePath);
                manifest = JsonConvert.DeserializeObject<ManifestModel>(cached);
            }
            else
            {
                var response = await client.GetStringAsync(new Uri(manifestUrl));
                manifest = JsonConvert.DeserializeObject<ManifestModel>(response);
                File.WriteAllText(cachedFilePath, response);
            }
        }
        catch (Exception ex)
        {
            if (ex is not HttpRequestException { StatusCode: HttpStatusCode.NotFound })
                logger.LogWarning(ex, "Failed to fetch manifest from {ManifestUrl}", manifestUrl);

            if (!File.Exists(cachedFilePath))
                return [];

            try
            {
                var cached = File.ReadAllText(cachedFilePath);
                manifest = JsonConvert.DeserializeObject<ManifestModel>(cached);
            }
            catch (Exception ex2)
            {
                logger.LogWarning(ex2, "Failed to parse cached manifest from {ManifestUrl}", manifestUrl);
                return [];
            }
        }
        if (manifest is null)
            return [];

        var collected = new List<(ManifestEntry Entry, ReleaseChannel Channel, Version Version)>();
        foreach (var e in manifest.Stable ?? [])
            collected.Add((e, ReleaseChannel.Stable, Version.Parse(e.Version.Replace("-dev", "").TrimStart('v').TrimStart('V'))));
        foreach (var e in manifest.Dev ?? [])
            collected.Add((e, ReleaseChannel.Dev, Version.Parse(e.Version.Replace("-dev", "").TrimStart('v').TrimStart('V'))));
        return collected
            .OrderByDescending(e => e.Version, _versionComparer)
            .DistinctBy(e => e.Entry.Version);
    }

    private ReleaseVersionInformation EntryToReleaseInfo(ManifestEntry entry, ReleaseChannel channel)
    {
        var version = Version.Parse(entry.Version.Replace("-dev", "").TrimStart('v').TrimStart('V'));
        var tag = entry.Tag;
        if (string.IsNullOrEmpty(tag))
            tag = entry.Version;

        var commit = entry.Commit;
        if (string.IsNullOrEmpty(commit))
            commit = "";

        return new ReleaseVersionInformation
        {
            Version = version,
            Description = entry.ReleaseNotes?.Trim() ?? string.Empty,
            SourceRevision = commit,
            ReleaseTag = tag,
            Channel = channel,
            ReleasedAt = entry.Date ?? DateTime.MinValue,
        };
    }

    private WebReleaseVersionInformation EntryToWebReleaseInfo(ManifestEntry entry, ReleaseChannel channel)
    {
        Version? minServerVersion = null;
        if (entry.MinServerVersion is { Length: > 0 } minVer && Version.TryParse(minVer.Replace("-dev", "").TrimStart('v').TrimStart('V'), out var parsedMin))
            minServerVersion = parsedMin;

        Version? maxServerVersion = null;
        if (entry.MaxServerVersion is { Length: > 0 } maxVer && Version.TryParse(maxVer.Replace("-dev", "").TrimStart('v').TrimStart('V'), out var parsedMax))
            maxServerVersion = parsedMax;

        var baseInfo = EntryToReleaseInfo(entry, channel);
        return new WebReleaseVersionInformation
        {
            Version = baseInfo.Version,
            Description = baseInfo.Description,
            SourceRevision = baseInfo.SourceRevision,
            ReleaseTag = baseInfo.ReleaseTag,
            Channel = baseInfo.Channel,
            ReleasedAt = baseInfo.ReleasedAt,
            MinimumServerVersion = minServerVersion,
            MaximumServerVersion = maxServerVersion
        };
    }

    #endregion

    #region Web Component

    /// <inheritdoc />
    public event EventHandler? WebComponentUpdated;

    /// <inheritdoc />
    public string WebComponentManifestUrl
    {
        get => settingsProvider.GetSettings().Web.ClientManifestUrl;
        set
        {
            // If validation fails then catch and swallow the exception.
            try
            {
                var copy = settingsProvider.GetSettings(true);
                copy.Web.ClientManifestUrl = value;
                settingsProvider.SaveSettings(copy);
            }
            catch { }
        }
    }

    /// <inheritdoc />
    public WebReleaseVersionInformation? LoadWebComponentVersionInformation()
    {
        if (LoadWebUIVersionInfo(applicationPaths) is not { } webVer)
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
        if (LoadIncludedWebUIVersionInfo(applicationPaths) is not { } webVer)
            return null;
        return new()
        {
            Version = webVer.VersionAsVersion,
            Channel = webVer.Channel,
            MinimumServerVersion = webVer.MinimumServerVersion,
            MaximumServerVersion = webVer.MaximumServerVersion,
            Description = null,
            ReleasedAt = webVer.Date ?? DateTime.MinValue,
            ReleaseTag = webVer.Tag,
            SourceRevision = webVer.Commit,
        };
    }

    /// <inheritdoc />
    public async Task<bool> UpdateWebComponent(ReleaseChannel channel = ReleaseChannel.Auto, bool allowIncompatible = false)
    {
        var version = await GetLatestWebComponentVersion(channel, allowIncompatible: allowIncompatible);
        return await InstallWebComponentVersion(version);
    }

    /// <inheritdoc />
    public async Task<bool> InstallWebComponentVersion(WebReleaseVersionInformation version)
    {
        var versions = await FetchManifestAsync(WebComponentManifestUrl, "web-manifest.json", false);
        var entry = versions.FirstOrDefault(e => e.Version == version.Version);
        if (entry.Entry is null)
            return false;

        await DownloadAndInstallUpdate(entry.Entry.DownloadUrl, version);

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
        if (channel is ReleaseChannel.Auto)
            channel = GetCurrentWebUIReleaseChannel();

        var versions = await GetWebComponentHistory(channel, force);
        var nextVersionIndex = -1;
        var currentServerVersion = Assembly.GetExecutingAssembly().GetName().Version!;
        foreach (var (version, index) in versions.Select((version, index) => (version, index)))
        {
            // Check minimum server version compatibility.
            if (!allowIncompatible && version.MinimumServerVersion is var minServerVersion && _versionComparer.Compare(minServerVersion, currentServerVersion) > 0)
                continue;

            // Check maximum server version compatibility.
            if (!allowIncompatible && version.MaximumServerVersion is { } maxServerVersion && _versionComparer.Compare(maxServerVersion, currentServerVersion) < 0)
                continue;

            nextVersionIndex = index;
            break;
        }

        var webuiVersion = LoadWebUIVersionInfo();
        if (nextVersionIndex >= 0)
        {
            var currentVersionIndex = webuiVersion is null ? -1 : versions.FindIndex(v =>
                v.Version == webuiVersion.VersionAsVersion &&
                v.Channel == webuiVersion.Channel
            );
            if (currentVersionIndex is -1)
                return versions[nextVersionIndex];

            var releaseNotes = new StringBuilder();
            if (nextVersionIndex == currentVersionIndex)
            {
                releaseNotes.Append(versions[currentVersionIndex].Description ?? "N/A");
            }
            else
            {
                var endIndex = Math.Min(currentVersionIndex - 1, nextVersionIndex + 19);
                for (var i = endIndex; i >= nextVersionIndex; i--)
                    releaseNotes
                        .AppendLine()
                        .AppendLine($"**{versions[i].Version}**:")
                        .AppendLine()
                        .AppendLine(versions[i].Description ?? "N/A");
                if (endIndex < currentVersionIndex - 1)
                    releaseNotes
                        .AppendLine()
                        .AppendLine("…");
            }

            return new WebReleaseVersionInformation
            {
                Version = versions[nextVersionIndex].Version,
                MinimumServerVersion = versions[nextVersionIndex].MinimumServerVersion,
                MaximumServerVersion = versions[nextVersionIndex].MaximumServerVersion,
                SourceRevision = versions[nextVersionIndex].SourceRevision,
                Channel = channel,
                ReleasedAt = versions[nextVersionIndex].ReleasedAt,
                ReleaseTag = versions[nextVersionIndex].ReleaseTag,
                Description = releaseNotes.ToString().Trim(),
            };
        }

        // If on a non-Dev channel and no compatible release was found, fall back
        // to the currently installed version.
        return new WebReleaseVersionInformation
        {
            Version = webuiVersion?.VersionAsVersion ?? new(1, 0, 0),
            MinimumServerVersion = webuiVersion?.MinimumServerVersion,
            MaximumServerVersion = webuiVersion?.MaximumServerVersion,
            SourceRevision = webuiVersion?.Commit ?? "",
            Channel = channel,
            ReleasedAt = webuiVersion?.Date ?? DateTime.MinValue,
            ReleaseTag = webuiVersion?.Tag ?? "1.0.0",
            Description = string.Empty,
        };

    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WebReleaseVersionInformation>> GetWebComponentHistory(ReleaseChannel? channel = null, bool force = false)
    {
        if (channel is ReleaseChannel.Auto)
            channel = GetCurrentWebUIReleaseChannel();

        return (await FetchManifestAsync(WebComponentManifestUrl, "webui-manifest.json", force))
            .Where(tuple => !channel.HasValue || tuple.Channel == channel.Value)
            .Select(tuple => EntryToWebReleaseInfo(tuple.Entry, tuple.Channel))
            .ToList();
    }

    #region Web Component | Internals

    /// <summary>
    /// Download and install update.
    /// </summary>
    /// <param name="url">direct link to version you want to install</param>
    /// <param name="version">Version to download.</param>
    /// <exception cref="WebException">An error occurred while downloading the resource.</exception>
    private async Task DownloadAndInstallUpdate(string url, WebReleaseVersionInformation version)
    {
        var webuiDir = applicationPaths.WebPath;
        var backupDir = Path.Combine(webuiDir, "old");
        var files = Directory.GetFiles(webuiDir);
        var directories = Directory.GetDirectories(webuiDir);

        // Make sure the base directory exists.
        if (!Directory.Exists(webuiDir))
            Directory.CreateDirectory(webuiDir);

        // Download the zip file.
        var client = httpClientFactory.CreateClient("Default");
        var zipContent = await client.GetByteArrayAsync(url);

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
        using var reader = ReaderFactory.OpenReader(stream);
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
        var webUIFileInfo = new FileInfo(Path.Join(applicationPaths.WebPath, "version.json"));
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
        if (version.MaximumServerVersion is null ? webuiVersion.MaximumServerVersion is not null : (webuiVersion.MaximumServerVersion is not { } || webuiVersion.MaximumServerVersion != version.MaximumServerVersion))
        {
            webuiVersion.MaximumServerVersion = version.MaximumServerVersion;
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
        /// Maximum Shoko Server version compatible with the Web UI.
        /// </summary>
        [JsonProperty("maximumServerVersion", NullValueHandling = NullValueHandling.Ignore)]
        public string? MaximumServerVersionAsString { get; set; }

        /// <summary>
        /// Maximum Shoko Server version compatible with the Web UI.
        /// </summary>
        [JsonIgnore]
        public Version? MaximumServerVersion
        {
            get => MaximumServerVersionAsString is { Length: > 0 }
                ? new(MaximumServerVersionAsString.Replace("-dev", ""))
                : null;
            set
            {
                if (value is null)
                    MaximumServerVersionAsString = null;
                else if (value is not { Revision: > 0 })
                    MaximumServerVersionAsString = $"{value.Major}.{value.Minor}.{value.Build}";
                else
                    MaximumServerVersionAsString = $"{value.Major}.{value.Minor}.{value.Build}-dev.{value.Revision}";
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
    public string ServerManifestUrl
    {
        get => settingsProvider.GetSettings().Web.ServerManifestUrl;
        set
        {
            // If validation fails then catch and swallow the exception.
            try
            {
                var copy = settingsProvider.GetSettings(true);
                copy.Web.ServerManifestUrl = value;
                settingsProvider.SaveSettings(copy);
            }
            catch { }
        }
    }

    /// <inheritdoc />
    public async Task<ReleaseVersionInformation?> GetLatestServerVersion(ReleaseChannel channel = ReleaseChannel.Auto, bool force = false)
    {
        if (channel is ReleaseChannel.Auto)
            channel = GetCurrentServerReleaseChannel();

        var versions = await GetServerHistory(channel, force);
        if (versions.Count is 0)
            return null;

        // The latest version is at index 0 (sorted descending).
        var latestVersion = versions[0];
        var currentServerVersion = Assembly.GetExecutingAssembly().GetName().Version!;
        var currentVersionIndex = versions.FindIndex(v => v.Version == currentServerVersion);
        if (currentVersionIndex is <= 0)
            return latestVersion;

        var endIndex = Math.Min(currentVersionIndex - 1, 19);
        var releaseNotes = new StringBuilder();
        for (var i = endIndex; i >= 0; i--)
            releaseNotes
                .AppendLine()
                .AppendLine($"**{versions[i].Version}**:")
                .AppendLine()
                .AppendLine(versions[i].Description ?? "N/A");
        if (endIndex < currentVersionIndex - 1)
            releaseNotes
                .AppendLine()
                .AppendLine("…");

        return new()
        {
            Version = latestVersion.Version,
            Description = releaseNotes.ToString().Trim(),
            SourceRevision = latestVersion.SourceRevision,
            ReleaseTag = latestVersion.ReleaseTag,
            Channel = latestVersion.Channel,
            ReleasedAt = latestVersion.ReleasedAt,
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReleaseVersionInformation>> GetServerHistory(ReleaseChannel? channel = null, bool force = false)
    {
        if (channel is ReleaseChannel.Auto)
            channel = GetCurrentServerReleaseChannel();

        return (await FetchManifestAsync(ServerManifestUrl, "server-manifest.json", force))
            .Where(tuple => !channel.HasValue || tuple.Channel == channel.Value)
            .Select(tuple => EntryToReleaseInfo(tuple.Entry, tuple.Channel))
            .ToList();
    }

    #region Server | Internals

    private ReleaseChannel GetCurrentServerReleaseChannel()
    {
        if (systemService.Version.Channel is ReleaseChannel.Debug)
            return ReleaseChannel.Stable;
        return systemService.Version.Channel;
    }

    #endregion

    #endregion
}
