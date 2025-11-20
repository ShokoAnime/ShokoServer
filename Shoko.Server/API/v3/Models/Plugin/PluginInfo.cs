using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Extensions;
using Shoko.Server.Services;

using AbstractPluginInfo = Shoko.Plugin.Abstractions.Plugin.PluginInfo;

#nullable enable
namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
/// A plugin definition.
/// </summary>
public class PluginInfo(AbstractPluginInfo pluginInfo)
{
    /// <summary>
    ///   The unique identifier for the plugin.
    /// </summary>
    public Guid ID { get; init; } = pluginInfo.ID;

    /// <summary>
    ///   The name of the plugin.
    /// </summary>
    public string Name { get; init; } = pluginInfo.Name;

    /// <summary>
    ///   The description of the plugin.
    /// </summary>
    public string Description { get; init; } = pluginInfo.Description;

    /// <summary>
    ///   The version of the plugin.
    /// </summary>
    public Version Version { get; init; } = pluginInfo.Version;

    /// <summary>
    ///   The order in which the plugin was loaded.
    /// </summary>
    public int LoadOrder { get; init; } = pluginInfo.LoadOrder;

    /// <summary>
    ///   Indicates the plugin is currently installed. Will only be <c>false</c>
    ///   if the plugin has be uninstalled in the current session.
    /// </summary>
    public bool IsInstalled { get; init; } = pluginInfo.IsInstalled;

    /// <summary>
    ///   Indicates the plugin is currently enabled for use in the current
    ///   session or for the next session.
    /// </summary>
    public bool IsEnabled { get; init; } = pluginInfo.IsEnabled;

    /// <summary>
    ///   Indicates the plugin is currently loaded in the current session.
    /// </summary>
    public bool IsActive { get; init; } = pluginInfo.IsActive;

    /// <summary>
    ///   Indicates the plugin requires a restart for changes to take effect.
    /// </summary>
    public bool RestartPending { get; init; } = pluginInfo.RestartPending;

    /// <summary>
    ///   Indicates if the plugin can be uninstalled by the user.
    /// </summary>
    public bool CanUninstall { get; init; } = pluginInfo.CanUninstall;

    /// <summary>
    ///   The directory containing the plugin DLLs, if the plugin is not placed
    ///   in the root of the plugins directory.
    /// </summary>
    public string? ContainingDirectory { get; init; } = !string.IsNullOrEmpty(pluginInfo.ContainingDirectory)
        ? pluginInfo.ContainingDirectory.Replace(ApplicationPaths.Instance.PluginsPath, "%PluginsPath%").Replace(ApplicationPaths.Instance.ApplicationPath, "%ApplicationPath%")
        : null;

    /// <summary>
    ///   All DLLs for the plugin. The first path will always be the main DLL
    ///   which contains the plugin implementation.
    /// </summary>
    public IReadOnlyList<string> DLLs { get; init; } = pluginInfo.DLLs
        .Select(path => path.Replace(ApplicationPaths.Instance.PluginsPath, "%PluginsPath%").Replace(ApplicationPaths.Instance.ApplicationPath, "%ApplicationPath%"))
        .ToArray();
}
