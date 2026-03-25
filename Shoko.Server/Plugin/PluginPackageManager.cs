using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Quartz;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Events;
using Shoko.Abstractions.Plugin.Exceptions;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Models.Internal;
using Shoko.Server.Plugin.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#nullable enable

namespace Shoko.Server.Plugin;

/// <summary>
///   Responsible for package installation, upgrades, and repository management.
/// </summary>
public partial class PluginPackageManager(
    ConfigurationProvider<ServerSettings> configurationProvider,
    ILogger<PluginPackageManager> logger,
    IHttpClientFactory httpClientFactory,
    IPluginManager pluginManager,
    IApplicationPaths applicationPaths,
    ISchedulerFactory schedulerFactory
) : IPluginPackageManager
{
    internal const string Repositories = "repositories";

    private const string RepoInfoFile = "info.json";

    private const string ManifestFile = "manifest.json";

    private static readonly PackageRepositoryInfo _localRepositoryInfo = new()
    {
        ID = Guid.Empty,
        Name = "Local Repository",
        Url = "file://%PluginsPath%",
        StaleTime = null,
        LastFetchedAt = null,
    };

    private readonly ILogger<PluginPackageManager> _logger = logger;

    private readonly IPluginManager _pluginManager = pluginManager;

    private readonly IApplicationPaths _applicationPaths = applicationPaths;

    #region Settings

    /// <summary>
    ///   Whether automatic repository syncing is enabled.
    /// </summary>
    public bool IsAutoSyncEnabled
    {
        get => configurationProvider.Load().Plugins.Updates.IsAutoSyncEnabled;
        set
        {
            var config = configurationProvider.Load(copy: true);
            config.Plugins.Updates.IsAutoSyncEnabled = value;
            configurationProvider.Save(config);
        }
    }

    /// <summary>
    ///   Whether automatic plugin upgrades are enabled.
    /// </summary>
    public bool IsAutoUpgradeEnabled
    {
        get => configurationProvider.Load().Plugins.Updates.IsAutoUpgradeEnabled;
        set
        {
            var config = configurationProvider.Load(copy: true);
            config.Plugins.Updates.IsAutoUpgradeEnabled = value;
            configurationProvider.Save(config);
        }
    }

    /// <summary>
    ///   Default time before a repository is considered stale (default: 12 hours).
    /// </summary>
    public TimeSpan DefaultRepositoryStaleTime
    {
        get => configurationProvider.Load().Plugins.Updates.DefaultRepositoryStaleTime;
        set
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(InactivePluginVersionRetention), "Stale time cannot be lower than zero.");
            var config = configurationProvider.Load(copy: true);
            config.Plugins.Updates.DefaultRepositoryStaleTime = value;
            configurationProvider.Save(config);
        }
    }

    /// <summary>
    ///   Time to retain old plugin versions before auto-cleanup.
    /// </summary>
    public TimeSpan InactivePluginVersionRetention
    {
        get => configurationProvider.Load().Plugins.Updates.InactivePluginVersionRetention;
        set
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(InactivePluginVersionRetention), "Retention cannot be lower than zero.");
            var config = configurationProvider.Load(copy: true);
            config.Plugins.Updates.InactivePluginVersionRetention = value;
            configurationProvider.Save(config);
        }
    }

    #endregion

    #region Package Installation

    private readonly List<PackageInfo> _installedPackages = [];

    public event EventHandler<PackageInstallationStartedEventArgs>? PackageInstallationStarted;

    public event EventHandler<PackageInstallationCompletedEventArgs>? PackageInstallationCompleted;

    public event EventHandler<PackageInstallationFailedEventArgs>? PackageInstallationFailed;

    /// <summary>
    ///   Gets all packages installed since the server startup till now.
    /// </summary>
    /// <returns>
    ///   A list of the packages installed since the server startup till now.
    /// </returns>
    public IReadOnlyList<PackageInfo> GetInstalledPackages() => _installedPackages;

    /// <summary>
    ///   Gets all locally installed packages as <see cref="PackageInfo"/>.
    /// </summary>
    /// <returns>
    ///   List of all packages installed locally in the system.
    /// </returns>
    public IReadOnlyList<PackageInfo> GetLocalPackages()
    {
        var repositories = ListPackageRepositories();
        var manifests = GetAvailablePackageManifests(allowSync: false).Result;
        var localPackages = new List<PackageInfo>();
        foreach (var groupedPlugins in _pluginManager.GetPluginInfos().GroupBy(p => p.ID))
        {
            var manifest = manifests.FirstOrDefault(m => m.PackageID == groupedPlugins.Key);
            foreach (var plugin in groupedPlugins)
            {
                var release = manifest?.Releases
                    .FirstOrDefault(r =>
                        // If the source revision is set then it should match, otherwise fall back to version matching.
                        plugin.Version.SourceRevision is { Length: > 0 } a
                            ? (
                                r.SourceRevision is { Length: > 0 } b && string.Equals(a, b) &&
                                r.Archives.Any(archive =>
                                    plugin.Version.AbstractionVersion == archive.AbstractionVersion &&
                                    archive.RuntimeIdentifier == plugin.Version.RuntimeIdentifier
                                )
                            )
                            : (
                                r.Version == plugin.Version.Version &&
                                (
                                    r.Archives.Any(archive =>
                                        plugin.Version.AbstractionVersion == archive.AbstractionVersion &&
                                        archive.RuntimeIdentifier == plugin.Version.RuntimeIdentifier
                                    ) ||
                                    r.Archives.Any(archive =>
                                        plugin.Version.AbstractionVersion == archive.AbstractionVersion &&
                                        archive.RuntimeIdentifier == PluginManager.AnyRuntimeIdentifier
                                    )
                                )
                            )
                    );
                if (release is not null)
                {
                    var archive = release.Archives.First(archive =>
                        archive.RuntimeIdentifier == plugin.Version.RuntimeIdentifier ||
                        archive.RuntimeIdentifier is PluginManager.AnyRuntimeIdentifier
                    );
                    var repository = repositories.FirstOrDefault(r => r.ID == release.RepositoryID);
                    localPackages.Add(new PackageInfo
                    {
                        Manifest = manifest!,
                        Repository = repository,
                        Archive = archive,
                        Plugin = plugin,
                        Release = release,
                    });
                }
                // Fake package if no matching release is found.
                else
                {
                    release = new()
                    {
                        Version = plugin.Version.Version,
                        RepositoryID = _localRepositoryInfo.ID,
                        ReleasedAt = plugin.InstalledAt,
                        Channel = plugin.Version.Channel,
                        Tag = plugin.Version.ReleaseTag,
                        SourceRevision = plugin.Version.SourceRevision,
                        ReleaseNotes = null,
                        Archives = [
                            new()
                            {
                                RuntimeIdentifier = plugin.Version.RuntimeIdentifier,
                                AbstractionVersion = plugin.Version.AbstractionVersion,
                                ArchiveUrl = "",
                                ArchiveChecksum = "",
                            },
                        ],
                    };
                    manifest = new()
                    {
                        PackageID = plugin.ID,
                        Name = plugin.Name,
                        Overview = plugin.Description,
                        Authors = plugin.Authors ?? "Unknown",
                        RepositoryUrl = plugin.RepositoryUrl,
                        HomepageUrl = plugin.HomepageUrl,
                        Tags = plugin.Tags,
                        Thumbnail = plugin.Thumbnail,
                        Releases = [release],
                        LastFetchedAt = plugin.InstalledAt,
                    };
                    localPackages.Add(new()
                    {
                        Manifest = manifest,
                        Repository = _localRepositoryInfo,
                        Archive = release.Archives[0]!,
                        Plugin = plugin,
                        Release = release,
                    });
                }
            }
        }
        return localPackages;
    }

    /// <summary>
    ///   Installs or upgrades a plugin. Returns installed
    ///   <see cref="LocalPluginInfo"/> or <see langword="null"/> on failure.
    /// </summary>
    /// <param name="packageInfo">
    ///   The package and version to install.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. Cancellation token.
    /// </param>
    /// <returns>
    ///   The installed see, or <see langword="null"/> if installation failed.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///   Thrown if the installation was cancelled by the
    ///   <paramref name="cancellationToken"/>.
    /// </exception>
    /// <exception cref="InvalidChecksumException">
    ///   Thrown when package checksum verification fails.
    /// </exception>
    public async Task<LocalPluginInfo?> InstallPackage(PackageInfo packageInfo, CancellationToken cancellationToken = default)
    {
        var eventArgs = new PackageInstallationStartedEventArgs() { Package = packageInfo, StartedAt = DateTime.UtcNow };

        _logger.LogInformation(
            "Installing package {PackageName}. (Id={PackageId},Version={PackageVersion},RuntimeId={PackageRuntimeId})",
            packageInfo.Manifest.Name,
            packageInfo.Manifest.PackageID,
            packageInfo.Release.Version,
            packageInfo.Archive.RuntimeIdentifier
        );

        try
        {
            PackageInstallationStarted?.Invoke(null, eventArgs);
        }
        catch (OperationCanceledException)
        {
            eventArgs.Cancel = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Installation of package {PackageName} failed during the started event. (Id={PackageId},Version={PackageVersion},Runtime={PackageRuntimeId})",
                packageInfo.Manifest.Name,
                packageInfo.Manifest.PackageID,
                packageInfo.Release.Version,
                packageInfo.Archive.RuntimeIdentifier
            );

            _ = Task.Run(() => PackageInstallationFailed?.Invoke(null, new()
            {
                Package = packageInfo,
                StartedAt = eventArgs.StartedAt,
                FailedAt = DateTime.UtcNow,
                Reason = "Package installation failed during the started event.",
                Exception = ex,
            }));

            throw;
        }

        if (eventArgs.Cancel || cancellationToken.IsCancellationRequested)
        {
            var ex = new OperationCanceledException("Installation was cancelled by a started event consumer before starting.");
            _logger.LogWarning(
                ex,
                "Installation of package {PackageName} was cancelled before starting. (Id={PackageId},Version={PackageVersion},RuntimeId={PackageRuntimeId})",
                packageInfo.Manifest.Name,
                packageInfo.Manifest.PackageID,
                packageInfo.Release.Version,
                packageInfo.Archive.RuntimeIdentifier
            );

            _ = Task.Run(() => PackageInstallationFailed?.Invoke(null, new()
            {
                Package = packageInfo,
                StartedAt = eventArgs.StartedAt,
                FailedAt = DateTime.UtcNow,
                Reason = "Package installation was cancelled by a started event consumer before starting.",
                Exception = ex,
            }));

            throw ex;
        }

        var extractPath = Path.Join(_applicationPaths.PluginsPath, $"{packageInfo.Manifest.Name}-{packageInfo.Release.Version}-{packageInfo.Archive.RuntimeIdentifier}");
        var zipPath = extractPath + ".zip";
        try
        {
            // Cleanup old files before starting.
            if (File.Exists(zipPath))
                File.Delete(zipPath);
            if (!Directory.Exists(extractPath) || !Directory.EnumerateFileSystemEntries(extractPath).Any())
            {
                if (Directory.Exists(extractPath))
                    Directory.Delete(extractPath, true);
                await DownloadAndVerifyArchiveAsync(packageInfo.Archive.ArchiveUrl, zipPath, packageInfo.Archive.ArchiveChecksum, cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException("Installation cancelled during download or verification.");

                _logger.LogInformation($"Extracting plugin to {extractPath}");

                ZipFile.ExtractToDirectory(zipPath, extractPath);
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException("Installation cancelled during archive extraction.");
            }

            var pluginInfo = _pluginManager.LoadFromPath(extractPath);
            if (pluginInfo is null)
            {
                _logger.LogWarning("Plugin info was null after loading from path.");

                return null;
            }

            _installedPackages.Add(new PackageInfo
            {
                Manifest = packageInfo.Manifest,
                Repository = packageInfo.Repository,
                Archive = packageInfo.Archive,
                Plugin = pluginInfo,
                Release = packageInfo.Release,
            });

            _ = Task.Run(() => PackageInstallationCompleted?.Invoke(null, new()
            {
                Package = packageInfo,
                StartedAt = eventArgs.StartedAt,
                CompletedAt = DateTime.UtcNow,
                Plugin = pluginInfo,
            }));

            return pluginInfo;
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                ex,
                "Installation of package {PackageName} was cancelled during installation. (Id={PackageId},Version={PackageVersion},RuntimeId={PackageRuntimeId})",
                packageInfo.Manifest.Name,
                packageInfo.Manifest.PackageID,
                packageInfo.Release.Version,
                packageInfo.Archive.RuntimeIdentifier
            );

            _ = Task.Run(() => PackageInstallationFailed?.Invoke(null, new()
            {
                Package = packageInfo,
                StartedAt = eventArgs.StartedAt,
                FailedAt = DateTime.UtcNow,
                Reason = "Package installation was cancelled during installation.",
                Exception = ex,
            }));
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to install package {PackageName} due to an exception occurring during installation. (Id={PackageId},Version={PackageVersion},RuntimeId={PackageRuntimeId})",
                packageInfo.Manifest.Name,
                packageInfo.Manifest.PackageID,
                packageInfo.Release.Version,
                packageInfo.Archive.RuntimeIdentifier
            );
            try
            {
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
                if (Directory.Exists(extractPath))
                    Directory.Delete(extractPath, true);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "Failed to clean up extracted files after cancellation.");
            }
            _ = Task.Run(() => PackageInstallationFailed?.Invoke(null, new()
            {
                Package = packageInfo,
                StartedAt = eventArgs.StartedAt,
                FailedAt = DateTime.UtcNow,
                Reason = "Failed to install package due to an exception occurring.",
                Exception = ex,
            }));
            throw;
        }
    }

    private async Task DownloadAndVerifyArchiveAsync(string downloadUrl, string destinationPath, string expectedChecksum, CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient("PluginPackages");

        using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException("Download cancelled during header check.");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var fileStream = File.Create(destinationPath);

        var sha256 = SHA256.Create();
        var hashBytes = new byte[1024 * 1024];
        int bytesRead;
        long totalBytesRead = 0;

        while ((bytesRead = await stream.ReadAsync(hashBytes, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Download cancelled during data read.");

            sha256.TransformBlock(hashBytes, 0, bytesRead, hashBytes, 0);
            await fileStream.WriteAsync(hashBytes.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            totalBytesRead += bytesRead;
        }

        sha256.TransformFinalBlock([], 0, 0);

        _logger.LogDebug($"Downloaded package with {totalBytesRead} bytes");

        var actualHash = Convert.ToHexString(sha256.Hash!);

        if (!string.Equals(actualHash, expectedChecksum, StringComparison.InvariantCultureIgnoreCase))
        {
            try { File.Delete(destinationPath); } catch { /* Ignore cleanup errors */ }
            throw new InvalidChecksumException(expectedChecksum, actualHash);
        }

        _logger.LogDebug($"Package downloaded and verified successfully to {destinationPath}. Hash: {actualHash}");
    }

    #endregion

    #region Repository Management Events

    /// <summary>
    ///   Dispatched right before a repository sync operation begins.
    /// </summary>
    public event EventHandler<RepositorySyncStartedEventArgs>? RepositorySyncStarted;

    /// <summary>
    ///   Dispatched when a repository sync operation fails.
    /// </summary>
    public event EventHandler<RepositorySyncFailedEventArgs>? RepositorySyncFailed;

    /// <summary>
    ///   Dispatched when a repository sync operation completes.
    /// </summary>
    public event EventHandler<RepositorySyncCompletedEventArgs>? RepositorySyncCompleted;

    #endregion

    #region Package Discovery & Listing

    /// <summary>
    ///   Gets all available packages across all synced repositories.
    /// </summary>
    /// <param name="allowSync">
    ///   Optional. Whether to sync repositories before retrieving packages.
    /// </param>
    /// <param name="forceSyncNow">
    ///   Optional. Whether to forcefully sync the repositories.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. Cancellation token.
    /// </param>
    /// <returns>
    ///   List of available packages.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///   Thrown if the fetch was cancelled by the
    ///   <paramref name="cancellationToken"/>.
    /// </exception>
    public async Task<IReadOnlyList<PackageManifestInfo>> GetAvailablePackageManifests(
        bool allowSync = true,
        bool forceSyncNow = false,
        CancellationToken cancellationToken = default
    )
    {
        var now = DateTime.UtcNow;
        var manifestInfoDict = new Dictionary<Guid, List<PackageManifestInfo>>();
        foreach (var repositoryInfo in ListPackageRepositories())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (allowSync && (forceSyncNow || now - repositoryInfo.LastFetchedAt > (repositoryInfo.StaleTime ?? DefaultRepositoryStaleTime)))
                await SyncPackageRepository(repositoryInfo, forceSyncNow, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var localManifest in GetLocalManifestsForRepository(repositoryInfo))
            {
                if (!manifestInfoDict.TryGetValue(localManifest.PackageID, out var existingList))
                    manifestInfoDict[localManifest.PackageID] = existingList = [];
                existingList.Add(localManifest);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return manifestInfoDict.Values
            .Select(list => list.OrderByDescending(m => m.LastFetchedAt).ToList())
            .Select(list => new PackageManifestInfo()
            {
                PackageID = list[0].PackageID,
                Overview = list[0].Overview,
                Authors = list[0].Authors,
                RepositoryUrl = list[0].RepositoryUrl,
                HomepageUrl = list[0].HomepageUrl,
                Thumbnail = list.FirstOrDefault(m => m.Thumbnail is not null)?.Thumbnail,
                Name = list[0].Name,
                Releases = list.SelectMany(m => m.Releases)
                    .OrderBy(m => m.Version, new SemverVersionComparer())
                    .ThenByDescending(m => m.ReleasedAt)
                    .ToList(),
                Tags = list.SelectMany(m => m.Tags)
                    .Distinct(StringComparer.InvariantCultureIgnoreCase)
                    .Order()
                    .ToList(),
                LastFetchedAt = list[0].LastFetchedAt,
            })
            .OrderBy(m => m.Name)
            .ThenBy(m => m.PackageID)
            .ToList();
    }

    private IReadOnlyList<PackageManifestInfo> GetLocalManifestsForRepository(PackageRepositoryInfo repositoryInfo)
    {
        var repoPath = Path.Join(_applicationPaths.PluginsPath, Repositories, repositoryInfo.ID.ToString(), ManifestFile);
        if (!File.Exists(repoPath))
            return [];

        var manifest = File.ReadAllText(repoPath);
        var manifestList = JsonConvert.DeserializeObject<List<PackageManifestInfo>>(manifest)!;
        return manifestList;
    }

    /// <summary>
    ///   Filter the package manifests to filter.
    /// </summary>
    /// <param name="packageManifests">
    ///   The package manifests to filter.
    /// </param>
    /// <param name="onlyCompatible">
    ///   Optional. Whether to return only ABI- and runtime-compatible packages.
    ///   Defaults to <see langword="true"/>.
    /// </param>
    /// <param name="onlyLatest">
    ///   Optional. Whether to return only the latest version of each package.
    /// </param>
    /// <returns>
    ///   The filtered <see cref="PackageInfo"/>s.
    /// </returns>
    public IEnumerable<PackageInfo> FilterPackageManifests(
        IEnumerable<PackageManifestInfo> packageManifests,
        bool onlyCompatible = true,
        bool onlyLatest = true
    )
    {
        var repositories = ListPackageRepositories();
        foreach (var manifest in packageManifests)
        {
            var manifestPlugin = _pluginManager.GetPluginInfo(manifest.PackageID);
            foreach (var release in manifest.Releases)
            {
                var plugin = _pluginManager.GetPluginInfo(manifest.PackageID, release.Version);
                var repository = repositories.FirstOrDefault(r => r.ID == release.RepositoryID);
                foreach (var archive in release.Archives)
                {
                    if (onlyCompatible && archive.RuntimeIdentifier is not PluginManager.AnyRuntimeIdentifier && archive.RuntimeIdentifier != _pluginManager.RuntimeIdentifier)
                        continue;

                    yield return new()
                    {
                        Manifest = manifest,
                        Repository = repository,
                        Archive = archive,
                        Plugin = plugin,
                        Release = release,
                    };
                }

                if (onlyLatest)
                    break;
            }
        }
    }

    #endregion

    #region Repository Management

    /// <inheritdoc/>
    public IReadOnlyList<PackageRepositoryInfo> ListPackageRepositories()
    {
        var list = new List<PackageRepositoryInfo>() { _localRepositoryInfo };
        lock (_logger)
        {
            var repoDirPath = Path.Join(_applicationPaths.PluginsPath, Repositories);
            var files = Directory.Exists(repoDirPath)
                ? Directory.EnumerateDirectories(repoDirPath, "*", SearchOption.TopDirectoryOnly)
                : [];
            foreach (var dirPath in files)
            {
                try
                {
                    var filePath = Path.Join(dirPath, RepoInfoFile);
                    var fileContent = File.ReadAllText(filePath, Encoding.UTF8);
                    var repo = JsonConvert.DeserializeObject<PackageRepositoryInfo>(fileContent);
                    if (repo is not null)
                        list.Add(repo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to read repository definition!");
                }
            }
        }
        return list;
    }

    /// <inheritdoc/>
    public async Task<PackageRepositoryInfo> AddPackageRepository(string name, string url, TimeSpan? staleTime = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("Url must be absolute!", nameof(url));
        if (uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Url must use the http:// or https:// schema!", nameof(url));
        if (staleTime.HasValue && staleTime.Value < TimeSpan.Zero)
            throw new ArgumentException("Stale time cannot be less than zero.", nameof(staleTime));

        name = name.Trim();
        url = uri.ToString();
        var existingRepositories = ListPackageRepositories();
        var repositoryInfo = new PackageRepositoryInfo()
        {
            ID = UuidUtility.GetV5(url),
            Name = name,
            Url = url,
            StaleTime = staleTime ?? DefaultRepositoryStaleTime,
            LastFetchedAt = null,
        };

        lock (_logger)
        {
            if (existingRepositories.Any(r => string.Equals(r.Name, name, StringComparison.InvariantCultureIgnoreCase)))
                throw new ArgumentException("An existing repository with the given name already exists.", nameof(name));
            if (existingRepositories.Any(r => string.Equals(r.Url, url)))
                throw new ArgumentException("An existing repository with the given URL already exists.", nameof(url));
            var repoPath = Path.Join(_applicationPaths.PluginsPath, Repositories, repositoryInfo.ID.ToString(), RepoInfoFile);
            var repoJson = JsonConvert.SerializeObject(repositoryInfo);

            Directory.CreateDirectory(Path.GetDirectoryName(repoPath)!);
            File.WriteAllText(repoPath, repoJson);

            _logger.LogInformation($"Repository '{name}' created.");
        }

        return await SyncPackageRepository(repositoryInfo, forceSync: true, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<bool> RemovePackageRepository(PackageRepositoryInfo repositoryInfo, CancellationToken cancellationToken = default)
    {
        if (repositoryInfo.ID == _localRepositoryInfo.ID)
            return Task.FromResult(false);

        var repoPath = Path.Join(_applicationPaths.PluginsPath, Repositories, repositoryInfo.ID.ToString());
        var exists = Directory.Exists(repoPath);
        lock (_logger)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (exists)
            {
                Directory.Delete(repoPath, true);
                _logger.LogInformation("Removed repository {RepositoryName} (Id={RepositoryId})", repositoryInfo.Name, repositoryInfo.ID);
            }
        }
        return Task.FromResult(exists);
    }

    /// <inheritdoc/>
    public async Task<PackageRepositoryInfo> SyncPackageRepository(PackageRepositoryInfo repositoryInfo, bool forceSync = false, CancellationToken cancellationToken = default)
    {
        var eventArgs = new RepositorySyncStartedEventArgs()
        {
            Repository = repositoryInfo,
            ForceSync = forceSync,
            StaleTime = repositoryInfo.StaleTime ?? DefaultRepositoryStaleTime,
            StartedAt = DateTime.UtcNow,
        };

        RepositorySyncStarted?.Invoke(null, eventArgs);

        if (eventArgs.Cancel || (!eventArgs.ForceSync && DateTime.UtcNow - repositoryInfo.LastFetchedAt < eventArgs.StaleTime))
        {
            _ = Task.Run(() => RepositorySyncCompleted?.Invoke(null, new RepositorySyncCompletedEventArgs()
            {
                Repository = repositoryInfo,
                StartedAt = eventArgs.StartedAt,
                CompletedAt = eventArgs.StartedAt,
            }));

            return repositoryInfo;
        }

        _logger.LogInformation("Syncing repository '{RepositoryName}'", repositoryInfo.Name);

        var originalFetchedAt = repositoryInfo.LastFetchedAt;
        var repoDirPath = Path.Join(_applicationPaths.PluginsPath, Repositories, repositoryInfo.ID.ToString());
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var httpClient = httpClientFactory.CreateClient("PluginPackages");
            var remoteManifests = await GetRemoteManifestAsync(httpClient, repositoryInfo.Url, allowArray: true, cancellationToken: cancellationToken).ConfigureAwait(false);
            var lastFetchedAt = DateTime.UtcNow;
            var manifests = new List<PackageManifestInfo>();
            foreach (var (remoteManifest, fetchedAt) in remoteManifests)
            {
                var manifest = await ConvertRemoteToLocalAsync(remoteManifest, repositoryInfo.ID, fetchedAt, cancellationToken).ConfigureAwait(false);
                if (manifest is not null)
                    manifests.Add(manifest);
            }

            Directory.CreateDirectory(repoDirPath);

            var manifestsFilePath = Path.Join(repoDirPath, ManifestFile);
            File.WriteAllText(manifestsFilePath, JsonConvert.SerializeObject(manifests, Formatting.Indented));

            _logger.LogInformation($"Synced {manifests.Count} package manifests from repository '{repositoryInfo.Name}'.");

            repositoryInfo.LastFetchedAt = lastFetchedAt;

            var updatedRepoPath = Path.Join(repoDirPath, RepoInfoFile);
            File.WriteAllText(updatedRepoPath, JsonConvert.SerializeObject(repositoryInfo));

            _logger.LogInformation($"Repository sync completed for '{repositoryInfo.Name}'. Fetched at: {repositoryInfo.LastFetchedAt}");

            _ = Task.Run(() => RepositorySyncCompleted?.Invoke(null, new RepositorySyncCompletedEventArgs()
            {
                Repository = repositoryInfo,
                StartedAt = eventArgs.StartedAt,
                CompletedAt = eventArgs.StartedAt,
            }));
        }
        catch (Exception ex)
        {
            repositoryInfo.LastFetchedAt = originalFetchedAt;

            _logger.LogError(ex, "Failed to sync repository '{RepositoryName}' ({RepositoryId}).", repositoryInfo.Name, repositoryInfo.ID);

            _ = Task.Run(() => RepositorySyncFailed?.Invoke(null, new()
            {
                Repository = repositoryInfo,
                StartedAt = eventArgs.StartedAt,
                Reason = "Repository sync was cancelled",
                Exception = ex,
                FailedAt = DateTime.UtcNow,
            }));
        }

        return repositoryInfo;
    }

    private async Task<IReadOnlyList<(RemotePackageManifestInfo Manifest, DateTime fetchedAt)>> GetRemoteManifestAsync(HttpClient httpClient, string manifestUrl, bool allowArray, CancellationToken cancellationToken)
    {
        var (document, lastFetchedAt) = await FetchManifestAsync(httpClient, manifestUrl, cancellationToken).ConfigureAwait(false);
        var manifests = new List<(RemotePackageManifestInfo Manifest, DateTime fetchedAt)>();
        try
        {
            var rootElement = document.RootElement;
            if (rootElement.ValueKind == JsonValueKind.Array)
            {
                if (!allowArray)
                    throw new InvalidOperationException("A referenced single-repository manifest cannot contain multiple manifests.");

                foreach (var element in rootElement.EnumerateArray())
                {
                    try
                    {
                        var manifest = JsonConvert.DeserializeObject<RemotePackageManifestInfo>(element.GetRawText());
                        if (manifest is { IsReference: true })
                        {
                            var subManifests = await GetRemoteManifestAsync(httpClient, manifest.ManifestUrl, allowArray: false, cancellationToken: cancellationToken).ConfigureAwait(false);
                            manifests.AddRange(subManifests);
                        }
                        else if (manifest is not null)
                            manifests.Add((manifest, lastFetchedAt));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse package manifest from repository.");
                    }
                }
            }
            else if (rootElement.ValueKind == JsonValueKind.Object)
            {
                var manifest = JsonConvert.DeserializeObject<RemotePackageManifestInfo>(rootElement.GetRawText())
                    ?? throw new InvalidOperationException("Failed to parse repository manifest as object.");
                if (manifest.IsReference)
                    throw new InvalidOperationException("A single-repository manifest cannot reference another manifest.");
                manifests.Add((manifest, lastFetchedAt));
            }
            else
                throw new InvalidOperationException($"Unexpected JSON structure in repository manifest: {rootElement.ValueKind}.");
        }
        finally
        {
            document.Dispose();
        }

        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException("Repository sync cancelled during manifest processing.");

        return manifests;
    }

    private async Task<(JsonDocument Document, DateTime LastFetchedAt)> FetchManifestAsync(HttpClient httpClient, string manifestUrl, CancellationToken cancellationToken)
    {
        var lastFetchedAt = DateTime.UtcNow;
        var request = new HttpRequestMessage(HttpMethod.Get, manifestUrl);
        var manifestId = UuidUtility.GetV5(manifestUrl);
        var jsonPath = Path.Join(_applicationPaths.PluginsPath, Repositories, "cache", manifestId + ".json");
        var etagPath = Path.ChangeExtension(jsonPath, ".etag");
        var cacheExists = File.Exists(jsonPath);
        if (cacheExists)
        {
            lastFetchedAt = File.GetLastWriteTimeUtc(jsonPath);
            request.Headers.IfModifiedSince = lastFetchedAt;
            if (File.Exists(etagPath) && File.ReadAllText(etagPath, Encoding.UTF8) is { Length: > 0 } cachedEtag)
                request.Headers.IfNoneMatch.ParseAdd(cachedEtag);
        }

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (cacheExists && response.StatusCode is HttpStatusCode.NotModified)
        {
            var jsonCachedDoc = await JsonDocument.ParseAsync(File.OpenRead(jsonPath), default, cancellationToken).ConfigureAwait(false);
            return (jsonCachedDoc, lastFetchedAt);
        }

        response.EnsureSuccessStatusCode();
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is not ("application/json" or "text/plain"))
            throw new InvalidOperationException($"Invalid content type: \"{contentType}\". Expected \"application/json.\" or \"text/plain\" or \"text/json\"");

        cancellationToken.ThrowIfCancellationRequested();

        var jsonDoc = await JsonDocument.ParseAsync(response.Content.ReadAsStream(cancellationToken), default, cancellationToken).ConfigureAwait(false);
        return (jsonDoc, lastFetchedAt);
    }

    private async Task<PackageManifestInfo?> ConvertRemoteToLocalAsync(RemotePackageManifestInfo manifestInfo, Guid repositoryId, DateTime lastFetchedAt, CancellationToken cancellationToken)
    {
        if (manifestInfo.IsReference)
            return null;
        var thumbnail = await DownloadAndCacheThumbnailAsync(manifestInfo.ImageUrl, manifestInfo.PackageID, cancellationToken).ConfigureAwait(false);

        return new PackageManifestInfo
        {
            PackageID = manifestInfo.PackageID,
            Name = manifestInfo.Name,
            Overview = manifestInfo.Overview,
            Authors = manifestInfo.Authors,
            RepositoryUrl = manifestInfo.RepositoryUrl,
            HomepageUrl = manifestInfo.HomepageUrl,
            Tags = manifestInfo.Tags
                .Select(tag => tag.ToLowerInvariant())
                .Distinct()
                .ToArray(),
            Thumbnail = thumbnail,
            Releases = manifestInfo.Releases
                .Select(remoteRelease => new PackageReleaseInfo
                {
                    RepositoryID = repositoryId,
                    Version = remoteRelease.Version,
                    Tag = remoteRelease.Tag,
                    SourceRevision = remoteRelease.SourceRevision,
                    ReleasedAt = remoteRelease.ReleasedAt,
                    Channel = remoteRelease.Channel,
                    ReleaseNotes = remoteRelease.ReleaseNotes,
                    Archives = remoteRelease.Archives
                        .Select(remoteArchive => new PackageArchiveInfo
                        {
                            RuntimeIdentifier = remoteArchive.RuntimeIdentifier,
                            AbstractionVersion = remoteArchive.AbstractionVersion,
                            ArchiveUrl = remoteArchive.ArchiveUrl,
                            ArchiveChecksum = remoteArchive.ArchiveChecksum,
                        })
                        .ToList(),
                })
                .ToList(),
            LastFetchedAt = lastFetchedAt,
        };
    }

    private async Task<PackageThumbnailInfo?> DownloadAndCacheThumbnailAsync(string? imageUrl, Guid packageId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(imageUrl))
            return null;

        var imageId = UuidUtility.GetV5(imageUrl);
        var imagesDir = Path.Join(_applicationPaths.PluginsPath, Repositories, "images");
        var existingFiles = Directory.Exists(imagesDir)
            ? Directory.EnumerateFiles(imagesDir, $"{imageId}.*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false }).ToList()
            : [];
        if (existingFiles.Count > 0)
        {
            var existingFile = existingFiles[0];
            try
            {
                var imageInfo = new ImageMagick.MagickImageInfo(existingFile);
                var mime = PluginManager.GetMimeFromFormat(imageInfo);
                if (mime is not null)
                    return new PackageThumbnailInfo
                    {
                        MimeType = mime,
                        Width = imageInfo.Width,
                        Height = imageInfo.Height,
                        FilePath = existingFile
                            .Replace(_applicationPaths.PluginsPath, "%PluginsPath%")
                            .Replace(_applicationPaths.ApplicationPath, "%ApplicationPaths%"),
                    };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read cached image for package {PackageId}.", packageId);
            }
            return null;
        }

        try
        {
            var httpClient = httpClientFactory.CreateClient("PluginPackages");
            using var response = await httpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType;
            var extension = contentType is not null ? MimeMapping.MimeUtility.GetExtensions(contentType)?.FirstOrDefault() : null;
            if (extension is not null)
            {
                Directory.CreateDirectory(imagesDir);
                var imagePath = Path.Join(imagesDir, $"{imageId}{extension}");
                await using var fileStream = File.Create(imagePath);
                await response.Content.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);

                var imageInfo = new ImageMagick.MagickImageInfo(imagePath);
                var mime = PluginManager.GetMimeFromFormat(imageInfo);
                if (mime is not null)
                {
                    return new PackageThumbnailInfo
                    {
                        MimeType = mime,
                        Width = imageInfo.Width,
                        Height = imageInfo.Height,
                        FilePath = imagePath
                            .Replace(_applicationPaths.PluginsPath, "%PluginsPath%")
                            .Replace(_applicationPaths.ApplicationPath, "%ApplicationPaths%"),
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download image for package {PackageId} from {ImageUrl}.", packageId, imageUrl);
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task SyncAllPackageRepositories(bool forceSync = false, CancellationToken cancellationToken = default)
    {
        foreach (var repositoryInfo in ListPackageRepositories())
        {
            try
            {
                await SyncPackageRepository(repositoryInfo, forceSync, cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested)
                    break;
            }
            catch { }
        }
    }

    #endregion

    #region Update Checking

    /// <inheritdoc/>
    public async Task CheckForUpdates(bool? forceSync = null, bool? performUpgrade = null, CancellationToken cancellationToken = default)
    {
        var settings = configurationProvider.Load();
        var shouldForceSync = forceSync ?? false;

        // Check if auto-sync is enabled (must be enabled unless forcing)
        if (!settings.Plugins.Updates.IsAutoSyncEnabled && !shouldForceSync)
            return;

        // Check frequency setting (skip schedule check if forcing)
        if (!shouldForceSync)
        {
            if (settings.Plugins.Updates.AutoUpdateFrequency is ScheduledUpdateFrequency.Never)
                return;

            var schedule = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.PluginUpdates);
            if (schedule != null)
            {
                var freqHours = Utils.GetScheduledHours(settings.Plugins.Updates.AutoUpdateFrequency);
                var tsLastRun = DateTime.Now - schedule.LastUpdate;
                if (tsLastRun.TotalHours < freqHours)
                    return;
            }
        }

        // Update schedule tracking
        var scheduleRecord = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.PluginUpdates)
            ?? new ScheduledUpdate { UpdateType = (int)ScheduledUpdateType.PluginUpdates, UpdateDetails = string.Empty };
        scheduleRecord.LastUpdate = DateTime.Now;
        RepoFactory.ScheduledUpdate.Save(scheduleRecord);

        _logger.LogInformation("Checking for plugin updates...");

        // Sync repositories
        try
        {
            await SyncAllPackageRepositories(forceSync: shouldForceSync, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync plugin repositories");
            return;
        }

        // Resolve performUpgrade - use settings default if not specified
        var shouldUpgrade = performUpgrade ?? settings.Plugins.Updates.IsAutoUpgradeEnabled;
        if (!shouldUpgrade)
            return;

        // Auto-upgrade logic
        var manifests = await GetAvailablePackageManifests(allowSync: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        var localPackages = GetLocalPackages();
        var availablePackages = FilterPackageManifests(manifests, onlyCompatible: true, onlyLatest: true);

        var comparer = new SemverVersionComparer();
        foreach (var availablePackage in availablePackages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var localPackage = localPackages.FirstOrDefault(p => p.Manifest.PackageID == availablePackage.Manifest.PackageID && p.Plugin is { IsActive: true, IsEnabled: true });
            if (localPackage == null)
                continue;

            // Compare versions - upgrade if newer version available
            if (comparer.Compare(availablePackage.Release.Version, localPackage.Release.Version) > 0)
            {
                _logger.LogInformation("Auto-upgrading plugin {PluginName} from {OldVersion} to {NewVersion}",
                    availablePackage.Manifest.Name,
                    localPackage.Release.Version,
                    availablePackage.Release.Version);

                try
                {
                    await InstallPackage(availablePackage, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to auto-upgrade plugin {PluginName}", availablePackage.Manifest.Name);
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task ScheduleCheckForUpdates(bool? forceSync = null, bool? performUpgrade = null, CancellationToken cancellationToken = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken).ConfigureAwait(false);
        await scheduler.StartJob<CheckPluginUpdatesJob>(c => (c.ForceSync, c.PerformUpgrade) = (forceSync, performUpgrade)).ConfigureAwait(false);
    }

    #endregion
}
