using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Plugin;

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
