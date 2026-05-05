using System.ComponentModel.DataAnnotations;

using AbstractPackageInfo = Shoko.Abstractions.Plugin.Models.PackageInfo;

#nullable enable
namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
/// A package with version information for installation.
/// </summary>
public class PackageInfo(AbstractPackageInfo packageInfo)
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
    public PackageManifestInfo Manifest { get; init; } = new(packageInfo.Manifest);

    /// <summary>
    ///   The specific version to install.
    /// </summary>
    [Required]
    public PackageReleaseInfo Release { get; init; } = new(packageInfo.Release);

    /// <summary>
    ///   The archive information for downloading the package.
    /// </summary>
    [Required]
    public PackageArchiveInfo Archive { get; init; } = new(packageInfo.Archive);

    /// <summary>
    ///   The installed plugin info if this package is installed locally,
    ///   otherwise <see langword="null"/>.
    /// </summary>
    public PluginInfo? Plugin { get; init; } = packageInfo.Plugin is null
        ? null
        : new PluginInfo(packageInfo.Plugin);
}
