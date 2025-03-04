using System;

using AbstractPluginInfo = Shoko.Plugin.Abstractions.Plugin.PluginInfo;

#nullable enable
namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
/// A plugin definition.
/// </summary>
public class PluginInfo(AbstractPluginInfo pluginInfo)
{
    /// <summary>
    /// Gets the unique ID of the plugin.
    /// </summary>
    public Guid ID { get; init; } = pluginInfo.ID;

    /// <summary>
    /// Gets the version of the plugin.
    /// </summary>
    public Version Version { get; init; } = pluginInfo.Version;

    /// <summary>
    /// Gets the name of the plugin.
    /// </summary>
    public string Name { get; init; } = pluginInfo.Name;

    /// <summary>
    /// Gets the description of the plugin.
    /// </summary>
    public string? Description { get; init; } = string.IsNullOrEmpty(pluginInfo.Description) ? null : pluginInfo.Description;

    /// <summary>
    /// Gets a value indicating whether the plugin can be uninstalled.
    /// </summary>
    public bool CanUninstall { get; init; } = pluginInfo.CanUninstall;
}
