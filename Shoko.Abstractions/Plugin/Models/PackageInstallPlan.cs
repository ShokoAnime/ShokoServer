using System.Collections.Generic;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
///   What installing a package takes: the package, every dependency it needs
///   that is not installed yet, and why it cannot be installed, if it cannot.
///   Made by <see cref="IPluginPackageManager.PlanInstall"/>.
/// </summary>
public sealed class PackageInstallPlan
{
    /// <summary>
    ///   The package asked for.
    /// </summary>
    public required PackageInfo Package { get; init; }

    /// <summary>
    ///   The packages to install, dependencies first and the package asked for
    ///   last.
    /// </summary>
    public required IReadOnlyList<PackageInstallStep> Steps { get; init; }

    /// <summary>
    ///   Why the plan cannot be carried out. Empty when it can.
    /// </summary>
    public required IReadOnlyList<PackageInstallProblem> Problems { get; init; }

    /// <summary>
    ///   The optional dependencies that are not installed and were not asked
    ///   for, with the package that would be installed for each, if any.
    /// </summary>
    public required IReadOnlyList<PackageInstallOptionalDependency> OptionalDependencies { get; init; }

    /// <summary>
    ///   Indicates the plan can be carried out.
    /// </summary>
    public bool CanInstall => Problems.Count is 0;
}
