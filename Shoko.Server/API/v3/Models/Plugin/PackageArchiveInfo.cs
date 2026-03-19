using System;

using AbstractPackageArchiveInfo = Shoko.Abstractions.Plugin.Models.PackageArchiveInfo;

#nullable enable
namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
/// Information about a package archive file for download.
/// </summary>
public class PackageArchiveInfo(AbstractPackageArchiveInfo archiveInfo)
{
    /// <summary>
    ///   Runtime identifier for the version. Will be <c>"any"</c> for universal
    ///   packages.
    /// </summary>
    public string RuntimeIdentifier { get; init; } = archiveInfo.RuntimeIdentifier;

    /// <summary>
    ///   Semantic version for minimum ABI version required to run this version.
    /// </summary>
    public Version AbstractionVersion { get; init; } = archiveInfo.AbstractionVersion;

    /// <summary>
    ///   Download URL for the package's archive.
    /// </summary>
    public string ArchiveUrl { get; init; } = archiveInfo.ArchiveUrl;

    /// <summary>
    ///   SHA256 checksum for integrity verification.
    /// </summary>
    public string ArchiveChecksum { get; init; } = archiveInfo.ArchiveChecksum;
}
