using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;

using AbstractPackageInstallPlan = Shoko.Abstractions.Plugin.Models.PackageInstallPlan;

namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
///   What installing a package takes: the packages to install, dependencies
///   first, and why it cannot be done, if it cannot.
/// </summary>
public class PackageInstallPlan(AbstractPackageInstallPlan plan, IPluginManager pluginManager)
{
    /// <summary>
    ///   Indicates the plan can be carried out.
    /// </summary>
    [Required]
    public bool CanInstall { get; init; } = plan.CanInstall;

    /// <summary>
    ///   The packages to install, dependencies first and the package asked for
    ///   last.
    /// </summary>
    [Required]
    public IReadOnlyList<Step> Steps { get; init; } = plan.Steps
        .Select(step => new Step(step, pluginManager))
        .ToList();

    /// <summary>
    ///   Why the plan cannot be carried out. Empty when it can.
    /// </summary>
    [Required]
    public IReadOnlyList<Problem> Problems { get; init; } = plan.Problems
        .Select(problem => new Problem(problem))
        .ToList();

    /// <summary>
    ///   The optional dependencies that are not installed and were not asked
    ///   for.
    /// </summary>
    [Required]
    public IReadOnlyList<OptionalDependency> OptionalDependencies { get; init; } = plan.OptionalDependencies
        .Select(dependency => new OptionalDependency(dependency, pluginManager))
        .ToList();

    /// <summary>
    ///   A package the plan installs.
    /// </summary>
    public class Step(PackageInstallStep step, IPluginManager pluginManager)
    {
        /// <summary>
        ///   The package, release and archive to install.
        /// </summary>
        [Required]
        public PackageInfo Package { get; init; } = new(step.Package, pluginManager.GetPluginInfos(step.Package.Manifest.PackageID), pluginManager);

        /// <summary>
        ///   Indicates the package is installed as a dependency, rather than
        ///   being the package asked for.
        /// </summary>
        [Required]
        public bool IsDependency { get; init; } = step.IsDependency;

        /// <summary>
        ///   The plugins in the plan that need this package.
        /// </summary>
        [Required]
        public IReadOnlyList<Guid> RequiredBy { get; init; } = step.RequiredBy;
    }

    /// <summary>
    ///   Why the plan cannot be carried out.
    /// </summary>
    public class Problem(PackageInstallProblem problem)
    {
        /// <summary>
        ///   The plugin the problem is with.
        /// </summary>
        [Required]
        public Guid PluginID { get; init; } = problem.PluginID;

        /// <summary>
        ///   The plugin's name, if it is known.
        /// </summary>
        public string? Name { get; init; } = problem.Name;

        /// <summary>
        ///   The version range asked for, when the problem is with a
        ///   dependency.
        /// </summary>
        public string? VersionRange { get; init; } = problem.VersionRange;

        /// <summary>
        ///   The plugin asking for it, when the problem is with a dependency.
        /// </summary>
        public Guid? RequiredBy { get; init; } = problem.RequiredBy;

        /// <summary>
        ///   What is wrong.
        /// </summary>
        [Required]
        public string Reason { get; init; } = problem.Reason;
    }

    /// <summary>
    ///   An optional dependency the plan leaves out.
    /// </summary>
    public class OptionalDependency(PackageInstallOptionalDependency dependency, IPluginManager pluginManager)
    {
        /// <summary>
        ///   The plugin depended on.
        /// </summary>
        [Required]
        public Guid PluginID { get; init; } = dependency.PluginID;

        /// <summary>
        ///   The plugin's name, if it is known.
        /// </summary>
        public string? Name { get; init; } = dependency.Name;

        /// <summary>
        ///   The version range asked for.
        /// </summary>
        [Required]
        public string VersionRange { get; init; } = dependency.VersionRange;

        /// <summary>
        ///   The plugin asking for it.
        /// </summary>
        [Required]
        public Guid RequiredBy { get; init; } = dependency.RequiredBy;

        /// <summary>
        ///   The package that would be installed for it, if one fits.
        /// </summary>
        public PackageInfo? Package { get; init; } = dependency.Package is null ? null : new(dependency.Package, pluginManager.GetPluginInfos(dependency.PluginID), pluginManager);
    }
}
