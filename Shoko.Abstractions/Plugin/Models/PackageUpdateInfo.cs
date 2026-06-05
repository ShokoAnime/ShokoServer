using System;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
///   Pairs a locally installed package with the latest available compatible
///   release, indicating an update is available.
/// </summary>
public sealed class PackageUpdateInfo
{
    /// <summary>
    ///   The unique identifier of the package.
    /// </summary>
    public required Guid PackageID { get; init; }

    /// <summary>
    ///   The display name of the package.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   The currently installed package.
    /// </summary>
    public required PackageInfo Current { get; init; }

    /// <summary>
    ///   The latest available compatible package to update to.
    /// </summary>
    public required PackageInfo Latest { get; init; }
}
