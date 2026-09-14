using System;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Plugin.Enums;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
///   A validated <seealso cref="PluginFeature"/> tied to its parent plugin.
///   Represents a feature advertised by a plugin.
/// </summary>
public sealed class LocalPluginFeature
{
    /// <summary>
    ///   The name of the feature, unique within the plugin.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   The version of the feature, with only the major, minor, and build
    ///   (patch) components set.
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    ///   Who the feature is advertised to.
    /// </summary>
    public required PluginFeatureVisibility Visibility { get; init; }

    /// <summary>
    ///   Optional parameters of the feature for clients.
    /// </summary>
    public required JObject? Metadata { get; init; }

    /// <summary>
    ///   Information about the plugin the feature belongs to.
    /// </summary>
    public required LocalPluginInfo PluginInfo { get; init; }
}
