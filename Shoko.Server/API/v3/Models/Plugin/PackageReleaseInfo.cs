using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;

using AbstractPackageReleaseInfo = Shoko.Abstractions.Plugin.Models.PackageReleaseInfo;

namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
/// Information about a package release.
/// </summary>
public class PackageReleaseInfo(AbstractPackageReleaseInfo releaseInfo, IReadOnlyList<LocalPluginInfo> pluginInfoList, IPluginManager pluginManager, bool includeArchives = true)
{
    /// <summary>
    ///   Unique package repository identifier for the release.
    /// </summary>
    [Required]
    public Guid? RepositoryID { get; init; } = releaseInfo.RepositoryID;

    /// <summary>
    ///   Shared semantic version for all versions of the release. (e.g.,
    ///   "1.2.3").
    /// </summary>
    [Required]
    public Version Version { get; init; } = releaseInfo.Version;

    /// <summary>
    ///   The source revision tag of the release, if provided.
    /// </summary>
    public string? Tag { get; init; } = releaseInfo.Tag;

    /// <summary>
    ///   The SHA digest of the source revision used to build the release, if
    ///   provided.
    /// </summary>
    public string? SourceRevision { get; init; } = releaseInfo.SourceRevision;

    /// <summary>
    ///   Whether the release is installed.
    /// </summary>
    [Required]
    public bool IsInstalled { get; init; } = (
        releaseInfo.SourceRevision is { Length: > 0 } a
        ? pluginInfoList.FirstOrDefault(p =>
            p.Version.SourceRevision is { Length: > 0 } b && string.Equals(a, b) &&
            releaseInfo.Archives.Any(archiveInfo =>
                p.Version.AbstractionVersion == archiveInfo.AbstractionVersion &&
                p.Version.RuntimeIdentifier == archiveInfo.RuntimeIdentifier
            )
        )
        : pluginInfoList.FirstOrDefault(p =>
            p.Version.Version == releaseInfo.Version &&
            (
                releaseInfo.Archives.Any(archiveInfo =>
                    p.Version.AbstractionVersion == archiveInfo.AbstractionVersion &&
                    p.Version.RuntimeIdentifier == archiveInfo.RuntimeIdentifier
                ) ||
                releaseInfo.Archives.Any(archiveInfo =>
                    p.Version.AbstractionVersion == archiveInfo.AbstractionVersion &&
                    p.Version.RuntimeIdentifier == IPluginManager.AnyRuntimeIdentifier
                )
            )
        )
    ) is not null;

    /// <summary>
    ///   When the release was made.
    /// </summary>
    [Required]
    public DateTime ReleasedAt { get; init; } = releaseInfo.ReleasedAt;

    /// <summary>
    ///   The channel for the release.
    /// </summary>
    [Required]
    public ReleaseChannel Channel { get; init; } = releaseInfo.Channel;

    /// <summary>
    ///   Release notes, or <see langword="null"/> if not available for this
    ///   release.
    /// </summary>
    public string? ReleaseNotes { get; init; } = releaseInfo.ReleaseNotes;

    /// <summary>
    ///   Available archives for different runtime environments and
    ///   architectures.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<PackageArchiveInfo>? Archives { get; init; } = !includeArchives ? null : releaseInfo.Archives
        .Select(a => new PackageArchiveInfo(releaseInfo, a, pluginInfoList, pluginManager))
        .ToList();
}
