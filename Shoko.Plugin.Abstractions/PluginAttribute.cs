using System;

namespace Shoko.Plugin.Abstraction;

/// <summary>
/// An attribute for defining a plugin.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class PluginAttribute : Attribute
{
    /// <summary>
    /// The ID of the plugin.
    /// </summary>
    public string PluginId { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginAttribute"/> class.
    /// </summary>
    /// <param name="pluginId">The ID of the plugin.</param>
    public PluginAttribute(string pluginId)
    {
        PluginId = pluginId;
    }
}
