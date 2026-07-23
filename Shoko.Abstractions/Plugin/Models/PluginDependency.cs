using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
///   Describes a dependency of a plugin release on another plugin.
/// </summary>
public sealed class PluginDependency
{
    /// <summary>
    ///   The unique identifier of the plugin this release depends on.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public required Guid PluginID { get; init; }

    /// <summary>
    ///   The semantic version constraint for the dependency (e.g.
    ///   <c>"&gt;=1.0.0"</c>, <c>"^2.0.0"</c>).
    /// </summary>
    [JsonPropertyName("version")]
    [JsonProperty("version")]
    public required string VersionRange { get; init; }

    /// <summary>
    ///   If <see langword="true" />, the dependency is optional. A missing
    ///   optional dependency produces a warning but does not block install or
    ///   enable.
    /// </summary>
    [JsonPropertyName("optional")]
    [JsonProperty("optional")]
    public bool IsOptional { get; init; }
}
