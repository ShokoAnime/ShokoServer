using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Plugin;

/// <summary>
///   Result of resolving a single plugin dependency against installed plugins.
/// </summary>
public sealed class ResolvedDependency
{
    /// <summary>
    ///   The dependency declaration from the plugin manifest or metadata.
    /// </summary>
    public required PluginDependency Dependency { get; init; }

    /// <summary>
    ///   Whether a compatible installed plugin was found.
    /// </summary>
    public required bool IsResolved { get; init; }

    /// <summary>
    ///   The installed plugin that satisfies the dependency, if resolved.
    /// </summary>
    public LocalPluginInfo? Plugin { get; init; }

    /// <summary>
    ///   A human-readable message describing the resolution result (e.g. the
    ///   reason resolution failed).
    /// </summary>
    public string? Message { get; init; }
}

/// <summary>
///   Result of resolving all dependencies for a plugin.
/// </summary>
public sealed class DependencyResolutionResult
{
    /// <summary>
    ///   Whether all required (non-optional) dependencies can be satisfied.
    /// </summary>
    public required bool CanResolve { get; init; }

    /// <summary>
    ///   The per-dependency resolution results.
    /// </summary>
    public required IReadOnlyList<ResolvedDependency> Dependencies { get; init; }

    /// <summary>
    ///   Any plugins that need to be installed (or enabled) for resolution.
    /// </summary>
    public IReadOnlyList<PluginDependency> MissingDependencies => Dependencies
        .Where(d => !d.Dependency.IsOptional && !d.IsResolved)
        .Select(d => d.Dependency)
        .ToArray();
}

/// <summary>
///   Resolves plugin-to-plugin dependencies and provides dependency-graph
///   queries.
/// </summary>
public interface IPluginDependencyResolver
{
    /// <summary>
    ///   Resolve all dependencies declared by a plugin against the currently
    ///   installed and enabled plugins.
    /// </summary>
    /// <param name="plugin">The plugin whose dependencies to resolve.</param>
    /// <returns>A <see cref="DependencyResolutionResult"/>.</returns>
    DependencyResolutionResult ResolveDependencies(LocalPluginInfo plugin);

    /// <summary>
    ///   Resolve a list of dependency declarations against the currently
    ///   installed and enabled plugins.
    /// </summary>
    /// <param name="dependencies">The dependency list to resolve.</param>
    /// <returns>A <see cref="DependencyResolutionResult"/>.</returns>
    DependencyResolutionResult ResolveDependencies(IReadOnlyList<PluginDependency> dependencies);

    /// <summary>
    ///   Find all installed plugins that declare the given plugin as a
    ///   dependency. Only considers the currently active (enabled) version of
    ///   each plugin.
    /// </summary>
    /// <param name="plugin">The plugin to find dependents of.</param>
    /// <returns>Plugins that depend on <paramref name="plugin"/>.</returns>
    IReadOnlyList<LocalPluginInfo> GetDependents(LocalPluginInfo plugin);

    /// <summary>
    ///   Check whether <paramref name="candidate"/> satisfies a given version
    ///   constraint string.
    /// </summary>
    /// <param name="versionRange">
    ///   The version constraint (e.g. <c>"&gt;=1.0.0"</c>, <c>"^2.0.0"</c>,
    ///   <c>"1.5.0"</c>).
    /// </param>
    /// <param name="candidate">The version to test.</param>
    /// <returns><c>true</c> if the candidate satisfies the range.</returns>
    bool IsVersionSatisfied(string versionRange, Version candidate);

    #region Lifecycle Validation

    /// <summary>
    ///   Validate that a plugin can be enabled. All required dependencies must
    ///   be installed and active.
    /// </summary>
    /// <param name="plugin">The plugin to validate.</param>
    /// <returns>
    ///   A <see cref="DependencyResolutionResult"/> with <see cref="DependencyResolutionResult.CanResolve"/>
    ///   set to <c>true</c> if all required dependencies are satisfied.
    /// </returns>
    DependencyResolutionResult ValidateEnable(LocalPluginInfo plugin);

    /// <summary>
    ///   Validate that a plugin can be disabled. Returns the list of enabled
    ///   plugins that depend on <paramref name="plugin"/>.
    /// </summary>
    /// <param name="plugin">The plugin to validate.</param>
    /// <returns>
    ///   Enabled plugins that declare <paramref name="plugin"/> as a
    ///   dependency. Empty if it is safe to disable.
    /// </returns>
    IReadOnlyList<LocalPluginInfo> ValidateDisable(LocalPluginInfo plugin);

    /// <summary>
    ///   Compute the set of plugins that would be affected by uninstalling
    ///   <paramref name="plugin"/>. Includes direct dependents and transitive
    ///   dependents (dependents of dependents).
    /// </summary>
    /// <param name="plugin">The plugin to check.</param>
    /// <returns>
    ///   The full cascade of plugins that depend on <paramref name="plugin"/>,
    ///   directly or transitively, excluding the plugin itself.
    /// </returns>
    IReadOnlyList<LocalPluginInfo> GetUninstallCascade(LocalPluginInfo plugin);

    #endregion
}
