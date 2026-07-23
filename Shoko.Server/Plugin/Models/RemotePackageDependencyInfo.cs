using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Shoko.Server.Plugin.Models;

/// <summary>
///   Describes a dependency of a plugin release on another plugin, as
///   represented in the remote package manifest.
/// </summary>
public sealed class RemotePackageDependencyInfo
{
    /// <summary>
    ///   The unique identifier of the plugin this release depends on.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public Guid PluginID { get; set; }

    /// <summary>
    ///   The semantic version constraint for the dependency (e.g.
    ///   <c>"&gt;=1.0.0"</c>, <c>"^2.0.0"</c>).
    /// </summary>
    [JsonPropertyName("version")]
    [JsonProperty("version")]
    public string? VersionRange { get; set; }

    /// <summary>
    ///   If <see langword="true" />, the dependency is optional. A missing
    ///   optional dependency produces a warning but does not block install or
    ///   enable.
    /// </summary>
    [JsonPropertyName("optional")]
    [JsonProperty("optional")]
    public bool IsOptional { get; set; }
}
