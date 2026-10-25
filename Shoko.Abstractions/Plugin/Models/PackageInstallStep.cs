using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
///   A package an install plan installs.
/// </summary>
public sealed class PackageInstallStep
{
    /// <summary>
    ///   The package, release and archive to install.
    /// </summary>
    public required PackageInfo Package { get; init; }

    /// <summary>
    ///   Indicates the package is installed as a dependency, rather than
    ///   being the package asked for.
    /// </summary>
    public required bool IsDependency { get; init; }

    /// <summary>
    ///   The plugins in the plan that need this package. Empty for the
    ///   package asked for.
    /// </summary>
    public required IReadOnlyList<Guid> RequiredBy { get; init; }
}
