using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SharpCompress.Common;
using SharpCompress.Readers;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Web;
using Shoko.Abstractions.Web.Enums;
using Shoko.Server.Services;

using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

namespace Shoko.Server.API.FileProviders;

/// <summary>
///   The server's own web UI: the Shoko Web UI, installed in the
///   <c>webui</c> folder of the <see cref="IApplicationPaths.DataPath"/> and
///   served from <c>/webui</c>. Paths that are not files fall back to the
///   index, for the Web UI's own routes.
/// </summary>
public class CoreWebUIProvider : IWebUIProvider
{
    #region Fields

    /// <summary>
    ///   The path the Shoko Web UI is served from. A web UI anywhere else is a
    ///   plugin's.
    /// </summary>
    public const string WebUIPath = "/webui";

    /// <summary>
    ///   Found in the index of the bootstrapper page that asks the user to
    ///   install the Web UI, which is not a release.
    /// </summary>
    private const string InstallToken = "install-web-ui";

    /// <summary>
    ///   The name of the cached copy of the Web UI's manifest.
    /// </summary>
    private const string ManifestCacheName = "webui-manifest.json";

    /// <summary>
    ///   Folders inside the installation that an install leaves alone.
    /// </summary>
    private static readonly HashSet<string> _keptEntries = new(StringComparer.OrdinalIgnoreCase) { ".staging", ".backup", "tweak" };

    /// <summary>
    ///   Extensions of the files a Web UI build serves; a missing path with
    ///   one of them is a missing asset, not one of the Web UI's routes.
    /// </summary>
    private static readonly HashSet<string> _assetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".js", ".mjs", ".css", ".map", ".woff", ".woff2", ".ttf", ".otf", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".avif", ".svg", ".ico",
        ".json", ".webmanifest", ".txt", ".wasm",
    };

    private readonly ILogger<CoreWebUIProvider> _logger;

    private readonly ISettingsProvider _settingsProvider;

    private readonly IApplicationPaths _applicationPaths;

    private readonly IHttpClientFactory _httpClientFactory;

    private readonly ReleaseManifestReader _manifestReader;

    /// <summary>
    ///   The folder the Web UI is installed in.
    /// </summary>
    private readonly string _installPath;

    private readonly PhysicalFileProvider _files;

    private readonly SemaphoreSlim _installLock = new(1, 1);

    #endregion

    #region Constructors

    public CoreWebUIProvider(
        ILogger<CoreWebUIProvider> logger,
        ISettingsProvider settingsProvider,
        IApplicationPaths applicationPaths,
        IHttpClientFactory httpClientFactory,
        ReleaseManifestReader manifestReader
    )
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _applicationPaths = applicationPaths;
        _httpClientFactory = httpClientFactory;
        _manifestReader = manifestReader;
        _installPath = Path.Combine(applicationPaths.DataPath, "webui");

        // A new installation starts with what the server ships, which may be
        // the page asking the user to install the Web UI rather than a release.
        if (!Directory.Exists(_installPath) || !Directory.EnumerateFileSystemEntries(_installPath).Any())
        {
            if (Directory.Exists(BundledPath))
                CopyFilesRecursively(new(BundledPath), new(_installPath));
            else
                Directory.CreateDirectory(_installPath);
        }

        _files = new(_installPath);
    }

    #endregion

    #region Identity

    /// <inheritdoc />
    public string Name => "Shoko Web UI";

    /// <inheritdoc />
    public string? Description => "The web UI that ships with the server.";

    /// <inheritdoc />
    public string MountPath => WebUIPath;

    /// <inheritdoc />
    public IReadOnlyDictionary<WebUILinkType, string> Links { get; } = new Dictionary<WebUILinkType, string>
    {
        [WebUILinkType.Home] = "/dashboard",
        [WebUILinkType.Administration] = "/settings",
        [WebUILinkType.Group] = "/collection/group/{groupID}",
        [WebUILinkType.Series] = "/collection/series/{seriesID}",
        [WebUILinkType.Configuration] = "/settings/plugin/config/{configurationID}",
        [WebUILinkType.PluginPage] = "/settings/plugin/page/{pluginID}/{pageID}",
    };

    #endregion

    #region Files

    /// <inheritdoc />
    public IFileInfo GetFileInfo(string subpath)
    {
        var fileInfo = _files.GetFileInfo(subpath);
        if (fileInfo is { Exists: true, IsDirectory: false })
            return fileInfo;

        return IsAssetPath(subpath) ? new NotFoundFileInfo(subpath) : _files.GetFileInfo("index.html");
    }

    /// <inheritdoc />
    public IDirectoryContents GetDirectoryContents(string subpath)
        => _files.GetDirectoryContents(subpath);

    /// <inheritdoc />
    public IChangeToken Watch(string filter)
        => NullChangeToken.Singleton;

    /// <summary>
    ///   Checks if a missing path is a missing asset rather than one of the
    ///   Web UI's routes.
    /// </summary>
    /// <param name="subpath">The requested path.</param>
    /// <returns><c>true</c> if the path is under <c>/assets/</c> or ends in a known asset extension.</returns>
    private static bool IsAssetPath(string subpath)
        => subpath.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase) || _assetExtensions.Contains(Path.GetExtension(subpath));

    #endregion

    #region Releases

    /// <inheritdoc />
    public event EventHandler? ReleaseInstalled;

    /// <inheritdoc />
    public WebUIReleaseInformation? CurrentRelease
    {
        get
        {
            var indexPath = Path.Join(_installPath, "index.html");
            if (!File.Exists(indexPath) || File.ReadAllText(indexPath).Contains(InstallToken))
                return null;

            return LoadVersionInfo(_installPath) is { } versionInfo ? versionInfo.ToRelease(isBundled: false) : null;
        }
    }

    /// <inheritdoc />
    public WebUIReleaseInformation? BundledRelease
        => LoadVersionInfo(BundledPath) is { } versionInfo ? versionInfo.ToRelease(isBundled: true) : null;

    /// <inheritdoc />
    public async Task<IReadOnlyList<WebUIReleaseInformation>> GetReleases(bool force = false, CancellationToken cancellationToken = default)
    {
        var releases = (await _manifestReader.FetchManifestAsync(_settingsProvider.GetSettings().Web.ClientManifestUrl, ManifestCacheName, force, cancellationToken))
            .Select(tuple => ReleaseManifestReader.ToWebUIReleaseInfo(tuple.Entry, tuple.Channel))
            .ToList();

        // The bundled release is installed from the box, so mark it where the manifest lists it too.
        if (BundledRelease is { } bundled)
        {
            if (releases.FirstOrDefault(release => IsSameRelease(release, bundled)) is { } listed)
                listed.IsBundled = true;
            else
                releases.Insert(releases.FindIndex(release => release.Version < bundled.Version) is var index and >= 0 ? index : releases.Count, bundled);
        }

        return releases;
    }

    /// <inheritdoc />
    public async Task<WebUIInstallResult> InstallRelease(WebUIReleaseInformation release, CancellationToken cancellationToken = default)
    {
        await _installLock.WaitAsync(cancellationToken);
        try
        {
            if (BundledRelease is { } bundled && (release.IsBundled || IsSameRelease(release, bundled)))
            {
                _logger.LogInformation("Installing the bundled Web UI, version {Version}.", bundled.Version.ToSemanticVersioningString());
                ReplaceInstalledFiles(staging => CopyFilesRecursively(new(BundledPath), new(staging)));
            }
            else
            {
                var manifest = await _manifestReader.FetchManifestAsync(_settingsProvider.GetSettings().Web.ClientManifestUrl, ManifestCacheName, false, cancellationToken);
                var (entry, _, _) = manifest.FirstOrDefault(tuple => tuple.Version == release.Version && tuple.Channel == release.Channel);
                if (entry is null)
                {
                    _logger.LogWarning("Unable to install Web UI version {Version}, since the manifest does not list it.", release.Version.ToSemanticVersioningString());
                    return WebUIInstallResult.Failed;
                }

                _logger.LogInformation("Downloading Web UI version {Version}.", release.Version.ToSemanticVersioningString());
                using var client = _httpClientFactory.CreateClient("Default");
                var zipContent = await client.GetByteArrayAsync(entry.DownloadUrl, cancellationToken);
                ReplaceInstalledFiles(staging => ExtractArchive(zipContent, staging));
                UpdateVersionInfo(release);
            }
        }
        finally
        {
            _installLock.Release();
        }

        _ = Task.Run(() => ReleaseInstalled?.Invoke(this, EventArgs.Empty), CancellationToken.None);
        return WebUIInstallResult.Installed;
    }

    #endregion

    #region Installation

    private string BundledPath => Path.Combine(_applicationPaths.ApplicationPath, "webui");

    private static bool IsSameRelease(WebUIReleaseInformation a, WebUIReleaseInformation b)
        => a.Version == b.Version && a.Channel == b.Channel;

    /// <summary>
    ///   Replaces the installed files with new ones, prepared in a folder
    ///   inside the installation first, so a failed install leaves the old
    ///   files in place and requests never see half of the new ones.
    /// </summary>
    /// <param name="fill">Fills the staging folder it is given with the new files.</param>
    private void ReplaceInstalledFiles(Action<string> fill)
    {
        var webPath = _installPath;
        var stagingPath = Path.Combine(webPath, ".staging");
        var backupPath = Path.Combine(webPath, ".backup");
        Directory.CreateDirectory(webPath);
        if (Directory.Exists(stagingPath))
            Directory.Delete(stagingPath, true);
        if (Directory.Exists(backupPath))
            Directory.Delete(backupPath, true);

        Directory.CreateDirectory(stagingPath);
        try
        {
            fill(stagingPath);
        }
        catch
        {
            Directory.Delete(stagingPath, true);
            throw;
        }

        Directory.CreateDirectory(backupPath);
        MoveEntries(webPath, backupPath);
        try
        {
            MoveEntries(stagingPath, webPath);
        }
        catch
        {
            MoveEntries(webPath, stagingPath);
            MoveEntries(backupPath, webPath);
            throw;
        }
        finally
        {
            if (Directory.Exists(stagingPath))
                Directory.Delete(stagingPath, true);
        }

        Directory.Delete(backupPath, true);
    }

    private static void MoveEntries(string source, string target)
    {
        foreach (var directory in Directory.GetDirectories(source))
        {
            var name = Path.GetFileName(directory);
            if (!_keptEntries.Contains(name))
                Directory.Move(directory, Path.Combine(target, name));
        }

        foreach (var file in Directory.GetFiles(source))
            File.Move(file, Path.Combine(target, Path.GetFileName(file)), true);
    }

    private static void ExtractArchive(byte[] zipContent, string target)
    {
        using var stream = new MemoryStream(zipContent);
        using var reader = ReaderFactory.OpenReader(stream);
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
                reader.WriteEntryToDirectory(target, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
        }
    }

    private static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
    {
        target.Create();
        foreach (var directory in source.GetDirectories())
            CopyFilesRecursively(directory, target.CreateSubdirectory(directory.Name));

        foreach (var file in source.GetFiles())
            file.CopyTo(Path.Combine(target.FullName, file.Name), true);
    }

    private void UpdateVersionInfo(WebUIReleaseInformation release)
    {
        var path = Path.Join(_installPath, "version.json");
        var versionInfo = LoadVersionInfo(_installPath) ?? new();
        versionInfo.Version = release.Version.ToSemanticVersioningString();
        versionInfo.MinimumServerVersion = release.MinimumServerVersion;
        versionInfo.MaximumServerVersion = release.MaximumServerVersion;
        versionInfo.Tag = release.ReleaseTag;
        versionInfo.Commit = release.SourceRevision;
        versionInfo.Date = release.ReleasedAt;
        versionInfo.Channel = release.Channel;
        versionInfo.IsDebug = null;
        File.WriteAllText(path, JsonConvert.SerializeObject(versionInfo));
    }

    private static WebUIVersionInfo? LoadVersionInfo(string directory)
    {
        var path = Path.Join(directory, "version.json");
        return File.Exists(path) ? JsonConvert.DeserializeObject<WebUIVersionInfo>(File.ReadAllText(path)) : null;
    }

    #endregion

    #region Version Info

    /// <summary>
    ///   The <c>version.json</c> file of a Web UI build.
    /// </summary>
    public class WebUIVersionInfo
    {
        /// <summary>
        ///   Package version.
        /// </summary>
        [JsonProperty("package")]
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        ///   Minimum server version compatible with the Web UI.
        /// </summary>
        [JsonProperty("minimumServerVersion", NullValueHandling = NullValueHandling.Ignore)]
        public string? MinimumServerVersionAsString { get; set; }

        /// <summary>
        ///   Minimum server version compatible with the Web UI.
        /// </summary>
        [JsonIgnore]
        public Version? MinimumServerVersion
        {
            get => ReleaseManifestReader.TryParseVersion(MinimumServerVersionAsString);
            set => MinimumServerVersionAsString = value?.ToSemanticVersioningString();
        }

        /// <summary>
        ///   Maximum server version compatible with the Web UI.
        /// </summary>
        [JsonProperty("maximumServerVersion", NullValueHandling = NullValueHandling.Ignore)]
        public string? MaximumServerVersionAsString { get; set; }

        /// <summary>
        ///   Maximum server version compatible with the Web UI.
        /// </summary>
        [JsonIgnore]
        public Version? MaximumServerVersion
        {
            get => ReleaseManifestReader.TryParseVersion(MaximumServerVersionAsString);
            set => MaximumServerVersionAsString = value?.ToSemanticVersioningString();
        }

        /// <summary>
        ///   Git tag.
        /// </summary>
        [JsonProperty("tag")]
        public string Tag { get; set; } = "v1.0.0";

        /// <summary>
        ///   Long-form git commit sha digest.
        /// </summary>
        [JsonProperty("git")]
        public string Commit { get; set; } = "0000000";

        /// <summary>
        ///   Release date for web ui release.
        /// </summary>
        [JsonProperty("date", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Date { get; set; } = null;

        [JsonIgnore]
        private ReleaseChannel? _channel = null;

        /// <summary>
        ///   Cached release channel.
        /// </summary>
        [JsonProperty("channel")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ReleaseChannel Channel
        {
            get => _channel ??= IsDebug.HasValue && IsDebug.Value ? ReleaseChannel.Debug : Version.Contains("-dev") ? ReleaseChannel.Dev : ReleaseChannel.Stable;
            set => _channel = value;
        }

        /// <summary>
        ///   True if this is a debug package.
        /// </summary>
        [JsonProperty("debug", NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? IsDebug { get; set; }

        /// <summary>
        ///   Converts the file to release information.
        /// </summary>
        /// <param name="isBundled">Whether the file is the bundled release's.</param>
        /// <returns>The release information.</returns>
        public WebUIReleaseInformation ToRelease(bool isBundled)
            => new()
            {
                Version = ReleaseManifestReader.ParseVersion(Version),
                Channel = Channel,
                MinimumServerVersion = MinimumServerVersion,
                MaximumServerVersion = MaximumServerVersion,
                Description = null,
                ReleasedAt = Date ?? DateTime.MinValue,
                ReleaseTag = Tag,
                SourceRevision = Commit,
                IsBundled = isBundled,
            };
    }

    #endregion
}
