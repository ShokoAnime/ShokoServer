using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.Utilities;

namespace Shoko.Server.Plugin;

/// <summary>
///   Works out what installing a package takes: the dependencies to install
///   with it, picked from the package manifests, and why it cannot be done.
/// </summary>
internal static class PackageInstallPlanner
{
    #region Plan

    /// <summary>
    ///   Plans installing a package. Each required dependency is satisfied by
    ///   an installed version in its range, loaded or not, else by the newest
    ///   release in its range with an archive this server can load, preferring
    ///   the dependent's own repository, and its dependencies are planned in
    ///   turn. Optional dependencies are only planned when asked for.
    /// </summary>
    /// <param name="package">The package to install.</param>
    /// <param name="installed">Every installed plugin version.</param>
    /// <param name="manifests">The package manifests of the enabled repositories.</param>
    /// <param name="repositories">The repositories, in the order they are preferred.</param>
    /// <param name="isCompatible">Whether an archive's ABI version and runtime can load here.</param>
    /// <param name="runtimeIdentifier">This server's runtime, preferred over a universal archive.</param>
    /// <param name="includeOptional">Whether to plan the optional dependencies too.</param>
    /// <returns>The plan.</returns>
    internal static PackageInstallPlan Plan(
        PackageInfo package,
        IReadOnlyList<InstalledPlugin> installed,
        IReadOnlyList<PackageManifestInfo> manifests,
        IReadOnlyList<PackageRepositoryInfo> repositories,
        Func<Version, string, bool> isCompatible,
        string runtimeIdentifier,
        bool includeOptional = false
    )
    {
        var context = new Context(installed, manifests, repositories, isCompatible, runtimeIdentifier, includeOptional);
        context.Planned[package.Manifest.PackageID] = new(package, false, []);
        context.Visit(package);
        context.Order.Add(package.Manifest.PackageID);

        return new()
        {
            Package = package,
            Steps = context.Order
                .Select(id => context.Planned[id])
                .Select(planned => new PackageInstallStep { Package = planned.Package, IsDependency = planned.IsDependency, RequiredBy = planned.RequiredBy })
                .ToList(),
            Problems = context.Problems,
            OptionalDependencies = context.Optional,
        };
    }

    #endregion

    #region Context

    /// <summary>
    ///   A package the plan installs, with the plugins that need it.
    /// </summary>
    private sealed record Planned(PackageInfo Package, bool IsDependency, List<Guid> RequiredBy);

    private sealed class Context(
        IReadOnlyList<InstalledPlugin> installed,
        IReadOnlyList<PackageManifestInfo> manifests,
        IReadOnlyList<PackageRepositoryInfo> repositories,
        Func<Version, string, bool> isCompatible,
        string runtimeIdentifier,
        bool includeOptional
    )
    {
        private readonly SemverVersionComparer _comparer = new();

        private readonly Dictionary<Guid, List<InstalledPlugin>> _installed = installed.GroupBy(plugin => plugin.ID).ToDictionary(group => group.Key, group => group.ToList());

        private readonly Dictionary<Guid, PackageManifestInfo> _manifests = manifests.GroupBy(manifest => manifest.PackageID).ToDictionary(group => group.Key, group => group.First());

        private readonly HashSet<Guid> _visiting = [];

        public Dictionary<Guid, Planned> Planned { get; } = [];

        public List<Guid> Order { get; } = [];

        public List<PackageInstallProblem> Problems { get; } = [];

        public List<PackageInstallOptionalDependency> Optional { get; } = [];

        /// <summary>
        ///   Plans the dependencies of a package, each one ahead of the
        ///   package needing it.
        /// </summary>
        public void Visit(PackageInfo package)
        {
            var packageID = package.Manifest.PackageID;
            _visiting.Add(packageID);
            foreach (var dependency in package.Release.Dependencies)
            {
                if (dependency.PluginID == packageID)
                    continue;

                if (dependency.IsOptional && !includeOptional)
                {
                    if (!IsSatisfied(dependency))
                        Optional.Add(new()
                        {
                            PluginID = dependency.PluginID,
                            Name = GetName(dependency.PluginID),
                            VersionRange = dependency.VersionRange,
                            RequiredBy = packageID,
                            Package = PickRelease(dependency, package.Repository?.ID),
                        });
                    continue;
                }

                if (Planned.TryGetValue(dependency.PluginID, out var planned))
                {
                    if (_visiting.Contains(dependency.PluginID))
                        AddProblem(dependency, packageID, "It is part of a dependency cycle.");
                    else if (!PluginVersionRange.IsSatisfied(dependency.VersionRange, PluginPackageManager.NormalizeVersion(planned.Package.Release.Version)))
                        AddProblem(dependency, packageID, $"The plan installs version {planned.Package.Release.Version}, which does not satisfy \"{dependency.VersionRange}\".");
                    else if (!planned.RequiredBy.Contains(packageID))
                        planned.RequiredBy.Add(packageID);
                    continue;
                }

                var versions = _installed.GetValueOrDefault(dependency.PluginID) ?? [];
                if (versions.FirstOrDefault(version => version.IsPinned) is { } pinned && !PluginVersionRange.IsSatisfied(dependency.VersionRange, pinned.Version))
                {
                    AddProblem(dependency, packageID, $"It is pinned at version {pinned.Version}, which does not satisfy \"{dependency.VersionRange}\".");
                    continue;
                }

                if (versions.Any(version => PluginVersionRange.IsSatisfied(dependency.VersionRange, version.Version)))
                {
                    if (versions.All(version => !version.IsEnabled && !version.IsEnabledOnDemand))
                        AddProblem(dependency, packageID, "It is installed but disabled.");
                    continue;
                }

                if (PickRelease(dependency, package.Repository?.ID) is not { } release)
                {
                    AddProblem(dependency, packageID, !_manifests.ContainsKey(dependency.PluginID)
                        ? "It is not in any enabled repository."
                        : $"No release in \"{dependency.VersionRange}\" can load on this server.");
                    continue;
                }

                Planned[dependency.PluginID] = new(release, true, [packageID]);
                Visit(release);
                Order.Add(dependency.PluginID);
            }

            _visiting.Remove(packageID);
        }

        /// <summary>
        ///   Whether an installed or planned version satisfies a dependency.
        /// </summary>
        private bool IsSatisfied(PluginDependency dependency)
            => Planned.TryGetValue(dependency.PluginID, out var planned)
                ? PluginVersionRange.IsSatisfied(dependency.VersionRange, PluginPackageManager.NormalizeVersion(planned.Package.Release.Version))
                : (_installed.GetValueOrDefault(dependency.PluginID) ?? []).Any(version => PluginVersionRange.IsSatisfied(dependency.VersionRange, version.Version));

        /// <summary>
        ///   Picks the release to install for a dependency: the newest one in
        ///   its range with an archive this server can load, from the
        ///   dependent's own repository when two list the same version.
        /// </summary>
        private PackageInfo? PickRelease(PluginDependency dependency, Guid? preferredRepositoryID)
        {
            if (!_manifests.TryGetValue(dependency.PluginID, out var manifest))
                return null;

            var candidates = manifest.Releases
                .Where(release => PluginVersionRange.IsSatisfied(dependency.VersionRange, PluginPackageManager.NormalizeVersion(release.Version)))
                .OrderByDescending(release => release.Version, _comparer)
                .ThenByDescending(release => release.RepositoryID == preferredRepositoryID)
                .ThenBy(release => repositories.ToList().FindIndex(repository => repository.ID == release.RepositoryID) is var index and >= 0 ? index : int.MaxValue);
            foreach (var release in candidates)
            {
                var archive = release.Archives
                    .Where(archive => isCompatible(archive.AbstractionVersion, archive.RuntimeIdentifier))
                    .OrderByDescending(archive => archive.RuntimeIdentifier == runtimeIdentifier)
                    .ThenByDescending(archive => archive.AbstractionVersion, _comparer)
                    .FirstOrDefault();
                if (archive is null)
                    continue;

                return new()
                {
                    Manifest = manifest,
                    Repository = repositories.FirstOrDefault(repository => repository.ID == release.RepositoryID),
                    Archive = archive,
                    Plugin = null,
                    Release = release,
                };
            }

            return null;
        }

        private string? GetName(Guid pluginID)
            => _manifests.TryGetValue(pluginID, out var manifest) ? manifest.Name : _installed.GetValueOrDefault(pluginID)?.FirstOrDefault()?.Name;

        private void AddProblem(PluginDependency dependency, Guid requiredBy, string reason)
            => Problems.Add(new()
            {
                PluginID = dependency.PluginID,
                Name = GetName(dependency.PluginID),
                VersionRange = dependency.VersionRange,
                RequiredBy = requiredBy,
                Reason = reason,
            });
    }

    #endregion
}
