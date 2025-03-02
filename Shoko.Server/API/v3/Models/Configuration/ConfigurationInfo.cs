
using System;
using AbstractConfigurationInfo = Shoko.Plugin.Abstractions.Config.ConfigurationInfo;

namespace Shoko.Server.API.v3.Models.Configuration;

public class ConfigurationInfo(AbstractConfigurationInfo info)
{
    /// <summary>
    /// The ID of the configuration.
    /// </summary>
    public Guid ID { get; init; } = info.ID;

    /// <summary>
    /// The ID of the plugin that the configuration belongs to.
    /// </summary>
    public Guid PluginID { get; init; } = info.Plugin.ID;

    /// <summary>
    /// The display name of the configuration.
    /// </summary>
    public string Name { get; init; } = info.Name;

    /// <summary>
    /// The name of the plugin that the configuration belongs to.
    /// </summary>
    public string PluginName { get; init; } = info.Plugin.Name;

    /// <summary>
    /// The version of the plugin that the configuration belongs to.
    /// </summary>
    public Version PluginVersion { get; init; } = info.Plugin.GetType().Assembly.GetName().Version ?? new("0.0.0.0");
}
