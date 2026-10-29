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
using ImageMagick;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Events;
using Shoko.Abstractions.Plugin.Exceptions;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.Utilities;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Plugin.Models;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Plugin;

/// <summary>
///   Responsible for package installation, upgrades, and repository management.
///   Built before the web host, so it can install upgrades before the plugin
///   scan.
/// </summary>
/// <param name="configurationProvider">The server settings.</param>
/// <param name="logger">The logger.</param>
/// <param name="httpClientFactory">Hands out the core's <c>"Default"</c> client.</param>
/// <param name="pluginManager">The plugin manager.</param>
/// <param name="applicationPaths">The server's folders.</param>
/// <param name="queueSchedulerAccessor">
///   Gets the queue scheduler, which only exists once the web host is built.
/// </param>
public partial class PluginPackageManager(
    ConfigurationProvider<ServerSettings> configurationProvider,
    ILogger<PluginPackageManager> logger,
    IHttpClientFactory httpClientFactory,
    IPluginManager pluginManager,
    IApplicationPaths applicationPaths,
    Func<IQueueScheduler> queueSchedulerAccessor
) : IPluginPackageManager
{
    internal const string Repositories = "repositories";

    private const string RepoInfoFile = "info.json";

    private const string ManifestFile = "manifest.json";

    private readonly ILogger<PluginPackageManager> _logger = logger;

    private readonly IPluginManager _pluginManager = pluginManager;

    /// <summary>
    ///   The plugin manager's reader for installed plugins, used before the
    ///   plugin scan.
    /// </summary>
    /// <exception cref="InvalidOperationException">The plugin manager cannot read installed plugins.</exception>
    private IInstalledPluginReader InstalledPluginReader
        => _pluginManager as IInstalledPluginReader
            ?? throw new InvalidOperationException("The plugin manager cannot read the installed plugins.");

    private readonly IApplicationPaths _applicationPaths = applicationPaths;

    #region Settings

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
    ///   Whether the newest compatible release of each installed plugin is
    ///   installed while the server starts, before the plugins load.
    /// </summary>
    public bool IsUpgradeOnStartEnabled
    {
        get => configurationProvider.Load().Plugins.Updates.IsUpgradeOnStartEnabled;
        set
        {
            var config = configurationProvider.Load(copy: true);
            config.Plugins.Updates.IsUpgradeOnStartEnabled = value;
            configurationProvider.Save(config);
        }
    }

    /// <inheritdoc/>
    public bool SkipBundledPluginUpgrades
    {
        get => configurationProvider.Load().Plugins.Updates.SkipBundledPluginUpgrades;
        set
        {
            var config = configurationProvider.Load(copy: true);
            config.Plugins.Updates.SkipBundledPluginUpgrades = value;
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
                throw new ArgumentOutOfRangeException(nameof(DefaultRepositoryStaleTime), "Stale time cannot be lower than zero.");
            var config = configurationProvider.Load(copy: true);
            config.Plugins.Updates.DefaultRepositoryStaleTime = value;
            configurationProvider.Save(config);
        }
    }

    /// <inheritdoc/>
    public TimeSpan RepositoryStaleTimeOnStart
    {
        get => configurationProvider.Load().Plugins.Updates.RepositoryStaleTimeOnStart;
        set
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(RepositoryStaleTimeOnStart), "Stale time cannot be lower than zero.");
            var config = configurationProvider.Load(copy: true);
            config.Plugins.Updates.RepositoryStaleTimeOnStart = value;
            configurationProvider.Save(config);
        }
    }

    /// <summary>
    ///   How long an older plugin version stays installed after a newer one
    ///   was installed, not after the restart that activated it, before
    ///   <see cref="PurgeInactivePluginVersions"/> marks it for removal.
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
                                        archive.RuntimeIdentifier == IPluginManager.AnyRuntimeIdentifier
                                    )
                                )
                            )
                    );
                if (release is not null)
                {
                    var archive = release.Archives.First(archive =>
                        archive.AbstractionVersion == plugin.Version.AbstractionVersion && (
                            archive.RuntimeIdentifier == plugin.Version.RuntimeIdentifier ||
                            archive.RuntimeIdentifier is IPluginManager.AnyRuntimeIdentifier
                        )
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
                        RepositoryID = null,
                        ReleasedAt = plugin.InstalledAt,
                        Channel = plugin.Version.Channel,
                        Tag = plugin.Version.ReleaseTag,
                        SourceRevision = plugin.Version.SourceRevision,
                        ReleaseNotes = null,
                        IsMeta = plugin.IsMeta,
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
                        IsMeta = plugin.IsMeta,
                        Thumbnail = plugin.Thumbnail,
                        Icon = plugin.Icon,
                        Releases = [release],
                        LastFetchedAt = plugin.InstalledAt,
                    };
                    localPackages.Add(new()
                    {
                        Manifest = manifest,
                        Repository = null,
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
    ///   <see cref="LocalPluginInfo"/> or <c>null</c> on failure.
    /// </summary>
    /// <param name="packageInfo">
    ///   The package and version to install.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. Cancellation token.
    /// </param>
    /// <returns>
    ///   The installed see, or <c>null</c> if installation failed.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///   Thrown if the installation was cancelled by the
    ///   <paramref name="cancellationToken"/>.
    /// </exception>
    /// <exception cref="InvalidChecksumException">
    ///   Thrown when package checksum verification fails.
    /// </exception>
    public Task<LocalPluginInfo?> InstallPackage(PackageInfo packageInfo, CancellationToken cancellationToken = default)
        => InstallPackage(packageInfo, InstallReason.Explicit, [], cancellationToken);

    /// <summary>
    ///   Why a package is installed, which decides whether the installed
    ///   version is marked as installed as a dependency.
    /// </summary>
    internal enum InstallReason : byte
    {
        /// <summary>
        ///   Asked for by the admin. Never marked.
        /// </summary>
        Explicit = 1,

        /// <summary>
        ///   Needed by another plugin. Marked unless the plugin is already
        ///   installed on purpose.
        /// </summary>
        Dependency = 2,

        /// <summary>
        ///   An upgrade of an installed plugin. Marked while every installed
        ///   version is marked.
        /// </summary>
        Upgrade = 3,
    }

    /// <summary>
    ///   Decides whether a newly installed version is marked as installed as a
    ///   dependency, from why it is installed and the plugin's other installed
    ///   versions.
    /// </summary>
    /// <param name="reason">Why the version is installed.</param>
    /// <param name="otherVersionsMarked">Whether each other installed version of the plugin is marked.</param>
    /// <returns><c>true</c> if the version is marked.</returns>
    internal static bool ShouldMarkAsDependency(InstallReason reason, IReadOnlyCollection<bool> otherVersionsMarked)
        => reason switch
        {
            InstallReason.Dependency => otherVersionsMarked.All(marked => marked),
            InstallReason.Upgrade => otherVersionsMarked.Count > 0 && otherVersionsMarked.All(marked => marked),
            _ => false,
        };

    /// <summary>
    ///   Installs a package for a reason, marking the installed version as
    ///   installed as a dependency when the reason calls for it.
    /// </summary>
    /// <param name="packageInfo">The package and version to install.</param>
    /// <param name="reason">Why it is installed.</param>
    /// <param name="requiredBy">The plugins needing it, written to its marker.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The installed version, or <c>null</c> if the installation failed.</returns>
    private async Task<LocalPluginInfo?> InstallPackage(PackageInfo packageInfo, InstallReason reason, IReadOnlyList<Guid> requiredBy, CancellationToken cancellationToken)
    {
        var eventArgs = new PackageInstallationStartedEventArgs() { Package = packageInfo, StartedAt = DateTime.UtcNow };

        _logger.LogInformation(
            "Installing package {PackageName}. (Id={PackageId}, Version={PackageVersion}, RuntimeId={PackageRuntimeId})",
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
                "Installation of package {PackageName} failed during the started event. (Id={PackageId}, Version={PackageVersion}, Runtime={PackageRuntimeId})",
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
                "Installation of package {PackageName} was cancelled before starting. (Id={PackageId}, Version={PackageVersion}, RuntimeId={PackageRuntimeId})",
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

        var extractPath = GetExtractPath(packageInfo);
        var zipPath = extractPath + ".zip";
        try
        {
            await DownloadAndExtractPackage(packageInfo, cancellationToken).ConfigureAwait(false);

            // Before the version is read, so a version installed as a dependency gets no enabled state of its own.
            var versions = _pluginManager.GetPluginInfos(packageInfo.Manifest.PackageID).Where(info => info.IsInstalled).ToList();
            var registered = versions.FirstOrDefault(info => string.Equals(info.ContainingDirectory, extractPath, StringComparison.Ordinal));
            var markAsDependency = ShouldMarkAsDependency(reason, versions.Where(info => info != registered).Select(info => info.IsDependency).ToList());
            if (registered is null)
            {
                if (markAsDependency)
                    PluginManager.WriteDependencyMarker(extractPath, string.Empty, requiredBy);
                else
                    PluginManager.RemoveDependencyMarker(extractPath, string.Empty);
            }

            var pluginInfo = _pluginManager.LoadFromPath(extractPath);
            if (pluginInfo is null)
            {
                _logger.LogWarning("Plugin info was null after loading from path.");

                return null;
            }

            // A version installed before keeps its folder, so its marker changes the way the admin's would.
            if (registered is not null && pluginInfo.IsDependency != markAsDependency)
                pluginInfo = markAsDependency ? _pluginManager.MarkAsDependency(pluginInfo) : _pluginManager.UnmarkAsDependency(pluginInfo);

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
                "Installation of package {PackageName} was cancelled during installation. (Id={PackageId}, Version={PackageVersion}, RuntimeId={PackageRuntimeId})",
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
                "Failed to install package {PackageName} due to an exception occurring during installation. (Id={PackageId}, Version={PackageVersion}, RuntimeId={PackageRuntimeId})",
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

    /// <inheritdoc/>
    public async Task<PackageInstallPlan> PlanInstall(PackageInfo packageInfo, bool includeOptional = false, CancellationToken cancellationToken = default)
    {
        var manifests = await GetAvailablePackageManifests(allowSync: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        return PackageInstallPlanner.Plan(
            packageInfo,
            GetInstalledPlugins(),
            manifests,
            ListPackageRepositories(),
            _pluginManager.IsAbiAndRuntimeCompatible,
            _pluginManager.RuntimeIdentifier,
            includeOptional
        );
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<LocalPluginInfo>> InstallPackages(PackageInstallPlan plan, CancellationToken cancellationToken = default)
        => InstallPackages(plan, InstallReason.Explicit, cancellationToken);

    /// <summary>
    ///   Carries out an install plan, all or nothing, installing the package
    ///   asked for for a reason.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="reason">Why the package asked for is installed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The installed plugin versions, in the order of the plan's steps.</returns>
    private async Task<IReadOnlyList<LocalPluginInfo>> InstallPackages(PackageInstallPlan plan, InstallReason reason, CancellationToken cancellationToken)
    {
        if (!plan.CanInstall)
            throw new InvalidOperationException($"Unable to install package {plan.Package.Manifest.Name}: {string.Join(" ", plan.Problems.Select(problem => $"{problem.Name ?? problem.PluginID.ToString()}: {problem.Reason}"))}");

        var installed = new List<LocalPluginInfo>(plan.Steps.Count);
        var added = new List<LocalPluginInfo>();
        try
        {
            foreach (var step in plan.Steps)
            {
                var extractPath = GetExtractPath(step.Package);
                var existed = _pluginManager.GetPluginInfos(step.Package.Manifest.PackageID)
                    .Any(info => info.IsInstalled && string.Equals(info.ContainingDirectory, extractPath, StringComparison.Ordinal));
                var stepReason = step.IsDependency ? InstallReason.Dependency : reason;
                var pluginInfo = await InstallPackage(step.Package, stepReason, step.RequiredBy, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"Failed to install package {step.Package.Manifest.Name} version {step.Package.Release.Version}.");
                installed.Add(pluginInfo);
                if (!existed)
                    added.Add(pluginInfo);
            }
        }
        catch
        {
            // All or nothing: what this call added goes again, newest first.
            for (var i = added.Count - 1; i >= 0; i--)
            {
                try
                {
                    _pluginManager.UninstallPlugin(added[i]);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to uninstall version {Version} of plugin {PluginName} after a failed install.", added[i].Version.Version, added[i].Name);
                }
            }

            throw;
        }

        return installed;
    }

    /// <summary>
    ///   The installed plugin versions, the way the planner and the upgrades
    ///   read them.
    /// </summary>
    /// <returns>Every installed plugin version.</returns>
    private List<InstalledPlugin> GetInstalledPlugins()
        => _pluginManager.GetPluginInfos()
            .Where(pluginInfo => pluginInfo.IsInstalled)
            .Select(pluginInfo => new InstalledPlugin(
                pluginInfo.ID,
                pluginInfo.Name,
                pluginInfo.Version.Version,
                pluginInfo.IsEnabled,
                pluginInfo.IsPinned,
                IsBundled: !pluginInfo.CanUninstall,
                IsDependency: pluginInfo.IsDependency,
                Dependencies: pluginInfo.Dependencies,
                IsEnabledOnDemand: pluginInfo.IsEnabledOnDemand
            ))
            .ToList();

    /// <summary>
    ///   Gets the directory a package is extracted to.
    /// </summary>
    /// <param name="packageInfo">The package.</param>
    /// <returns>The directory, in the user plugins folder.</returns>
    private string GetExtractPath(PackageInfo packageInfo)
        => Path.Join(_applicationPaths.PluginsPath, $"{packageInfo.Manifest.Name.ReplaceInvalidPathCharacters()}-{packageInfo.Release.Version}-{packageInfo.Archive.RuntimeIdentifier}");

    /// <summary>
    ///   Downloads, verifies and extracts a package to its directory, unless
    ///   the directory already holds it.
    /// </summary>
    /// <param name="packageInfo">The package.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The directory the package is in.</returns>
    /// <exception cref="OperationCanceledException">
    ///   Thrown if the <paramref name="cancellationToken"/> cancelled it.
    /// </exception>
    /// <exception cref="InvalidChecksumException">
    ///   Thrown when the archive does not match its checksum.
    /// </exception>
    private async Task<string> DownloadAndExtractPackage(PackageInfo packageInfo, CancellationToken cancellationToken)
    {
        var extractPath = GetExtractPath(packageInfo);
        var zipPath = extractPath + ".zip";
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
            if (Directory.GetFileSystemEntries(extractPath) is { Length: 1 } entries && Directory.Exists(entries[0]))
            {
                _logger.LogDebug("Found inner directory, moving contents to root.");
                var innerPath = entries[0];
                foreach (var entryPath in Directory.GetFileSystemEntries(innerPath))
                {
                    var destinationPath = Path.Join(extractPath, Path.GetFileName(entryPath));
                    if (Directory.Exists(entryPath))
                        Directory.Move(entryPath, destinationPath);
                    else
                        File.Move(entryPath, destinationPath, overwrite: true);
                }
                Directory.Delete(innerPath, true);
            }

            File.Delete(zipPath);

            // Extraction keeps the archive's file times, so stamp the install time on the DLLs, the main one among them.
            var installedAt = DateTime.UtcNow;
            foreach (var dllPath in Directory.EnumerateFiles(extractPath, "*.dll", SearchOption.AllDirectories))
                File.SetLastWriteTimeUtc(dllPath, installedAt);

            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Installation cancelled during archive extraction.");
        }

        return extractPath;
    }

    private async Task DownloadAndVerifyArchiveAsync(string downloadUrl, string destinationPath, string checksum, CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient("Default");

        // Parse optional hash prefix (e.g. "sha256:...", "sha1:...", "md5:..."), default to SHA256.
        string expectedChecksum;
        HashAlgorithm hashAlgorithm;
        if (checksum.IndexOf(':') is > -1 and var colonIndex)
        {
            var prefix = checksum[..colonIndex];
            expectedChecksum = checksum[(colonIndex + 1)..];
            hashAlgorithm = prefix.ToLowerInvariant() switch
            {
                "sha256" => SHA256.Create(),
                "sha1" => SHA1.Create(),
                "md5" => MD5.Create(),
                _ => throw new ArgumentException($"Unknown hash prefix '{prefix}' in checksum. Supported prefixes: sha256:, sha1:, md5:."),
            };
        }
        else
        {
            hashAlgorithm = SHA256.Create();
            expectedChecksum = checksum;
        }

        using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException("Download cancelled during header check.");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var fileStream = File.Create(destinationPath);

        var hashBytes = new byte[1024 * 1024];
        int bytesRead;
        long totalBytesRead = 0;

        while ((bytesRead = await stream.ReadAsync(hashBytes, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Download cancelled during data read.");

            hashAlgorithm.TransformBlock(hashBytes, 0, bytesRead, hashBytes, 0);
            await fileStream.WriteAsync(hashBytes.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            totalBytesRead += bytesRead;
        }

        hashAlgorithm.TransformFinalBlock([], 0, 0);

        _logger.LogDebug($"Downloaded package with {totalBytesRead} bytes");

        var actualHash = Convert.ToHexString(hashAlgorithm.Hash!);

        hashAlgorithm.Dispose();

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
        foreach (var repositoryInfo in ListPackageRepositories().Where(repositoryInfo => repositoryInfo.IsEnabled))
        {
            cancellationToken.ThrowIfCancellationRequested();
            // A repository that never synced, such as one the server just added, is as stale as it gets.
            if (allowSync && (forceSyncNow || repositoryInfo.LastFetchedAt is null || now - repositoryInfo.LastFetchedAt > (repositoryInfo.StaleTime ?? DefaultRepositoryStaleTime)))
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
                Icon = list.FirstOrDefault(m => m.Icon is not null)?.Icon,
                Name = list[0].Name,
                IsMeta = list.Select(m => m.IsMeta).FirstOrDefault(isMeta => isMeta is not null),
                Releases = list.SelectMany(m => m.Releases)
                    .OrderByDescending(m => m.Version, new SemverVersionComparer())
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
    ///   Optional. Whether to return only packages for this server's runtime.
    ///   Releases built for a newer server are kept, so they can be installed
    ///   ahead of a server upgrade. Defaults to <c>true</c>.
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
            var manifestPlugins = _pluginManager.GetPluginInfos(manifest.PackageID);
            foreach (var release in manifest.Releases)
            {
                var repository = repositories.FirstOrDefault(r => r.ID == release.RepositoryID);
                foreach (var archive in release.Archives)
                {
                    if (onlyCompatible && archive.RuntimeIdentifier is not IPluginManager.AnyRuntimeIdentifier && archive.RuntimeIdentifier != _pluginManager.RuntimeIdentifier)
                        continue;

                    var plugin = release.SourceRevision is { Length: > 0 } a
                        ? manifestPlugins.FirstOrDefault(p =>
                            p.Version.SourceRevision is { Length: > 0 } b && string.Equals(a, b) &&
                            p.Version.AbstractionVersion == archive.AbstractionVersion &&
                            p.Version.RuntimeIdentifier == archive.RuntimeIdentifier
                        )
                        : manifestPlugins.FirstOrDefault(p =>
                            p.Version.Version == release.Version &&
                            p.Version.AbstractionVersion == archive.AbstractionVersion &&
                            p.Version.RuntimeIdentifier == archive.RuntimeIdentifier
                        ) ?? manifestPlugins.FirstOrDefault(p =>
                            p.Version.Version == release.Version &&
                            p.Version.AbstractionVersion == archive.AbstractionVersion &&
                            p.Version.RuntimeIdentifier == IPluginManager.AnyRuntimeIdentifier
                        );
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
        var list = new List<PackageRepositoryInfo>();
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
                    if (Path.GetFileName(dirPath) is not { Length: > 0 } dirName || !Guid.TryParse(dirName, out _))
                        continue;

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
    public async Task<PackageRepositoryInfo> AddPackageRepository(PackageRepositoryData data, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(data.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.Url);
        if (!Uri.TryCreate(data.Url, UriKind.Absolute, out var uri))
            throw new ArgumentException("Url must be absolute!", nameof(data.Url));
        if (uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Url must use the http:// or https:// schema!", nameof(data.Url));
        if (data.StaleTime.HasValue && data.StaleTime.Value < TimeSpan.Zero)
            throw new ArgumentException("Stale time cannot be less than zero.", nameof(data.StaleTime));

        var name = data.Name.Trim();
        var url = uri.ToString();
        var existingRepositories = ListPackageRepositories();
        var repositoryInfo = new PackageRepositoryInfo()
        {
            ID = UuidUtility.GetV5(url),
            Name = name,
            Description = string.IsNullOrWhiteSpace(data.Description) ? null : data.Description.Trim(),
            Url = url,
            StaleTime = data.StaleTime ?? DefaultRepositoryStaleTime,
            LastFetchedAt = null,
            IsEnabled = data.IsEnabled,
        };

        lock (_logger)
        {
            if (existingRepositories.Any(r => string.Equals(r.Name, name, StringComparison.InvariantCultureIgnoreCase)))
                throw new ArgumentException("An existing repository with the given name already exists.", nameof(data.Name));
            if (existingRepositories.Any(r => string.Equals(r.Url, url)))
                throw new ArgumentException("An existing repository with the given URL already exists.", nameof(data.Url));
            WriteRepository(repositoryInfo);

            _logger.LogInformation($"Repository '{name}' created.");
        }

        return await SyncPackageRepository(repositoryInfo, forceSync: true, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PackageRepositoryInfo> EnablePackageRepository(PackageRepositoryInfo repositoryInfo, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = SetRepositoryEnabled(repositoryInfo, true);
        try
        {
            return await SyncPackageRepository(current, forceSync: true, cancellationToken).ConfigureAwait(false);
        }
        // Enabled either way; it syncs again when it can be reached.
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Enabled repository {RepositoryName} but could not sync it.", current.Name);
            return current;
        }
    }

    /// <inheritdoc/>
    public PackageRepositoryInfo DisablePackageRepository(PackageRepositoryInfo repositoryInfo)
        => SetRepositoryEnabled(repositoryInfo, false);

    private PackageRepositoryInfo SetRepositoryEnabled(PackageRepositoryInfo repositoryInfo, bool isEnabled)
    {
        lock (_logger)
        {
            // Read again, so a sync that finished in the meantime is not written over.
            var current = ListPackageRepositories().FirstOrDefault(existing => existing.ID == repositoryInfo.ID)
                ?? throw new ArgumentException("The repository does not exist.", nameof(repositoryInfo));
            if (current.IsEnabled == isEnabled)
                return current;

            current.IsEnabled = isEnabled;
            WriteRepository(current);
            _logger.LogInformation("{Action} repository {RepositoryName} (Id={RepositoryId})", isEnabled ? "Enabled" : "Disabled", current.Name, current.ID);
            return current;
        }
    }

    private void WriteRepository(PackageRepositoryInfo repositoryInfo)
    {
        var repoPath = Path.Join(_applicationPaths.PluginsPath, Repositories, repositoryInfo.ID.ToString(), RepoInfoFile);
        var repoJson = JsonConvert.SerializeObject(repositoryInfo);

        Directory.CreateDirectory(Path.GetDirectoryName(repoPath)!);
        File.WriteAllText(repoPath, repoJson);
    }

    #region Default Repositories

    /// <summary>
    ///   A repository shipped with the server.
    /// </summary>
    /// <param name="Key">
    ///   What the repository is remembered by once added. It outlives a
    ///   change of URL, so a moved feed is not taken for a new one.
    /// </param>
    /// <param name="Name">The name to add it under.</param>
    /// <param name="Description">What it holds and who stands behind it.</param>
    /// <param name="Url">The URL of its manifest.</param>
    /// <param name="IsEnabled">Whether it is added enabled, or left for an admin to enable.</param>
    internal sealed record DefaultPackageRepository(string Key, string Name, string Description, string Url, bool IsEnabled = true);

    /// <summary>
    ///   The repositories shipped with the server.
    /// </summary>
    internal static readonly IReadOnlyList<DefaultPackageRepository> DefaultRepositories =
    [
        new(
            "core",
            "Shoko Core Repository",
            "Plugins that are part of the official Shoko organization.",
            "https://shokoanime.com/core-plugin-manifest.json"
        ),
        new(
            "community",
            "Shoko Community Repository",
            "Plugins owned by their own authors, curated by the Shoko team.",
            "https://shokoanime.com/community-plugin-manifest.json",
            IsEnabled: false
        ),
    ];

    /// <summary>
    ///   Adds the repositories shipped with the server that were never added
    ///   before, without syncing them, and remembers them so they are not
    ///   added again, not even after an admin removed one.
    /// </summary>
    internal void SeedDefaultPackageRepositories()
    {
        var seeded = configurationProvider.Load().Plugins.Updates.SeededRepositories;
        var pending = SelectUnseededRepositories(DefaultRepositories, seeded);
        if (pending.Count is 0)
            return;

        foreach (var defaultRepository in pending)
            AddDefaultRepository(defaultRepository);

        var config = configurationProvider.Load(copy: true);
        config.Plugins.Updates.SeededRepositories = [.. config.Plugins.Updates.SeededRepositories, .. pending.Select(defaultRepository => defaultRepository.Key)];
        configurationProvider.Save(config);
    }

    /// <summary>
    ///   Picks the default repositories that were never added before.
    /// </summary>
    /// <param name="defaults">The repositories shipped with the server.</param>
    /// <param name="seededKeys">The keys of those added before.</param>
    /// <returns>The repositories still to add, in the order given.</returns>
    internal static IReadOnlyList<DefaultPackageRepository> SelectUnseededRepositories(IReadOnlyList<DefaultPackageRepository> defaults, IReadOnlyCollection<string> seededKeys)
        => defaults
            .Where(defaultRepository => !seededKeys.Contains(defaultRepository.Key, StringComparer.OrdinalIgnoreCase))
            .ToList();

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PackageRepositoryInfo>> RestoreDefaultPackageRepositories(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var added = DefaultRepositories
            .Select(AddDefaultRepository)
            .WhereNotNull()
            .ToList();
        var restored = new List<PackageRepositoryInfo>();
        foreach (var repositoryInfo in added)
        {
            try
            {
                restored.Add(await SyncPackageRepository(repositoryInfo, forceSync: true, cancellationToken).ConfigureAwait(false));
            }
            // The repository is back either way; it syncs again when it can be reached.
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Restored repository {RepositoryName} but could not sync it.", repositoryInfo.Name);
                restored.Add(repositoryInfo);
            }
        }

        return restored;
    }

    /// <summary>
    ///   Adds a default repository unless one with its URL exists, under its
    ///   own name or, when that is taken, a numbered one.
    /// </summary>
    /// <param name="defaultRepository">The repository to add.</param>
    /// <returns>The repository added, or <c>null</c> when its URL was there already.</returns>
    private PackageRepositoryInfo? AddDefaultRepository(DefaultPackageRepository defaultRepository)
    {
        var url = new Uri(defaultRepository.Url).ToString();
        lock (_logger)
        {
            var existingRepositories = ListPackageRepositories();
            if (existingRepositories.Any(repositoryInfo => string.Equals(repositoryInfo.Url, url)))
                return null;

            var name = defaultRepository.Name;
            for (var number = 2; existingRepositories.Any(repositoryInfo => string.Equals(repositoryInfo.Name, name, StringComparison.InvariantCultureIgnoreCase)); number++)
                name = $"{defaultRepository.Name} ({number})";

            var repositoryInfo = new PackageRepositoryInfo()
            {
                ID = UuidUtility.GetV5(url),
                Name = name,
                Description = defaultRepository.Description,
                Url = url,
                StaleTime = null,
                LastFetchedAt = null,
                IsEnabled = defaultRepository.IsEnabled,
            };
            WriteRepository(repositoryInfo);

            _logger.LogInformation("Added the default repository {RepositoryName}. ({RepositoryUrl})", name, url);
            return repositoryInfo;
        }
    }

    #endregion

    /// <inheritdoc/>
    public Task<bool> RemovePackageRepository(PackageRepositoryInfo repositoryInfo, CancellationToken cancellationToken = default)
    {
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
        // A disabled repository is never fetched, not even when forced.
        if (!repositoryInfo.IsEnabled)
            return repositoryInfo;

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

            var httpClient = httpClientFactory.CreateClient("Default");
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
        // Only a referenced manifest falls back on its cached copy: when the
        // repository's own manifest fails, the last synced state stays as is.
        var (document, lastFetchedAt) = await FetchManifestAsync(httpClient, manifestUrl, allowStaleCache: !allowArray, cancellationToken).ConfigureAwait(false);
        var manifests = new List<(RemotePackageManifestInfo Manifest, DateTime fetchedAt)>();
        // Keyed by package *and* source, not by package alone. One package
        // legitimately appears more than once in a repository - a stable
        // manifest and a dev manifest are the same package at two addresses,
        // and GetAvailablePackageManifests below is built to union their
        // releases. What is never meaningful is the same package read twice
        // from the same address: that is a repository listing an entry
        // twice, and ingesting it duplicates every release it carries.
        var seen = new HashSet<(Guid PackageID, string Source)>();
        try
        {
            var rootElement = document.RootElement;
            if (rootElement.ValueKind == JsonValueKind.Array)
            {
                if (!allowArray)
                    throw new InvalidOperationException("A referenced single-repository manifest cannot contain multiple manifests.");

                foreach (var element in rootElement.EnumerateArray())
                {
                    RemotePackageManifestInfo? manifest = null;
                    try
                    {
                        manifest = JsonConvert.DeserializeObject<RemotePackageManifestInfo>(element.GetRawText());
                        if (manifest is { IsReference: true })
                        {
                            var subManifests = await GetRemoteManifestAsync(httpClient, manifest.ManifestUrl, allowArray: false, cancellationToken: cancellationToken).ConfigureAwait(false);
                            foreach (var subManifest in subManifests)
                                AddUnlessSeen(subManifest.Manifest, subManifest.fetchedAt, manifest.ManifestUrl);
                        }
                        else if (manifest is not null)
                            AddUnlessSeen(manifest, lastFetchedAt, manifestUrl);
                    }
                    catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        if (manifest is { IsReference: true })
                            _logger.LogWarning(
                                IsExpectedFetchFailure(ex) ? null : ex,
                                "Could not fetch the linked manifest at {ManifestUrl}: {Reason}. Its packages are left out, since no earlier copy is cached. " +
                                "The rest of the repository is not affected.",
                                manifest.ManifestUrl,
                                DescribeFetchFailure(ex)
                            );
                        else
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
                AddUnlessSeen(manifest, lastFetchedAt, manifestUrl);
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

        void AddUnlessSeen(RemotePackageManifestInfo manifest, DateTime fetchedAt, string source)
        {
            if (!seen.Add((manifest.PackageID, source)))
            {
                _logger.LogDebug("Skipped a duplicate manifest for package {PackageId} from {Source}.", manifest.PackageID, source);
                return;
            }

            manifests.Add((manifest, fetchedAt));
        }
    }

    /// <summary>
    ///   Fetch a manifest, keeping a copy of every good answer. A manifest the
    ///   server says is unchanged is read from that copy.
    /// </summary>
    /// <param name="httpClient">The client to fetch with.</param>
    /// <param name="manifestUrl">The manifest's address.</param>
    /// <param name="allowStaleCache">
    ///   Whether to answer with the kept copy when the fetch fails.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    ///   The manifest and when it was last fetched.
    /// </returns>
    /// <exception cref="HttpRequestException">
    ///   Thrown if the fetch failed and no kept copy could be used.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Thrown if the answer is not JSON and no kept copy could be used.
    /// </exception>
    private async Task<(JsonDocument Document, DateTime LastFetchedAt)> FetchManifestAsync(HttpClient httpClient, string manifestUrl, bool allowStaleCache, CancellationToken cancellationToken)
    {
        var manifestId = UuidUtility.GetV5(manifestUrl);
        var jsonPath = Path.Join(_applicationPaths.PluginsPath, Repositories, "cache", manifestId + ".json");
        var etagPath = Path.ChangeExtension(jsonPath, ".etag");
        var cacheExists = File.Exists(jsonPath);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, manifestUrl);
            if (cacheExists)
            {
                request.Headers.IfModifiedSince = File.GetLastWriteTimeUtc(jsonPath);
                if (File.Exists(etagPath) && File.ReadAllText(etagPath, Encoding.UTF8) is { Length: > 0 } cachedEtag)
                    request.Headers.IfNoneMatch.ParseAdd(cachedEtag);
            }

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (cacheExists && response.StatusCode is HttpStatusCode.NotModified)
                return ReadCachedManifest(jsonPath);

            response.EnsureSuccessStatusCode();
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType is not ("application/json" or "text/plain" or "text/json"))
                throw new InvalidOperationException($"Invalid content type: \"{contentType}\". Expected \"application/json\", \"text/plain\" or \"text/json\".");

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            JsonDocument jsonDoc;
            try
            {
                jsonDoc = JsonDocument.Parse(content);
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new InvalidOperationException("The manifest is not valid JSON.", ex);
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
                File.WriteAllBytes(jsonPath, content);
                if (response.Headers.ETag?.ToString() is { Length: > 0 } etag)
                    File.WriteAllText(etagPath, etag, Encoding.UTF8);
                else
                    File.Delete(etagPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Failed to cache the manifest at {ManifestUrl}.", manifestUrl);
            }

            return (jsonDoc, DateTime.UtcNow);
        }
        catch (Exception ex) when (allowStaleCache && cacheExists && !cancellationToken.IsCancellationRequested && ex is HttpRequestException or InvalidOperationException or OperationCanceledException)
        {
            var cached = ReadCachedManifest(jsonPath);
            _logger.LogWarning(
                IsExpectedFetchFailure(ex) ? null : ex,
                "Could not fetch the linked manifest at {ManifestUrl}: {Reason}. Kept its packages from the copy cached at {CachedAt}.",
                manifestUrl,
                DescribeFetchFailure(ex),
                cached.LastFetchedAt
            );
            return cached;
        }
    }

    private static (JsonDocument Document, DateTime LastFetchedAt) ReadCachedManifest(string jsonPath)
        => (JsonDocument.Parse(File.ReadAllBytes(jsonPath)), File.GetLastWriteTimeUtc(jsonPath));

    /// <summary>
    ///   Whether a failed manifest fetch is the server answering with an error
    ///   status, so the log line says enough without a stack trace.
    /// </summary>
    /// <param name="exception">The exception the fetch failed with.</param>
    /// <returns>
    ///   <c>true</c> when the exception carries an HTTP status code.
    /// </returns>
    private static bool IsExpectedFetchFailure(Exception exception)
        => exception is HttpRequestException { StatusCode: not null };

    /// <summary>
    ///   Say in plain words why a manifest fetch failed.
    /// </summary>
    /// <param name="exception">The exception the fetch failed with.</param>
    /// <returns>
    ///   The reason, without a trailing period.
    /// </returns>
    private static string DescribeFetchFailure(Exception exception)
        => exception switch
        {
            HttpRequestException { StatusCode: HttpStatusCode.NotFound } =>
                "the server answered 404 (Not Found), so the address is wrong or the manifest is private or gone",
            HttpRequestException { StatusCode: { } statusCode } => $"the server answered {(int)statusCode} ({statusCode})",
            HttpRequestException => $"the server could not be reached ({exception.Message.TrimEnd('.')})",
            OperationCanceledException => "the request timed out",
            _ => exception.Message.TrimEnd('.'),
        };

    private async Task<PackageManifestInfo?> ConvertRemoteToLocalAsync(RemotePackageManifestInfo manifestInfo, Guid repositoryId, DateTime lastFetchedAt, CancellationToken cancellationToken)
    {
        if (manifestInfo.IsReference)
            return null;
        // `image_url` is deprecated but still honoured, so a manifest written before
        // the split keeps its image rather than silently losing it.
#pragma warning disable CS0618
        var thumbnailUrl = manifestInfo.ThumbnailUrl ?? manifestInfo.ImageUrl;
#pragma warning restore CS0618
        var thumbnail = await DownloadAndCacheImageAsync(thumbnailUrl, manifestInfo.PackageID, cancellationToken).ConfigureAwait(false);
        var icon = await DownloadAndCacheImageAsync(manifestInfo.IconUrl, manifestInfo.PackageID, cancellationToken).ConfigureAwait(false);

        return new PackageManifestInfo
        {
            PackageID = manifestInfo.PackageID,
            Name = manifestInfo.Name,
            Overview = manifestInfo.Overview,
            Authors = manifestInfo.Authors,
            RepositoryUrl = manifestInfo.RepositoryUrl,
            HomepageUrl = manifestInfo.HomepageUrl,
            IsMeta = manifestInfo.IsMeta,
            Tags = manifestInfo.Tags
                .Select(tag => tag.ToLowerInvariant())
                .Distinct()
                .ToArray(),
            Thumbnail = thumbnail,
            Icon = icon,
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
                    IsMeta = remoteRelease.IsMeta,
                    // A dependency without a range accepts any version.
                    Dependencies = remoteRelease.Dependencies
                        .Select(remoteDependency => new PluginDependency
                        {
                            PluginID = remoteDependency.PluginID,
                            VersionRange = remoteDependency.VersionRange is { Length: > 0 } range ? range : ">=0.0.0",
                            IsOptional = remoteDependency.IsOptional,
                        })
                        .ToList(),
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

    /// <summary>
    ///   Download an image named by a manifest, or return the copy already
    ///   cached for that url. Keyed by the url, so the thumbnail and the icon
    ///   share one cache without colliding.
    /// </summary>
    /// <param name="imageUrl">The url the manifest gave, if any.</param>
    /// <param name="packageId">The package, for the log line.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    ///   The image, or <c>null</c> when there was no url, the
    ///   download failed, or what came back was not an image we serve.
    /// </returns>
    private async Task<PackageImageInfo?> DownloadAndCacheImageAsync(string? imageUrl, Guid packageId, CancellationToken cancellationToken)
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
                var imageInfo = new MagickImageInfo(existingFile);
                var mime = PackageImageLoader.GetMimeFromFormat(imageInfo);
                if (mime is not null)
                    return new PackageImageInfo
                    {
                        MimeType = mime,
                        Width = (int)imageInfo.Width,
                        Height = (int)imageInfo.Height,
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
            var httpClient = httpClientFactory.CreateClient("Default");
            using var response = await httpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (ContentTypeHelper.TryGetExtensionForMimeType(contentType, out var extension))
            {
                Directory.CreateDirectory(imagesDir);
                var imagePath = Path.Join(imagesDir, $"{imageId}{extension}");
                // Closed before it is read back: a file still open for writing reads as empty.
                await using (var fileStream = File.Create(imagePath))
                    await response.Content.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);

                var imageInfo = new MagickImageInfo(imagePath);
                var mime = PackageImageLoader.GetMimeFromFormat(imageInfo);
                if (mime is not null)
                {
                    return new PackageImageInfo
                    {
                        MimeType = mime,
                        Width = (int)imageInfo.Width,
                        Height = (int)imageInfo.Height,
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
    public async Task<IReadOnlyList<PackageUpdateInfo>> GetAvailableUpdates(bool allowSync = false, bool forceSyncNow = false, bool includeInactive = false, bool includePinned = false, CancellationToken cancellationToken = default)
    {
        var manifests = await GetAvailablePackageManifests(allowSync: allowSync, forceSyncNow: forceSyncNow, cancellationToken: cancellationToken).ConfigureAwait(false);
        var localPackages = GetLocalPackages();
        var availablePackages = FilterPackageManifests(manifests, onlyCompatible: true, onlyLatest: true);

        var comparer = new SemverVersionComparer();
        var updates = new List<PackageUpdateInfo>();
        foreach (var availablePackage in availablePackages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var localPackage = localPackages.FirstOrDefault(p => p.Manifest.PackageID == availablePackage.Manifest.PackageID && p.Plugin is not null);
            if (localPackage is null)
                continue;

            if (!includeInactive && localPackage.Plugin is not { IsActive: true, IsEnabled: true })
                continue;

            if (!includePinned && localPackage.Plugin!.IsPinned)
                continue;

            if (comparer.Compare(availablePackage.Release.Version, localPackage.Release.Version) <= 0)
                continue;

            updates.Add(new PackageUpdateInfo
            {
                PackageID = availablePackage.Manifest.PackageID,
                Name = availablePackage.Manifest.Name,
                Current = localPackage,
                Latest = availablePackage,
            });
        }

        return updates;
    }

    /// <inheritdoc/>
    public async Task CheckForUpdates(bool? forceSync = null, bool? performUpgrade = null, CancellationToken cancellationToken = default)
    {
        var settings = configurationProvider.Load();
        var shouldForceSync = forceSync ?? false;

        // How often this runs on its own is up to the triggers of the action
        // that queues it, and each repository is only synced once it is stale.

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

        // The same choice as the upgrades on start, from the plugins already scanned.
        var installed = GetInstalledPlugins().Where(plugin => plugin.IsEnabled).ToList();
        var manifests = await GetAvailablePackageManifests(allowSync: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        var upgrades = SelectUpgrades(
            installed,
            manifests,
            ListPackageRepositories(),
            _pluginManager.IsAbiAndRuntimeCompatible,
            _pluginManager.RuntimeIdentifier,
            settings.Plugins.Updates.SkipBundledPluginUpgrades
        );
        foreach (var package in upgrades)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("Auto-upgrading plugin {PluginName} to {NewVersion}", package.Manifest.Name, package.Release.Version);
            try
            {
                // A release may need plugins the old one did not, which are installed with it.
                var plan = PackageInstallPlanner.Plan(package, GetInstalledPlugins(), manifests, ListPackageRepositories(), _pluginManager.IsAbiAndRuntimeCompatible, _pluginManager.RuntimeIdentifier);
                if (!plan.CanInstall)
                {
                    _logger.LogWarning(
                        "Not auto-upgrading plugin {PluginName} to {NewVersion}: {Problems}",
                        package.Manifest.Name,
                        package.Release.Version,
                        string.Join(" ", plan.Problems.Select(problem => $"{problem.Name ?? problem.PluginID.ToString()}: {problem.Reason}"))
                    );
                    continue;
                }

                await InstallPackages(plan, InstallReason.Upgrade, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-upgrade plugin {PluginName}", package.Manifest.Name);
            }
        }
    }

    /// <inheritdoc/>
    public async Task ScheduleCheckForUpdates(bool? forceSync = null, bool? performUpgrade = null, CancellationToken cancellationToken = default)
    {
        await queueSchedulerAccessor().StartJob<CheckPluginUpdatesJob>(c => (c.ForceSync, c.PerformUpgrade) = (forceSync, performUpgrade)).ConfigureAwait(false);
    }

    #endregion

    #region Start-up Upgrades

    /// <summary>
    ///   How long the upgrades before the plugin scan may take, all together.
    /// </summary>
    internal static readonly TimeSpan StartupUpgradeBudget = TimeSpan.FromSeconds(90);

    /// <summary>
    ///   Installs the newest compatible release of each enabled, unpinned
    ///   plugin on disk, before the plugin scan, within a time budget. A
    ///   timeout or error is logged and leaves the plugins as they are, so
    ///   the start goes on either way.
    /// </summary>
    /// <param name="reportProgress">Told what is being done, for the start-up message.</param>
    /// <param name="budget">How long it may take, all together.</param>
    /// <returns>The packages installed.</returns>
    internal async Task<IReadOnlyList<PackageInfo>> UpgradePluginsBeforeStart(Action<string> reportProgress, TimeSpan budget)
    {
        using var cancellationSource = new CancellationTokenSource(budget);
        try
        {
            var installed = await InstallStartupUpgrades(reportProgress, cancellationSource.Token).ConfigureAwait(false);
            if (installed.Count > 0)
                _logger.LogInformation("Upgraded {Count} plugins before loading them.", installed.Count);
            return installed;
        }
        catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
        {
            _logger.LogWarning("Upgrading plugins on start took longer than {Budget}; starting with the plugins installed.", budget);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to upgrade plugins on start; starting with the plugins installed.");
        }

        return [];
    }

    /// <summary>
    ///   Installs the newest compatible release of each enabled, unpinned
    ///   plugin on disk, after syncing the repositories fetched longer than
    ///   <see cref="PluginSettings.PluginUpdatesSettings.RepositoryStaleTimeOnStart"/>
    ///   ago. Reads the
    ///   plugins from their metadata, so nothing is loaded. Does nothing when
    ///   <see cref="IsUpgradeOnStartEnabled"/> is off, the plugins folder is
    ///   read-only or a repository is unreachable.
    /// </summary>
    /// <param name="reportProgress">Told what is being done, for the start-up message.</param>
    /// <param name="cancellationToken">Cancellation token, cancelled once the budget is spent.</param>
    /// <returns>The packages installed.</returns>
    /// <exception cref="OperationCanceledException">
    ///   Thrown if the <paramref name="cancellationToken"/> cancelled it.
    /// </exception>
    private async Task<IReadOnlyList<PackageInfo>> InstallStartupUpgrades(Action<string> reportProgress, CancellationToken cancellationToken)
    {
        if (!IsUpgradeOnStartEnabled)
        {
            _logger.LogDebug("Upgrading plugins on start is turned off.");
            return [];
        }

        if (!CanWriteToPluginsFolder())
        {
            _logger.LogInformation("Not upgrading plugins on start, as the plugins folder cannot be written to. ({PluginsPath})", _applicationPaths.PluginsPath);
            return [];
        }

        var allInstalled = InstalledPluginReader.ReadInstalledPlugins().ToList();
        var installed = allInstalled.Where(plugin => plugin.IsEnabled).ToList();
        if (installed.Count is 0)
            return [];

        reportProgress("Checking for plugin updates...");
        var staleTimeOnStart = configurationProvider.Load().Plugins.Updates.RepositoryStaleTimeOnStart;
        foreach (var repositoryInfo in ListPackageRepositories().Where(repositoryInfo => repositoryInfo.IsEnabled))
        {
            var lastFetchedAt = repositoryInfo.LastFetchedAt;
            var staleTime = repositoryInfo.StaleTime ?? DefaultRepositoryStaleTime;
            if (staleTimeOnStart < staleTime)
                staleTime = staleTimeOnStart;
            if (lastFetchedAt.HasValue && DateTime.UtcNow - lastFetchedAt.Value <= staleTime)
                continue;

            // Forced, as the repository's own stale time may not have passed. A failed sync keeps the
            // last fetch time; that is how an unreachable repository shows.
            await SyncPackageRepository(repositoryInfo, forceSync: true, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (repositoryInfo.LastFetchedAt == lastFetchedAt)
            {
                _logger.LogWarning("Not upgrading plugins on start, as repository {RepositoryName} could not be reached.", repositoryInfo.Name);
                return [];
            }
        }

        var manifests = await GetAvailablePackageManifests(allowSync: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        var upgrades = SelectUpgrades(
            installed,
            manifests,
            ListPackageRepositories(),
            _pluginManager.IsAbiAndRuntimeCompatible,
            _pluginManager.RuntimeIdentifier,
            SkipBundledPluginUpgrades
        );
        if (upgrades.Count is 0)
        {
            _logger.LogInformation("All plugins are up to date.");
            return [];
        }

        reportProgress("Installing plugin updates...");
        var done = new List<PackageInfo>();
        foreach (var packageInfo in upgrades)
        {
            // A release may need plugins the old one did not, which are installed with it.
            var plan = PackageInstallPlanner.Plan(packageInfo, allInstalled, manifests, ListPackageRepositories(), _pluginManager.IsAbiAndRuntimeCompatible, _pluginManager.RuntimeIdentifier);
            if (!plan.CanInstall)
            {
                _logger.LogWarning(
                    "Not upgrading plugin {PluginName} to version {Version} on start: {Problems}",
                    packageInfo.Manifest.Name,
                    packageInfo.Release.Version,
                    string.Join(" ", plan.Problems.Select(problem => $"{problem.Name ?? problem.PluginID.ToString()}: {problem.Reason}"))
                );
                continue;
            }

            try
            {
                foreach (var step in plan.Steps)
                {
                    var reason = step.IsDependency ? InstallReason.Dependency : InstallReason.Upgrade;
                    var otherVersionsMarked = allInstalled.Where(plugin => plugin.ID == step.Package.Manifest.PackageID).Select(plugin => plugin.IsDependency).ToList();
                    var markAsDependency = ShouldMarkAsDependency(reason, otherVersionsMarked);
                    if (await InstallPackageBeforeStart(step.Package, markAsDependency, step.RequiredBy, cancellationToken).ConfigureAwait(false) is { } read)
                        allInstalled.Add(read);
                    done.Add(step.Package);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to upgrade plugin {PluginName} to version {Version} on start.", packageInfo.Manifest.Name, packageInfo.Release.Version);
            }
        }

        return done;
    }

    /// <summary>
    ///   Picks the release to install for each installed plugin: the newest
    ///   one above its newest installed version that every other installed
    ///   plugin depending on it accepts, with an archive
    ///   <paramref name="isCompatible"/> accepts. A plugin with a pinned
    ///   version is left alone, and so is one the server ships when
    ///   <paramref name="skipBundled"/> is set.
    /// </summary>
    /// <param name="installed">The enabled plugin versions on disk.</param>
    /// <param name="manifests">The package manifests.</param>
    /// <param name="repositories">The repositories, to attach to each package.</param>
    /// <param name="isCompatible">Whether an archive's ABI version and runtime can load here.</param>
    /// <param name="runtimeIdentifier">This server's runtime, preferred over a universal archive.</param>
    /// <param name="skipBundled">Whether to leave alone the plugins the server ships.</param>
    /// <returns>The packages to install, one per plugin at most.</returns>
    internal static IReadOnlyList<PackageInfo> SelectUpgrades(
        IEnumerable<InstalledPlugin> installed,
        IEnumerable<PackageManifestInfo> manifests,
        IReadOnlyList<PackageRepositoryInfo> repositories,
        Func<Version, string, bool> isCompatible,
        string runtimeIdentifier,
        bool skipBundled = false
    )
    {
        var comparer = new SemverVersionComparer();
        var manifestsByID = manifests.ToDictionary(manifest => manifest.PackageID);
        var installedList = installed.ToList();
        var upgrades = new List<PackageInfo>();
        foreach (var versions in installedList.GroupBy(version => version.ID))
        {
            if (versions.Any(version => version.IsPinned || (skipBundled && version.IsBundled)) || !manifestsByID.TryGetValue(versions.Key, out var manifest))
                continue;

            // An upgrade the plugins depending on it do not accept would refuse them on the next start.
            var ranges = installedList
                .Where(other => other.ID != versions.Key)
                .SelectMany(other => other.Dependencies ?? [])
                .Where(dependency => dependency.PluginID == versions.Key)
                .Select(dependency => dependency.VersionRange)
                .Distinct()
                .ToList();
            var current = versions.Select(version => version.Version).Max(comparer)!;
            foreach (var release in manifest.Releases.OrderByDescending(release => release.Version, comparer))
            {
                if (comparer.Compare(NormalizeVersion(release.Version), current) <= 0)
                    break;

                if (ranges.Any(range => !PluginVersionRange.IsSatisfied(range, NormalizeVersion(release.Version))))
                    continue;

                var archive = release.Archives
                    .Where(archive => isCompatible(archive.AbstractionVersion, archive.RuntimeIdentifier))
                    .OrderByDescending(archive => archive.RuntimeIdentifier == runtimeIdentifier)
                    .ThenByDescending(archive => archive.AbstractionVersion, comparer)
                    .FirstOrDefault();
                if (archive is null)
                    continue;

                upgrades.Add(new()
                {
                    Manifest = manifest,
                    Repository = repositories.FirstOrDefault(repository => repository.ID == release.RepositoryID),
                    Archive = archive,
                    Plugin = null,
                    Release = release,
                });
                break;
            }
        }

        return upgrades;
    }

    /// <summary>
    ///   Drops a revision of zero or less and fills in a missing build, the
    ///   way the version of a plugin assembly is read.
    /// </summary>
    /// <param name="version">The version.</param>
    /// <returns>The normalized version.</returns>
    internal static Version NormalizeVersion(Version version)
        => version.Revision > 0
            ? new(version.Major, version.Minor, Math.Max(version.Build, 0), version.Revision)
            : new(version.Major, version.Minor, Math.Max(version.Build, 0));

    /// <summary>
    ///   Installs a package before the plugin scan, reading the result from
    ///   its metadata instead of loading it, which would leave it locked and
    ///   already loaded when the scan comes to it.
    /// </summary>
    /// <param name="packageInfo">The package to install.</param>
    /// <param name="markAsDependency">Whether to mark the version as installed as a dependency.</param>
    /// <param name="requiredBy">The plugins needing it, written to its marker.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The installed version, as the plugin scan reads it.</returns>
    /// <exception cref="OperationCanceledException">
    ///   Thrown if the <paramref name="cancellationToken"/> cancelled it.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when the package holds a different plugin than its manifest says.
    /// </exception>
    private async Task<InstalledPlugin?> InstallPackageBeforeStart(PackageInfo packageInfo, bool markAsDependency, IReadOnlyList<Guid> requiredBy, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Upgrading plugin {PackageName} to version {PackageVersion} before it loads. (Id={PackageId}, RuntimeId={PackageRuntimeId})",
            packageInfo.Manifest.Name,
            packageInfo.Release.Version,
            packageInfo.Manifest.PackageID,
            packageInfo.Archive.RuntimeIdentifier
        );

        var extractPath = GetExtractPath(packageInfo);
        var existed = Directory.Exists(extractPath) && Directory.EnumerateFileSystemEntries(extractPath).Any();
        try
        {
            await DownloadAndExtractPackage(packageInfo, cancellationToken).ConfigureAwait(false);
            if (markAsDependency)
                PluginManager.WriteDependencyMarker(extractPath, string.Empty, requiredBy);
            else
                PluginManager.RemoveDependencyMarker(extractPath, string.Empty);
            if (InstalledPluginReader.ReadPlugin(extractPath) is not { } installed)
                throw new InvalidOperationException("The package holds no plugin.");
            if (installed.ID != packageInfo.Manifest.PackageID)
                throw new InvalidOperationException($"The package holds plugin {installed.ID}, not {packageInfo.Manifest.PackageID}.");
            return installed;
        }
        catch when (!existed)
        {
            try
            {
                File.Delete(extractPath + ".zip");
                if (Directory.Exists(extractPath))
                    Directory.Delete(extractPath, true);
            }
            catch (Exception cleanupEx) when (cleanupEx is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(cleanupEx, "Failed to clean up the files of a failed plugin upgrade. ({ExtractPath})", extractPath);
            }

            throw;
        }
    }

    /// <summary>
    ///   Whether files can be written to the user plugins folder, which is
    ///   not the case on a read-only mount.
    /// </summary>
    /// <returns><c>true</c> when a file could be written and deleted there.</returns>
    private bool CanWriteToPluginsFolder()
    {
        try
        {
            Directory.CreateDirectory(_applicationPaths.PluginsPath);
            var probePath = Path.Join(_applicationPaths.PluginsPath, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probePath, string.Empty);
            File.Delete(probePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    #endregion

    #region Cleanup

    /// <inheritdoc/>
    public IReadOnlyList<LocalPluginInfo> PurgeInactivePluginVersions(TimeSpan? retention = null)
    {
        var retentionValue = retention ?? InactivePluginVersionRetention;
        if (retentionValue < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(retention), "Retention cannot be lower than zero.");

        var versions = SelectInactivePluginVersions(_pluginManager.GetPluginInfos(), retentionValue, DateTime.UtcNow);
        var marked = new List<LocalPluginInfo>(versions.Count);
        foreach (var pluginInfo in versions)
        {
            if (_pluginManager.UninstallPlugin(pluginInfo).IsInstalled)
                continue;

            _logger.LogInformation("Marked inactive version {Version} of plugin {PluginName} for removal on the next start.", pluginInfo.Version.Version, pluginInfo.Name);
            marked.Add(pluginInfo);
        }

        _logger.LogInformation("Marked {Count} inactive plugin versions for removal, retaining inactive versions for {Retention}.", marked.Count, retentionValue);
        return marked;
    }

    /// <summary>
    ///   Selects the installed versions that are not active, not pinned, not
    ///   kept, can be uninstalled and whose plugin has an active version. One
    ///   older than the active version goes once <paramref name="retention"/>
    ///   passed since the first newer version was installed; one newer goes
    ///   once it passed since its own install, but only while the active
    ///   version is pinned, as it is a pending update otherwise.
    /// </summary>
    /// <param name="pluginInfos">Every registered plugin version.</param>
    /// <param name="retention">How long an inactive version is kept.</param>
    /// <param name="now">The current time, in UTC.</param>
    /// <returns>The versions to remove.</returns>
    internal static IReadOnlyList<LocalPluginInfo> SelectInactivePluginVersions(IEnumerable<LocalPluginInfo> pluginInfos, TimeSpan retention, DateTime now)
    {
        var selected = new List<LocalPluginInfo>();
        foreach (var versions in pluginInfos.Where(p => p.IsInstalled).GroupBy(p => p.ID))
        {
            var active = versions.FirstOrDefault(p => p.IsActive);
            if (active is null)
                continue;

            foreach (var pluginInfo in versions)
            {
                if (pluginInfo.IsActive || pluginInfo.IsPinned || pluginInfo.IsKept || !pluginInfo.CanUninstall)
                    continue;

                var version = pluginInfo.Version.Version;
                DateTime since;
                if (version < active.Version.Version)
                    since = versions.Where(p => p.Version.Version > version).Min(p => p.InstalledAt);
                else if (version > active.Version.Version && active.IsPinned)
                    since = pluginInfo.InstalledAt;
                else
                    continue;

                if (retention == TimeSpan.Zero || now - since > retention)
                    selected.Add(pluginInfo);
            }
        }

        return selected;
    }

    #endregion
}
