using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Server.Plugin;

/// <summary>
///   Resolves plugin-to-plugin dependencies and provides dependency-graph
///   queries against the currently installed plugins.
/// </summary>
public class PluginDependencyResolver : IPluginDependencyResolver
{
    private readonly IPluginManager _pluginManager;

    public PluginDependencyResolver(IPluginManager pluginManager)
    {
        _pluginManager = pluginManager;
    }

    public DependencyResolutionResult ResolveDependencies(LocalPluginInfo plugin)
        => ResolveDependencies(plugin.Dependencies);

    public DependencyResolutionResult ResolveDependencies(IReadOnlyList<PluginDependency> dependencies)
    {
        var allPlugins = _pluginManager.GetPluginInfos();
        var resolved = new List<ResolvedDependency>(dependencies.Count);

        foreach (var dep in dependencies)
        {
            var (satisfied, matchingPlugin, message) = ResolveSingle(dep, allPlugins);
            resolved.Add(new ResolvedDependency
            {
                Dependency = dep,
                IsResolved = satisfied,
                Plugin = matchingPlugin,
                Message = message,
            });
        }

        var canResolve = resolved
            .Where(r => !r.Dependency.IsOptional)
            .All(r => r.IsResolved);

        return new DependencyResolutionResult
        {
            CanResolve = canResolve,
            Dependencies = resolved,
        };
    }

    public IReadOnlyList<LocalPluginInfo> GetDependents(LocalPluginInfo plugin)
    {
        var allPlugins = _pluginManager.GetPluginInfos()
            .Where(p => p.IsActive && p.IsInstalled)
            .ToList();

        var dependents = new List<LocalPluginInfo>();
        foreach (var candidate in allPlugins)
        {
            if (candidate.ID == plugin.ID)
                continue;

            foreach (var dep in candidate.Dependencies)
            {
                if (dep.PluginID == plugin.ID)
                {
                    dependents.Add(candidate);
                    break;
                }
            }
        }

        return dependents;
    }

    public bool IsVersionSatisfied(string versionRange, Version candidate)
        => PluginVersionRange.IsSatisfied(versionRange, candidate);

    #region Lifecycle Validation

    public DependencyResolutionResult ValidateEnable(LocalPluginInfo plugin)
        => ResolveDependencies(plugin.Dependencies);

    public IReadOnlyList<LocalPluginInfo> ValidateDisable(LocalPluginInfo plugin)
        => GetDependents(plugin);

    public IReadOnlyList<LocalPluginInfo> GetUninstallCascade(LocalPluginInfo plugin)
    {
        var allPlugins = _pluginManager.GetPluginInfos()
            .Where(p => p.IsActive && p.IsInstalled && p.ID != plugin.ID)
            .ToList();

        var cascade = new List<LocalPluginInfo>();
        var visited = new HashSet<Guid>();
        CollectDependents(plugin.ID, allPlugins, cascade, visited);
        return cascade;
    }

    private void CollectDependents(
        Guid pluginId,
        IReadOnlyList<LocalPluginInfo> allPlugins,
        List<LocalPluginInfo> cascade,
        HashSet<Guid> visited)
    {
        foreach (var candidate in allPlugins)
        {
            if (visited.Contains(candidate.ID))
                continue;

            if (candidate.Dependencies.Any(d => d.PluginID == pluginId))
            {
                visited.Add(candidate.ID);
                cascade.Add(candidate);
                // Recurse: dependents of this dependent may also be affected
                CollectDependents(candidate.ID, allPlugins, cascade, visited);
            }
        }
    }

    #endregion

    private (bool satisfied, LocalPluginInfo? plugin, string? message) ResolveSingle(
        PluginDependency dep,
        IReadOnlyList<LocalPluginInfo> allPlugins)
    {
        var versionRange = dep.VersionRange;

        // Find a matching installed plugin that is enabled and active.
        var matching = allPlugins.FirstOrDefault(p =>
            p.ID == dep.PluginID &&
            p.IsActive &&
            p.IsInstalled &&
            IsVersionSatisfied(versionRange, p.Version.Version));

        if (matching is not null)
            return (true, matching, null);

        // Check if any version is installed but disabled.
        var anyInstalled = allPlugins.FirstOrDefault(p =>
            p.ID == dep.PluginID && p.IsInstalled);

        if (anyInstalled is not null)
        {
            return (false, null,
                $"Plugin '{anyInstalled.Name}' ({anyInstalled.ID}) is installed but " +
                (anyInstalled.IsActive
                    ? $"version {anyInstalled.Version.Version} does not satisfy '{versionRange}'."
                    : "is disabled."));
        }

        return (false, null,
            $"No installed plugin found matching ID '{dep.PluginID}' with version '{versionRange}'.");
    }
}
