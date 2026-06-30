using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;

using AbstractPackageArchiveInfo = Shoko.Abstractions.Plugin.Models.PackageArchiveInfo;
using AbstractPackageReleaseInfo = Shoko.Abstractions.Plugin.Models.PackageReleaseInfo;

namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
/// Information about a package archive file for download.
/// </summary>
public class PackageArchiveInfo(AbstractPackageReleaseInfo releaseInfo, AbstractPackageArchiveInfo archiveInfo, IReadOnlyList<LocalPluginInfo> pluginInfoList, IPluginManager pluginManager)
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
    ///   Indicates if the archive is installed on the system.
    /// </summary>
    [Required]
    public bool IsInstalled { get; init; } = (
        releaseInfo.SourceRevision is { Length: > 0 } a
        ? pluginInfoList.FirstOrDefault(p =>
            p.Version.SourceRevision is { Length: > 0 } b && string.Equals(a, b) &&
            p.Version.AbstractionVersion == archiveInfo.AbstractionVersion &&
            p.Version.RuntimeIdentifier == archiveInfo.RuntimeIdentifier
        )
        : pluginInfoList.FirstOrDefault(p =>
            p.Version.Version == releaseInfo.Version &&
            p.Version.AbstractionVersion == archiveInfo.AbstractionVersion &&
            p.Version.RuntimeIdentifier == archiveInfo.RuntimeIdentifier
        ) ?? pluginInfoList.FirstOrDefault(p =>
            p.Version.Version == releaseInfo.Version &&
            p.Version.AbstractionVersion == archiveInfo.AbstractionVersion &&
            p.Version.RuntimeIdentifier == IPluginManager.AnyRuntimeIdentifier
        )
    ) is not null;

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
