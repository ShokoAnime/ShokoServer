using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shoko.Abstractions.Core;

using AbstractPackageReleaseInfo = Shoko.Abstractions.Plugin.Models.PackageReleaseInfo;

#nullable enable
namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
/// Information about a package release.
/// </summary>
public class PackageReleaseInfo(AbstractPackageReleaseInfo releaseInfo)
{
    /// <summary>
    ///   Unique package repository identifier for the release.
    /// </summary>
    [Required]
    public Guid RepositoryID { get; init; } = releaseInfo.RepositoryID;

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
    [Required]
    public IReadOnlyList<PackageArchiveInfo> Archives { get; init; } = releaseInfo.Archives
        .Select(a => new PackageArchiveInfo(a))
        .ToList();
}
