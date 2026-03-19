using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Plugin.Events;
using Shoko.Abstractions.Plugin.Exceptions;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Plugin;

/// <summary>
///   Responsible for package installation, upgrades, and repository management.
/// </summary>
public interface IPluginPackageManager
{
    #region Settings

    /// <summary>
    ///   Whether automatic repository syncing is enabled.
    /// </summary>
    bool IsAutoSyncEnabled { get; set; }

    /// <summary>
    ///   Whether automatic plugin upgrades are enabled.
    /// </summary>
    bool IsAutoUpgradeEnabled { get; set; }

    /// <summary>
    ///   Default time before a repository's packages are considered stale.
    ///   Defaults to 12 hours.
    /// </summary>
    TimeSpan DefaultRepositoryStaleTime { get; set; }

    /// <summary>
    ///   Time to retain old plugin versions before auto-cleanup. Defaults to
    ///   30 days.
    /// </summary>
    TimeSpan InactivePluginVersionRetention { get; set; }

    #endregion

    #region Package Installation

    /// <summary>
    ///   Dispatched when a plugin installation starts. Ran in-thread, and can
    ///   be used to cancel the operation.
    /// </summary>
    event EventHandler<PackageInstallationStartedEventArgs>? PackageInstallationStarted;

    /// <summary>
    ///   Dispatched when a plugin installation completes.
    /// </summary>
    event EventHandler<PackageInstallationCompletedEventArgs>? PackageInstallationCompleted;

    /// <summary>
    ///   Dispatched when a plugin installation fails.
    /// </summary>
    event EventHandler<PackageInstallationFailedEventArgs>? PackageInstallationFailed;

    /// <summary>
    ///   Gets all packages installed since the server startup till now.
    /// </summary>
    /// <returns>
    ///   A list of the packages installed since the server startup till now.
    /// </returns>
    IReadOnlyList<PackageInfo> GetInstalledPackages();

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
    /// <exception cref="HttpRequestException">
    ///   The request failed due to an issue getting a valid successful HTTP
    ///   response, such as network connectivity failure, DNS failure, server
    ///   certificate validation error, or invalid server response. On .NET 8
    ///   and later versions, the reason is indicated by
    ///   <see cref="HttpRequestException.HttpRequestError"/>.
    /// </exception>
    Task<LocalPluginInfo?> InstallPackage(PackageInfo packageInfo, CancellationToken cancellationToken = default);

    #endregion

    #region Package Discovery & Listing

    /// <summary>
    ///   Gets all locally installed packages as <see cref="PackageInfo"/>.
    /// </summary>
    /// <returns>
    ///   List of all packages installed locally in the system.
    /// </returns>
    IReadOnlyList<PackageInfo> GetLocalPackages();

    /// <summary>
    ///   Gets all available packages across all synced repositories.
    /// </summary>
    /// <param name="allowSync">
    ///   Optional. Whether to sync repositories before retrieving packages.
    /// </param>
    /// <param name="forceSyncNow">
    ///   Optional. Wether to forcefully sync the repositories.
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
    Task<IReadOnlyList<PackageManifestInfo>> GetAvailablePackageManifests(
        bool allowSync = true,
        bool forceSyncNow = false,
        CancellationToken cancellationToken = default
    );

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
    IEnumerable<PackageInfo> FilterPackageManifests(
        IEnumerable<PackageManifestInfo> packageManifests,
        bool onlyCompatible = true,
        bool onlyLatest = true
    );

    #endregion

    #region Repository Management

    /// <summary>
    ///   Dispatched right before a repository sync operation begins. Ran
    ///   in-thread, and can be used to cancel or force the operation.
    /// </summary>
    event EventHandler<RepositorySyncStartedEventArgs>? RepositorySyncStarted;

    /// <summary>
    ///   Dispatched when a repository sync operation fails.
    /// </summary>
    event EventHandler<RepositorySyncFailedEventArgs>? RepositorySyncFailed;

    /// <summary>
    ///   Dispatched when a repository sync operations is completed.
    /// </summary>
    event EventHandler<RepositorySyncCompletedEventArgs>? RepositorySyncCompleted;

    /// <summary>
    ///   Lists all configured repositories.
    /// </summary>
    /// <returns>
    ///   List of repositories.
    /// </returns>
    IReadOnlyList<PackageRepositoryInfo> ListPackageRepositories();

    /// <summary>
    ///   Add a new repository.
    /// </summary>
    /// <param name="name">
    ///   Friendly name of the repository.
    /// </param>
    /// <param name="url">
    ///   Repository API endpoint URL.
    /// </param>
    /// <param name="staleTime">
    ///   Optional. Custom stale time for this specific repository.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. Cancellation token.
    /// </param>
    /// <returns>
    ///   The newly added repository info.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///   The name or URL already exists for an existing repository.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///   Thrown if the add was cancelled by the
    ///   <paramref name="cancellationToken"/>.
    /// </exception>
    Task<PackageRepositoryInfo> AddPackageRepository(string name, string url, TimeSpan? staleTime = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Removes a repository.
    /// </summary>
    /// <param name="repositoryInfo">
    ///   The repository to remove.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. Cancellation token.
    /// </param>
    /// <returns>
    ///   <see langword="true"/> if the repository was successfully removed;
    ///   otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///   Thrown if the removal was cancelled by the
    ///   <paramref name="cancellationToken"/>.
    /// </exception>
    Task<bool> RemovePackageRepository(PackageRepositoryInfo repositoryInfo, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Syncs a specific repository, fetching the latest package list if the
    ///   last fetched timestamp is outside the stale period.
    /// </summary>
    /// <param name="repositoryInfo">
    ///   The repository to sync.
    /// </param>
    /// <param name="forceSync">
    ///   Optional. Whether to force a sync even if the repository is not stale.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. Cancellation token.
    /// </param>
    /// <returns>
    ///   Task completing when sync is finished.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///   Thrown if the sync was cancelled by the
    ///   <paramref name="cancellationToken"/>.
    /// </exception>
    Task<PackageRepositoryInfo> SyncPackageRepository(PackageRepositoryInfo repositoryInfo, bool forceSync = false, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Syncs all repositories, fetching the latest package list if the last
    ///   fetched timestamp is outside the stale period.
    /// </summary>
    /// <param name="forceSync">
    ///   Optional. Whether to force a sync even if the repository is not stale.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. Cancellation token.
    /// </param>
    /// <returns>
    ///   Task completing when sync is finished.
    /// </returns>
    Task SyncAllPackageRepositories(bool forceSync = false, CancellationToken cancellationToken = default);

    #endregion

    #region Update Checking

    /// <summary>
    ///   Checks for plugin updates and optionally performs upgrades on enabled plugins.
    /// </summary>
    /// <param name="forceSync">
    ///   Force sync even if not time yet. If null, checks the configured schedule.
    /// </param>
    /// <param name="performUpgrade">
    ///   Whether to upgrade enabled plugins. If null, uses settings default.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. Cancellation token.
    /// </param>
    /// <returns>
    ///   Task completing when the check (and optional upgrades) are finished.
    /// </returns>
    Task CheckForUpdates(bool? forceSync = null, bool? performUpgrade = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Schedules a plugin update check job.
    /// </summary>
    /// <param name="forceSync">
    ///   Force sync even if not stale. If null, checks the configured schedule.
    /// </param>
    /// <param name="performUpgrade">
    ///   Whether to perform upgrades on enabled plugins. If null, uses settings default.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. Cancellation token.
    /// </param>
    /// <returns>
    ///   Task completing when the job is scheduled.
    /// </returns>
    Task ScheduleCheckForUpdates(bool? forceSync = null, bool? performUpgrade = null, CancellationToken cancellationToken = default);

    #endregion
}
