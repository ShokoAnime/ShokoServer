using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Web.Attributes;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Plugin;
using Shoko.Server.API.v3.Models.Plugin.Input;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using AbstractPackageInfo = Shoko.Abstractions.Plugin.Models.PackageInfo;
using PluginInfo = Shoko.Server.API.v3.Models.Plugin.PluginInfo;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// Controller responsible for managing plugin packages, repositories, and installations.
/// Interacts with the <see cref="IPluginPackageManager"/>.
/// </summary>
/// <param name="settingsProvider">Settings provider.</param>
/// <param name="packageManager">Plugin installation manager.</param>
[ApiController]
[Route("/api/v{version:apiVersion}/Plugin/Package")]
[ApiV3]
[Authorize(Roles = "admin,init")]
[DatabaseBlockedExempt]
[InitFriendly]
public class PluginPackageController(
    ISettingsProvider settingsProvider,
    IPluginPackageManager packageManager
) : BaseController(settingsProvider)
{
    #region Package Listing

    /// <summary>
    ///   Gets all available package manifests across all synced repositories.
    /// </summary>
    /// <param name="query">
    ///   An optional query to filter packages by name.
    /// </param>
    /// <param name="onlyCompatible">
    ///   Whether to return only ABI- and runtime-compatible packages.
    ///   Defaults to <c>true</c>.
    /// </param>
    /// <param name="onlyLatest">
    ///   Whether to return only the latest version of each package.
    ///   Defaults to <c>true</c>.
    /// </param>
    /// <param name="allowSync">
    ///   Whether to sync repositories before retrieving packages.
    ///   Defaults to <c>false</c>.
    /// </param>
    /// <param name="forceSyncNow">
    ///   Whether to forcefully sync the repositories.
    ///   Defaults to <c>false</c>.
    /// </param>
    /// <param name="pageSize">
    ///     The page size. Set to <code>0</code> to disable pagination.
    /// </param>
    /// <param name="page">
    ///   The page index.
    /// </param>
    /// <returns>
    ///   A list of <see cref="PackageInfo"/> for all available packages.
    /// </returns>
    [HttpGet]
    public async Task<ActionResult<ListResult<PackageInfo>>> GetAvailablePackages(
        [FromQuery] string? query = null,
        [FromQuery] bool onlyCompatible = true,
        [FromQuery] bool onlyLatest = true,
        [FromQuery] bool allowSync = false,
        [FromQuery] bool forceSyncNow = false,
        [FromQuery, Range(0, 1000)] int pageSize = 20,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var manifests = await packageManager.GetAvailablePackageManifests(
            allowSync: allowSync,
            forceSyncNow: forceSyncNow
        ).ConfigureAwait(false);

        var packages = packageManager.FilterPackageManifests(
            manifests,
            onlyCompatible: onlyCompatible,
            onlyLatest: onlyLatest
        );

        if (!string.IsNullOrEmpty(query))
            packages = packages
                .Search(query, p => [p.Manifest.Name, p.Manifest.Overview, .. p.Manifest.Tags])
                .Select(p => p.Result);

        return packages.ToListResult(p => new PackageInfo(p), page, pageSize);
    }

    /// <summary>
    ///   Gets all locally installed packages as <see cref="PackageInfo"/>.
    /// </summary>
    /// <param name="query">
    ///   An optional query to filter packages by name.
    /// </param>
    /// <param name="pageSize">
    ///     The page size. Set to <code>0</code> to disable pagination.
    /// </param>
    /// <param name="page">
    ///   The page index.
    /// </param>
    /// <returns>
    ///   A list of all packages installed locally in the system.
    /// </returns>
    [HttpGet("Local")]
    public ActionResult<ListResult<PackageInfo>> GetLocalPackages(
        [FromQuery] string? query = null,
        [FromQuery, Range(0, 1000)] int pageSize = 20,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var packages = (IEnumerable<AbstractPackageInfo>)packageManager.GetLocalPackages();

        if (!string.IsNullOrEmpty(query))
            packages = packages
                .Search(query, p => [p.Manifest.Name, p.Manifest.Overview, .. p.Manifest.Tags])
                .Select(p => p.Result);

        return packages.ToListResult(p => new PackageInfo(p), page, pageSize);
    }

    /// <summary>
    ///   Gets all packages installed since the server startup.
    /// </summary>
    /// <returns>
    ///   A list of the packages installed since the server startup till now.
    /// </returns>
    [HttpGet("Installed")]
    public ActionResult<List<PackageInfo>> GetInstalledPackages()
        => packageManager.GetInstalledPackages()
            .Select(p => new PackageInfo(p))
            .ToList();

    /// <summary>
    ///   Gets all available versions for a package ID.
    /// </summary>
    /// <param name="packageID">
    ///   The package ID.
    /// </param>
    /// <param name="onlyCompatible">
    ///   Whether to return only ABI- and runtime-compatible packages.
    ///   Defaults to <c>true</c>.
    /// </param>
    /// <param name="onlyLatest">
    ///   Whether to return only the latest version of each package.
    ///   Defaults to <c>true</c>.
    /// </param>
    /// <param name="allowSync">
    ///   Whether to sync repositories before retrieving the package.
    ///   Defaults to <c>false</c>.
    /// </param>
    /// <returns>
    ///   A list of <see cref="PackageInfo"/> for all available packages.
    /// </returns>
    [HttpGet("{packageID}")]
    public async Task<ActionResult<List<PackageInfo>>> GetAvailablePackagesForPackageID(
        [FromRoute] Guid packageID,
        [FromQuery] bool onlyCompatible = true,
        [FromQuery] bool onlyLatest = true,
        [FromQuery] bool allowSync = false
    )
    {
        var manifests = await packageManager.GetAvailablePackageManifests(
            allowSync: allowSync,
            forceSyncNow: false
        ).ConfigureAwait(false);
        var manifest = manifests.FirstOrDefault(m => m.PackageID == packageID);
        if (manifest is null)
            return NotFound("Package not found");

        var packages = packageManager.FilterPackageManifests(
            [manifest],
            onlyCompatible: onlyCompatible,
            onlyLatest: onlyLatest
        );

        return packages
            .Select(p => new PackageInfo(p))
            .ToList();
    }

    /// <summary>
    ///   Gets a single package manifest by its ID.
    /// </summary>
    /// <param name="packageID">
    ///   The package ID.
    /// </param>
    /// <param name="allowSync">
    ///   Whether to sync repositories before retrieving the package.
    ///   Defaults to <c>false</c>.
    /// </param>
    /// <returns>
    ///   The <see cref="PackageManifestInfo"/> if found.
    /// </returns>
    [HttpGet("{packageID}/Manifest")]
    public async Task<ActionResult<PackageManifestInfo>> GetManifestByPackageID(
        [FromRoute] Guid packageID,
        [FromQuery] bool allowSync = false
    )
    {
        var manifests = await packageManager.GetAvailablePackageManifests(
            allowSync: allowSync,
            forceSyncNow: false
        ).ConfigureAwait(false);

        var manifest = manifests.FirstOrDefault(m => m.PackageID == packageID);
        if (manifest is null)
            return NotFound("Package not found");

        return new PackageManifestInfo(manifest);
    }

    /// <summary>
    ///   Gets the thumbnail for a package by its ID.
    /// </summary>
    /// <param name="packageID">
    ///   The package ID.
    /// </param>
    /// <returns>
    ///   The thumbnail image if available.
    /// </returns>
    [HttpGet("{packageID}/Thumbnail")]
    public async Task<ActionResult> GetThumbnailForManifestByPackageID([FromRoute] Guid packageID)
    {
        var manifests = await packageManager.GetAvailablePackageManifests(allowSync: false).ConfigureAwait(false);
        var manifest = manifests.FirstOrDefault(m => m.PackageID == packageID);
        if (manifest?.Thumbnail is null)
            return NotFound("Package or thumbnail not found");

        return File(manifest.Thumbnail.GetStream(ApplicationPaths.Instance), manifest.Thumbnail.MimeType);
    }

    #endregion

    #region Package Installation

    /// <summary>
    ///   Installs a package ID.
    /// </summary>
    /// <param name="packageID">
    ///   The package ID.
    /// </param>
    /// <param name="releaseVersion">
    ///   The specific release version to install.
    /// </param>
    /// <param name="abstractionVersion">
    ///   The specific abstraction version to install.
    /// </param>
    /// <param name="runtimeIdentifier">
    ///   The specific runtime identifier (e.g., "linux-x64", "win-x64", "any").
    /// </param>
    /// <returns>
    ///   The installed <see cref="PluginInfo"/> if successful.
    /// </returns>
    [HttpPost("{packageID}/Install")]
    public async Task<ActionResult<PluginInfo>> InstallPackageByID(
        [FromRoute] Guid packageID,
        [FromQuery] string? runtimeIdentifier = null,
        [FromQuery] string? abstractionVersion = null,
        [FromQuery] string? releaseVersion = null
    )
    {
        var manifests = await packageManager.GetAvailablePackageManifests(allowSync: false).ConfigureAwait(false);
        var manifest = manifests.FirstOrDefault(m => m.PackageID == packageID);
        if (manifest is null)
            return NotFound("Package not found");

        var parsedReleaseVersion = (Version?)null;
        if (releaseVersion is { Length: > 0 } && !Version.TryParse(releaseVersion?.Replace("-dev.", "."), out parsedReleaseVersion))
            ModelState.AddModelError(nameof(releaseVersion), "Version must be a valid semantic versioning string.");
        if (parsedReleaseVersion is { Revision: <= 0 })
            parsedReleaseVersion = new(parsedReleaseVersion.Major, parsedReleaseVersion.Minor, parsedReleaseVersion.Build);

        var parsedAbstractionVersion = (Version?)null;
        if (abstractionVersion is { Length: > 0 } && !Version.TryParse(abstractionVersion?.Replace("-dev.", "."), out parsedAbstractionVersion))
            ModelState.AddModelError(nameof(abstractionVersion), "Version must be a valid semantic versioning string.");
        if (parsedAbstractionVersion is { })
            parsedAbstractionVersion = new(parsedAbstractionVersion.Major, parsedAbstractionVersion.Minor, parsedAbstractionVersion.Build);

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var packages = packageManager.FilterPackageManifests(
            [manifest],
            onlyCompatible: runtimeIdentifier is not { Length: > 0 } && parsedAbstractionVersion is null,
            onlyLatest: parsedReleaseVersion is null
        );
        if (runtimeIdentifier is { Length: > 0 })
            packages = packages.Where(p => string.Equals(p.Archive.RuntimeIdentifier, runtimeIdentifier));
        if (parsedAbstractionVersion is not null)
            packages = packages.Where(p => p.Archive.AbstractionVersion == parsedAbstractionVersion);
        if (parsedReleaseVersion is not null)
            packages = packages.Where(p => p.Release.Version == parsedReleaseVersion);
        if (packages.FirstOrDefault() is not { } package)
            return ValidationProblem("Package not found or no compatible version available");

        var pluginInfo = await packageManager.InstallPackage(package).ConfigureAwait(false);
        if (pluginInfo is null)
            return StatusCode(500, "Failed to install package");

        return new PluginInfo(pluginInfo);
    }

    #endregion

    #region Repository Management

    /// <summary>
    ///   Lists all configured repositories.
    /// </summary>
    /// <returns>
    ///   A list of <see cref="PackageRepositoryInfo"/> for all repositories.
    /// </returns>
    [HttpGet("Repository")]
    public ActionResult<List<PackageRepositoryInfo>> ListRepositories()
        => packageManager.ListPackageRepositories()
            .Select(r => new PackageRepositoryInfo(r))
            .ToList();

    /// <summary>
    ///   Gets a specific repository by its ID.
    /// </summary>
    /// <param name="repositoryID">
    ///   The repository ID.
    /// </param>
    /// <returns>
    ///   The <see cref="PackageRepositoryInfo"/> if found.
    /// </returns>
    [HttpGet("Repository/{repositoryID}")]
    public ActionResult<PackageRepositoryInfo> GetRepositoryByID([FromRoute] Guid repositoryID)
    {
        var repository = packageManager.ListPackageRepositories()
            .FirstOrDefault(r => r.ID == repositoryID);
        if (repository is null)
            return NotFound("Repository not found");

        return new PackageRepositoryInfo(repository);
    }

    /// <summary>
    ///   Adds a new package repository.
    /// </summary>
    /// <param name="body">
    ///   The repository details.
    /// </param>
    /// <returns>
    ///   The newly added <see cref="PackageRepositoryInfo"/>.
    /// </returns>
    [HttpPost("Repository")]
    public async Task<ActionResult<PackageRepositoryInfo>> AddRepository(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] AddPackageRepositoryBody body
    )
    {
        try
        {
            var repository = await packageManager.AddPackageRepository(
                body.Name,
                body.Url,
                body.StaleTime
            ).ConfigureAwait(false);

            return new PackageRepositoryInfo(repository);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    ///   Removes a package repository.
    /// </summary>
    /// <param name="repositoryID">
    ///   The repository ID.
    /// </param>
    /// <returns>
    ///   No content if successful.
    /// </returns>
    [HttpDelete("Repository/{repositoryID}")]
    public async Task<ActionResult> RemoveRepository([FromRoute] Guid repositoryID)
    {
        var repository = packageManager.ListPackageRepositories()
            .FirstOrDefault(r => r.ID == repositoryID);
        if (repository is null)
            return NotFound("Repository not found");

        var removed = await packageManager.RemovePackageRepository(repository).ConfigureAwait(false);
        if (!removed)
            return BadRequest("Cannot remove the local repository");

        return NoContent();
    }

    /// <summary>
    ///   Syncs a specific repository.
    /// </summary>
    /// <param name="repositoryID">
    ///   The repository ID.
    /// </param>
    /// <param name="forceSync">
    ///   Whether to force sync even if the repository is not stale.
    ///   Defaults to <c>true</c>.
    /// </param>
    /// <returns>
    ///   The updated <see cref="PackageRepositoryInfo"/>.
    /// </returns>
    [HttpPost("Repository/{repositoryID}/Sync")]
    public async Task<ActionResult<PackageRepositoryInfo>> SyncRepository(
        [FromRoute] Guid repositoryID,
        [FromQuery] bool forceSync = true
    )
    {
        var repository = packageManager.ListPackageRepositories()
            .FirstOrDefault(r => r.ID == repositoryID);
        if (repository is null)
            return NotFound("Repository not found");

        var updatedRepository = await packageManager.SyncPackageRepository(
            repository,
            forceSync: forceSync
        ).ConfigureAwait(false);

        return new PackageRepositoryInfo(updatedRepository);
    }

    /// <summary>
    ///   Syncs all repositories.
    /// </summary>
    /// <param name="forceSync">
    ///   Whether to force sync even if the repositories are not stale.
    ///   Defaults to <c>true</c>.
    /// </param>
    /// <returns>
    ///   A list of all <see cref="PackageRepositoryInfo"/>s after sync.
    /// </returns>
    [HttpPost("Repository/Sync")]
    public async Task<ActionResult<List<PackageRepositoryInfo>>> SyncAllRepositories(
        [FromQuery] bool forceSync = true
    )
    {
        await packageManager.SyncAllPackageRepositories(forceSync: forceSync).ConfigureAwait(false);

        return packageManager.ListPackageRepositories()
            .Select(r => new PackageRepositoryInfo(r))
            .ToList();
    }

    #endregion

    #region Update Checking

    /// <summary>
    ///   Checks for plugin updates and optionally performs upgrades on enabled plugins.
    /// </summary>
    /// <param name="body">
    ///   Optional body containing force sync and perform upgrade options.
    /// </param>
    /// <returns>
    ///   No content when successful.
    /// </returns>
    [HttpPost("CheckForUpdates")]
    public async Task<ActionResult> CheckForUpdates(
        [FromBody] CheckForUpdatesBody? body = null
    )
    {
        await packageManager.CheckForUpdates(
            forceSync: body?.ForceSync,
            performUpgrade: body?.PerformUpgrade
        ).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///   Schedules a plugin update check job.
    /// </summary>
    /// <param name="body">
    ///   Optional body containing force sync and perform upgrade options.
    /// </param>
    /// <returns>
    ///   No content when the job is scheduled.
    /// </returns>
    [HttpPost("ScheduleCheckForUpdates")]
    public async Task<ActionResult> ScheduleCheckForUpdates(
        [FromBody] CheckForUpdatesBody? body = null
    )
    {
        await packageManager.ScheduleCheckForUpdates(
            forceSync: body?.ForceSync,
            performUpgrade: body?.PerformUpgrade
        ).ConfigureAwait(false);

        return NoContent();
    }

    #endregion
}
