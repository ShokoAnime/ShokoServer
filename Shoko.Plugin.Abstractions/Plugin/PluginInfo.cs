using System;

namespace Shoko.Plugin.Abstractions.Plugin;

/// <summary>
/// Information about a plugin.
/// </summary>
public class PluginInfo
{
    /// <summary>
    /// The unique identifier for the plugin.
    /// </summary>
    public required Guid ID { get; init; }

    /// <summary>
    /// The version of the plugin.
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    /// The name of the plugin.
    /// </summary>
    public string Name => Plugin.Name;

    /// <summary>
    /// The description of the plugin.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Whether the plugin can be uninstalled.
    /// </summary>
    public bool CanUninstall { get; init; }

    /// <summary>
    /// The instance of the plugin.
    /// </summary>
    public required IPlugin Plugin { get; init; }

    /// <summary>
    /// The type of the plugin.
    /// </summary>
    public required Type PluginType { get; init; }
}
