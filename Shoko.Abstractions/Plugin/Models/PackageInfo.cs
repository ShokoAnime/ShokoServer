
namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
///   A package with version information for installation.
/// </summary>
public sealed class PackageInfo
{
    /// <summary>
    ///   The repository this package came from.
    /// </summary>
    public required PackageRepositoryInfo? Repository { get; init; }

    /// <summary>
    ///   The package metadata.
    /// </summary>
    public required PackageManifestInfo Manifest { get; init; }

    /// <summary>
    ///   The specific version to install.
    /// </summary>
    public required PackageReleaseInfo Release { get; init; }

    /// <summary>
    ///   The archive information for downloading the package.
    /// </summary>
    public required PackageArchiveInfo Archive { get; init; }

    /// <summary>
    ///   The installed plugin info if this package is installed locally,
    ///   otherwise <see langword="null"/>.
    /// </summary>
    public required LocalPluginInfo? Plugin { get; init; }
}
