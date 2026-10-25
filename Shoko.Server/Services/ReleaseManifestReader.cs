using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Web;

namespace Shoko.Server.Services;

/// <summary>
///   Reads the release manifests of the server and the Shoko Web UI, keeping
///   a copy of each in the data folder for an hour.
/// </summary>
public class ReleaseManifestReader(ILogger<ReleaseManifestReader> logger, IHttpClientFactory httpClientFactory, IApplicationPaths applicationPaths)
{
    private static readonly TimeSpan _cacheTTL = TimeSpan.FromHours(1);

    private readonly SemverVersionComparer _versionComparer = new();

    #region Models

    /// <summary>
    ///   A release manifest.
    /// </summary>
    internal class ManifestModel
    {
        [JsonProperty("Stable")]
        public List<ManifestEntry> Stable { get; set; } = [];

        [JsonProperty("Dev")]
        public List<ManifestEntry> Dev { get; set; } = [];
    }

    /// <summary>
    ///   A release in a manifest.
    /// </summary>
    public class ManifestEntry
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

    #region Reading

    /// <summary>
    ///   Fetches a manifest, or reads the cached copy while it is fresh. A
    ///   manifest that cannot be fetched falls back to the cached copy.
    /// </summary>
    /// <param name="manifestUrl">The manifest's URL.</param>
    /// <param name="localName">The name of the cached copy in the data folder.</param>
    /// <param name="force">Fetch the manifest even while the cached copy is fresh.</param>
    /// <param name="cancellationToken">A cancellation token for cancelling the fetch.</param>
    /// <returns>Every release in the manifest, newest first.</returns>
    public async Task<IReadOnlyList<(ManifestEntry Entry, ReleaseChannel Channel, Version Version)>> FetchManifestAsync(
        string manifestUrl,
        string localName,
        bool force,
        CancellationToken cancellationToken = default
    )
    {
        var cachedFilePath = Path.Join(applicationPaths.DataPath, localName);
        ManifestModel? manifest = null;
        try
        {
            if (!force && File.Exists(cachedFilePath) && File.GetLastWriteTimeUtc(cachedFilePath) > DateTime.UtcNow.Subtract(_cacheTTL))
            {
                manifest = JsonConvert.DeserializeObject<ManifestModel>(await File.ReadAllTextAsync(cachedFilePath, cancellationToken));
            }
            else
            {
                using var client = httpClientFactory.CreateClient("Default");
                var response = await client.GetStringAsync(new Uri(manifestUrl), cancellationToken);
                manifest = JsonConvert.DeserializeObject<ManifestModel>(response);
                await File.WriteAllTextAsync(cachedFilePath, response, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                logger.LogDebug("Gave up fetching the manifest from {ManifestUrl}", manifestUrl);
            else if (ex is not HttpRequestException { StatusCode: HttpStatusCode.NotFound })
                logger.LogWarning(ex, "Failed to fetch manifest from {ManifestUrl}", manifestUrl);

            if (!File.Exists(cachedFilePath))
                return [];

            try
            {
                manifest = JsonConvert.DeserializeObject<ManifestModel>(File.ReadAllText(cachedFilePath));
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
        foreach (var entry in manifest.Stable ?? [])
            collected.Add((entry, ReleaseChannel.Stable, ParseVersion(entry.Version)));
        foreach (var entry in manifest.Dev ?? [])
            collected.Add((entry, ReleaseChannel.Dev, ParseVersion(entry.Version)));
        return collected
            .OrderByDescending(entry => entry.Version, _versionComparer)
            .DistinctBy(entry => entry.Entry.Version)
            .ToList();
    }

    #endregion

    #region Conversion

    /// <summary>
    ///   Converts a manifest entry to release information.
    /// </summary>
    /// <param name="entry">The manifest entry.</param>
    /// <param name="channel">The channel the entry is listed under.</param>
    /// <returns>The release information.</returns>
    public static ReleaseVersionInformation ToReleaseInfo(ManifestEntry entry, ReleaseChannel channel)
        => new()
        {
            Version = ParseVersion(entry.Version),
            Description = entry.ReleaseNotes?.Trim() ?? string.Empty,
            SourceRevision = entry.Commit is { Length: > 0 } commit ? commit : string.Empty,
            ReleaseTag = entry.Tag is { Length: > 0 } tag ? tag : entry.Version,
            Channel = channel,
            ReleasedAt = entry.Date ?? DateTime.MinValue,
        };

    /// <summary>
    ///   Converts a manifest entry to web UI release information.
    /// </summary>
    /// <param name="entry">The manifest entry.</param>
    /// <param name="channel">The channel the entry is listed under.</param>
    /// <returns>The web UI release information.</returns>
    public static WebUIReleaseInformation ToWebUIReleaseInfo(ManifestEntry entry, ReleaseChannel channel)
    {
        var baseInfo = ToReleaseInfo(entry, channel);
        return new()
        {
            Version = baseInfo.Version,
            Description = baseInfo.Description,
            SourceRevision = baseInfo.SourceRevision,
            ReleaseTag = baseInfo.ReleaseTag,
            Channel = baseInfo.Channel,
            ReleasedAt = baseInfo.ReleasedAt,
            MinimumServerVersion = TryParseVersion(entry.MinServerVersion),
            MaximumServerVersion = TryParseVersion(entry.MaxServerVersion),
        };
    }

    /// <summary>
    ///   Parses a release version, <c>-dev.N</c> becoming the revision.
    /// </summary>
    /// <param name="version">The version, with or without a leading <c>v</c>.</param>
    /// <returns>The version.</returns>
    internal static Version ParseVersion(string version)
        => Version.Parse(version.Replace("-dev", "").TrimStart('v').TrimStart('V'));

    /// <summary>
    ///   Parses a release version like <see cref="ParseVersion"/>, if there is
    ///   one and it is valid.
    /// </summary>
    /// <param name="version">The version, with or without a leading <c>v</c>.</param>
    /// <returns>The version, or <c>null</c>.</returns>
    internal static Version? TryParseVersion(string? version)
        => version is { Length: > 0 } && Version.TryParse(version.Replace("-dev", "").TrimStart('v').TrimStart('V'), out var parsed) ? parsed : null;

    #endregion
}
