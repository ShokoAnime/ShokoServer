using System;
using System.Collections.Generic;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Plugin;

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
    ///   Validate that a plugin can be enabled. Every required dependency must
    ///   have an installed version in range that loads on the next start: one
    ///   enabled, or enabled on demand, which enabling the plugin provides.
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
