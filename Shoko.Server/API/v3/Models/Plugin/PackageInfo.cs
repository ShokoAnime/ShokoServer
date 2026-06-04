using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;

using AbstractPackageInfo = Shoko.Abstractions.Plugin.Models.PackageInfo;

#nullable enable
namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
/// A package with version information for installation.
/// </summary>
public class PackageInfo(AbstractPackageInfo packageInfo, IReadOnlyList<LocalPluginInfo> pluginInfoList, IPluginManager pluginManager)
{
    /// <summary>
    ///   The repository this package came from.
    /// </summary>
    public PackageRepositoryInfo? Repository { get; init; } = packageInfo.Repository is null
        ? null
        : new PackageRepositoryInfo(packageInfo.Repository);

    /// <summary>
    ///   The package metadata.
    /// </summary>
    [Required]
    public PackageManifestInfo Manifest { get; init; } = new(packageInfo.Manifest, pluginInfoList, pluginManager, includeReleases: false);

    /// <summary>
    ///   The specific version to install.
    /// </summary>
    [Required]
    public PackageReleaseInfo Release { get; init; } = new(packageInfo.Release, pluginInfoList, pluginManager, includeArchives: false);

    /// <summary>
    ///   The archive information for downloading the package.
    /// </summary>
    [Required]
    public PackageArchiveInfo Archive { get; init; } = new(packageInfo.Release, packageInfo.Archive, pluginInfoList, pluginManager);

    /// <summary>
    ///   The installed plugin info if this package is installed locally,
    ///   otherwise <see langword="null"/>.
    /// </summary>
    public PluginInfo? Plugin { get; init; } = packageInfo.Plugin is null
        ? null
        : new PluginInfo(packageInfo.Plugin);
}
