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
