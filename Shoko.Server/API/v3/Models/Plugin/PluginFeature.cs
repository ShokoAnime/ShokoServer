using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Plugin.Enums;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
///   A feature advertised by a plugin, identified by the plugin ID and the
///   feature name.
/// </summary>
public class PluginFeature
{
    /// <summary>
    ///   The ID of the plugin advertising the feature.
    /// </summary>
    public Guid PluginID { get; init; }

    /// <summary>
    ///   The name of the feature, unique within the plugin. Always lowercase
    ///   kebab-case.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    ///   The version of the feature contract, as major, minor, and patch.
    /// </summary>
    public Version Version { get; init; }

    /// <summary>
    ///   Who the feature is advertised to.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public PluginFeatureVisibility Visibility { get; init; }

    /// <summary>
    ///   Optional parameters of the feature. The shape is defined by the plugin
    ///   and versioned by <see cref="Version"/>.
    /// </summary>
    public JObject? Metadata { get; init; }

    /// <summary>
    ///   The plugin advertising the feature. Only included for administrators.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public PluginInfo? PluginInfo { get; init; }

    public PluginFeature(LocalPluginFeature feature, bool includePluginInfo = false)
    {
        PluginID = feature.PluginInfo.ID;
        Name = feature.Name;
        Version = feature.Version;
        Visibility = feature.Visibility;
        Metadata = feature.Metadata;
        if (includePluginInfo)
            PluginInfo = new(feature.PluginInfo);
    }
}
