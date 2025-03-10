using System;
using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.Plugin;

/// <summary>
/// Responsible for managing plugin registration and management.
/// </summary>
public interface IPluginManager
{
    /// <summary>
    ///   Adds the needed parts for the service to function.
    /// </summary>
    /// <remarks>
    ///   This should be called once per instance of the service, and will be
    ///   called during start-up. Calling it multiple times will have no effect.
    /// </remarks>
    /// <param name="plugins">
    ///   The plugins.
    /// </param>
    void AddParts(IEnumerable<IPlugin> plugins);

    /// <summary>
    /// Gets information about all registered plugins.
    /// </summary>
    /// <returns>A list of <see cref="PluginInfo"/>s.</returns>
    IEnumerable<PluginInfo> GetPluginInfos();

    /// <summary>
    /// Gets information about a registered plugin by its ID if available.
    /// </summary>
    /// <param name="pluginId">The ID of the plugin.</param>
    /// <returns>The <see cref="PluginInfo"/> for the plugin, or <see langword="null"/> if not found.</returns>
    PluginInfo? GetPluginInfo(Guid pluginId);

    /// <summary>
    /// Gets information about a registered plugin by its instance.
    /// </summary>
    /// <param name="plugin">The instance of the plugin.</param>
    /// <returns>The <see cref="PluginInfo"/> for the plugin, or <see langword="null"/> if not found.</returns>
    PluginInfo? GetPluginInfo(IPlugin plugin);

    /// <summary>
    /// Gets information about a registered plugin by its type as a type parameter.
    /// </summary>
    /// <typeparam name="TPlugin">The type of the plugin.</typeparam>
    /// <returns>The <see cref="PluginInfo"/> for the plugin, or <see langword="null"/> if not found.</returns>
    PluginInfo? GetPluginInfo<TPlugin>() where TPlugin : IPlugin;

    /// <summary>
    /// Gets information about a registered plugin by its type.
    /// </summary>
    /// <param name="type">The type of the plugin.</param>
    /// <returns>The <see cref="PluginInfo"/> for the plugin, or <see langword="null"/> if not found.</returns>
    PluginInfo? GetPluginInfo(Type type);
}
