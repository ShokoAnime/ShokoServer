using System;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;

using AbstractPluginDependency = Shoko.Abstractions.Plugin.Models.PluginDependency;

namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
///   A plugin that this plugin depends on.
/// </summary>
public class PluginDependency
{
    /// <summary>
    ///   Create from an abstract dependency declaration (metadata only, no resolution).
    /// </summary>
    public PluginDependency(AbstractPluginDependency dependency)
    {
        PluginID = dependency.PluginID;
        VersionRange = dependency.VersionRange;
        IsOptional = dependency.IsOptional;
    }

    /// <summary>
    ///   Create from a resolved dependency, carrying resolution state.
    /// </summary>
    public PluginDependency(ResolvedDependency resolved)
    {
        var dep = resolved.Dependency;
        PluginID = dep.PluginID;
        VersionRange = dep.VersionRange;
        IsOptional = dep.IsOptional;
        IsResolved = resolved.IsResolved;
        Plugin = resolved.Plugin is not null ? new PluginInfo(resolved.Plugin) : null;
        Message = resolved.Message;
    }

    /// <summary>
    ///   The unique identifier of the plugin this release depends on.
    /// </summary>
    [Required]
    public Guid PluginID { get; init; }

    /// <summary>
    ///   The semantic version constraint for the dependency (e.g.
    ///   <c>"&gt;=1.0.0"</c>, <c>"^2.0.0"</c>).
    /// </summary>
    [Required]
    public string VersionRange { get; init; }

    /// <summary>
    ///   If <see langword="true" />, the dependency is optional. A missing
    ///   optional dependency produces a warning but does not block install or
    ///   enable.
    /// </summary>
    [Required]
    public bool IsOptional { get; init; }

    /// <summary>
    ///   Whether the dependency is satisfied by a currently installed and
    ///   enabled plugin. Only populated by resolution endpoints.
    /// </summary>
    public bool IsResolved { get; init; }

    /// <summary>
    ///   The installed plugin that satisfies this dependency, if resolved.
    ///   Only populated by resolution endpoints.
    /// </summary>
    public PluginInfo? Plugin { get; init; }

    /// <summary>
    ///   A human-readable description of the resolution state. Only populated
    ///   by resolution endpoints.
    /// </summary>
    public string? Message { get; init; }
}
