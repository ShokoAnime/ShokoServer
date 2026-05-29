using System;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Plugin;

using AbstractPackageArchiveInfo = Shoko.Abstractions.Plugin.Models.PackageArchiveInfo;

#nullable enable
namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
/// Information about a package archive file for download.
/// </summary>
public class PackageArchiveInfo(AbstractPackageArchiveInfo archiveInfo, IPluginManager pluginManager)
{
    /// <summary>
    ///   Runtime identifier for the version. Will be <c>"any"</c> for universal
    ///   packages.
    /// </summary>
    [Required]
    public string RuntimeIdentifier { get; init; } = archiveInfo.RuntimeIdentifier;

    /// <summary>
    ///   Semantic version for minimum ABI version required to run this version.
    /// </summary>
    [Required]
    public Version AbstractionVersion { get; init; } = archiveInfo.AbstractionVersion;

    /// <summary>
    ///   Indicates if the archive is ABI and runtime compatible with the current system.
    /// </summary>
    [Required]
    public bool IsCompatible { get; init; } = pluginManager.IsAbiAndRuntimeCompatible(archiveInfo.AbstractionVersion, archiveInfo.RuntimeIdentifier);

    /// <summary>
    ///   Download URL for the package's archive.
    /// </summary>
    [Required]
    public string ArchiveUrl { get; init; } = archiveInfo.ArchiveUrl;

    /// <summary>
    ///   SHA256 checksum for integrity verification.
    /// </summary>
    [Required]
    public string ArchiveChecksum { get; init; } = archiveInfo.ArchiveChecksum;
}
