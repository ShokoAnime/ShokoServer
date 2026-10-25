using System;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
///   An optional dependency an install plan leaves out, as it is not
///   installed and was not asked for.
/// </summary>
public sealed class PackageInstallOptionalDependency
{
    /// <summary>
    ///   The plugin depended on.
    /// </summary>
    public required Guid PluginID { get; init; }

    /// <summary>
    ///   The plugin's name, if it is known.
    /// </summary>
    public required string? Name { get; init; }

    /// <summary>
    ///   The version range asked for.
    /// </summary>
    public required string VersionRange { get; init; }

    /// <summary>
    ///   The plugin asking for it.
    /// </summary>
    public required Guid RequiredBy { get; init; }

    /// <summary>
    ///   The package that would be installed for it, or <c>null</c> if none
    ///   in the enabled repositories fits.
    /// </summary>
    public required PackageInfo? Package { get; init; }
}
