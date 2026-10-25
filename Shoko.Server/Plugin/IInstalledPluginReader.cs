using System;
using System.Collections.Generic;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Server.Plugin;

/// <summary>
///   Reads the installed plugins the way the plugin scan does, without
///   registering any of them, so the package manager can look at them before
///   the scan runs.
/// </summary>
public interface IInstalledPluginReader
{
    /// <summary>
    ///   Reads every installed plugin, system and user ones alike.
    /// </summary>
    /// <returns>The installed plugins, one entry per installed version.</returns>
    IReadOnlyList<InstalledPlugin> ReadInstalledPlugins();

    /// <summary>
    ///   Reads the plugin in one directory.
    /// </summary>
    /// <param name="directoryPath">The directory holding the plugin.</param>
    /// <returns>The plugin, or <c>null</c> when the directory holds none.</returns>
    InstalledPlugin? ReadPlugin(string directoryPath);
}

/// <summary>
///   An installed version of a plugin, as the plugin scan reads it.
/// </summary>
/// <param name="ID">The plugin's ID.</param>
/// <param name="Name">The plugin's name.</param>
/// <param name="Version">The installed version.</param>
/// <param name="IsEnabled">Whether the plugin is enabled, on demand included.</param>
/// <param name="IsPinned">Whether this version is pinned.</param>
/// <param name="IsBundled">Whether this version ships with the server, in its own plugins folder.</param>
/// <param name="IsDependency">Whether this version is marked as installed as a dependency.</param>
/// <param name="Dependencies">The dependencies of this version.</param>
/// <param name="IsEnabledOnDemand">Whether the plugin has no enabled state of its own and follows its dependents.</param>
public sealed record InstalledPlugin(
    Guid ID,
    string Name,
    Version Version,
    bool IsEnabled,
    bool IsPinned,
    bool IsBundled = false,
    bool IsDependency = false,
    IReadOnlyList<PluginDependency>? Dependencies = null,
    bool IsEnabledOnDemand = false
);
