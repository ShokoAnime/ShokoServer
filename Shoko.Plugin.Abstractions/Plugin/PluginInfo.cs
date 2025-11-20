using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Shoko.Plugin.Abstractions.Plugin;

/// <summary>
///   Information about a plugin.
/// </summary>
public sealed class PluginInfo
{
    /// <summary>
    ///   The unique identifier for the plugin.
    /// </summary>
    public required Guid ID { get; init; }

    /// <summary>
    ///   The name of the plugin.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   The description of the plugin.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    ///   The version of the plugin.
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    ///   The order in which the plugin was loaded.
    /// </summary>
    public required int LoadOrder { get; init; }

    /// <summary>
    ///   Indicates the plugin is currently installed. Will be <c>false</c>
    ///   if the plugin has be uninstalled in the current session, or if it's
    ///   a remote plugin that has not yet been installed.
    /// </summary>
    public required bool IsInstalled { get; set; }

    /// <summary>
    ///   Indicates the plugin is currently enabled for use in the current
    ///   session or for the next session.
    /// </summary>
    public required bool IsEnabled { get; set; }

    /// <summary>
    ///   Indicates the plugin is currently loaded in the current session.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Plugin), nameof(PluginType))]
    public required bool IsActive { get; init; }

    /// <summary>
    ///   Indicates the plugin requires a restart for changes to take effect.
    /// </summary>
    public bool RestartPending => IsEnabled != IsActive;

    /// <summary>
    ///   Indicates if the plugin can be uninstalled by the user.
    /// </summary>
    public required bool CanUninstall { get; init; }

    /// <summary>
    ///   The instance of the plugin, if it has been loaded.
    /// </summary>
    public required IPlugin? Plugin { get; init; }

    /// <summary>
    /// The type of the plugin, if it has been loaded.
    /// </summary>
    public required Type? PluginType { get; init; }

    /// <summary>
    ///   The directory containing the plugin DLLs, if the plugin is not placed
    ///   in the root of the plugins directory.
    /// </summary>
    public required string? ContainingDirectory { get; init; }

    /// <summary>
    ///   All DLLs for the plugin. The first path will always be the main DLL
    ///   which contains the plugin implementation.
    /// </summary>
    public required IReadOnlyList<string> DLLs { get; init; }

    /// <summary>
    ///   All loaded types for the plugin.
    /// </summary>
    public required IReadOnlyList<Type> Types { get; init; }
}
