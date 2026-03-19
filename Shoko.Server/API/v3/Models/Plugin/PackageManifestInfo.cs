using System;
using System.Collections.Generic;
using System.Linq;

using AbstractPackageManifestInfo = Shoko.Abstractions.Plugin.Models.PackageManifestInfo;

#nullable enable
namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
/// Information about a package manifest.
/// </summary>
public class PackageManifestInfo(AbstractPackageManifestInfo manifestInfo)
{
    /// <summary>
    ///   Unique package identifier.
    /// </summary>
    public Guid PackageID { get; init; } = manifestInfo.PackageID;

    /// <summary>
    ///   Package display name.
    /// </summary>
    public string Name { get; init; } = manifestInfo.Name;

    /// <summary>
    ///   Describes what the package does at a high level.
    /// </summary>
    public string Overview { get; init; } = manifestInfo.Overview;

    /// <summary>
    ///   The author(s) of the package and plugins contained within it.
    /// </summary>
    public string Authors { get; init; } = manifestInfo.Authors;

    /// <summary>
    ///   Search tags.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = manifestInfo.Tags;

    /// <summary>
    ///   The thumbnail for the plugin, if it's available.
    /// </summary>
    public PackageThumbnailInfo? Thumbnail { get; init; } = manifestInfo.Thumbnail is null
        ? null
        : new PackageThumbnailInfo(manifestInfo.Thumbnail);

    /// <summary>
    ///   Available releases from the manifest.
    /// </summary>
    public IReadOnlyList<PackageReleaseInfo> Releases { get; init; } = manifestInfo.Releases
        .Select(r => new PackageReleaseInfo(r))
        .ToList();

    /// <summary>
    ///   When this manifest was last fetched.
    /// </summary>
    public DateTime LastFetchedAt { get; init; } = manifestInfo.LastFetchedAt;
}
