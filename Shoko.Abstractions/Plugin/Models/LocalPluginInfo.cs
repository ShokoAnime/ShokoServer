using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Core;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
///   Information about a plugin.
/// </summary>
public sealed class LocalPluginInfo
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
    public required VersionInformation Version { get; init; }

    /// <summary>
    ///   The author(s) of the plugin.
    /// </summary>
    public required string? Authors { get; init; }

    /// <summary>
    ///   The repository URL for the plugin, if provided.
    /// </summary>
    public required string? RepositoryUrl { get; init; }

    /// <summary>
    ///   The home-page URL for the plugin, if provided.
    /// </summary>
    public required string? HomepageUrl { get; init; }

    /// <summary>
    ///   The search tags for the plugin. A maximum of 10 tags will be loaded if
    ///   provided.
    /// </summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>
    ///   The order in which the plugin was loaded.
    /// </summary>
    public required int LoadOrder { get; init; }

    /// <summary>
    ///   The thumbnail for the plugin, if it is available for the plugin.
    /// </summary>
    public required PackageThumbnailInfo? Thumbnail { get; init; }

    /// <summary>
    /// When the plugin was installed to the local system.
    /// </summary>
    public required DateTime InstalledAt { get; init; }

    private DateTime? _uninstalledAt;

    /// <summary>
    /// When the plugin was uninstalled from the local system.
    /// </summary>
    public DateTime? UninstalledAt
    {
        get => _uninstalledAt;
        set
        {
            if (value is null || _uninstalledAt is not null)
                return;
            _uninstalledAt = value;
        }
    }

    /// <summary>
    ///   Indicates the plugin is currently installed. Will be <c>false</c>
    ///   if the plugin has been uninstalled in the current session, or if it's
    ///   a remote plugin that has not yet been installed.
    /// </summary>
    public bool IsInstalled => !UninstalledAt.HasValue;

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
    ///   Indicates the plugin can be loaded by the current runtime. Missing
    ///   assemblies or incompatible ABI versions will prevent loading.
    /// </summary>
    public required bool CanLoad { get; init; }

    /// <summary>
    ///   Indicates if the plugin can be uninstalled by the user.
    /// </summary>
    public required bool CanUninstall { get; init; }

    /// <summary>
    ///   The instance of the plugin, if it has been loaded.
    /// </summary>
    public required IPlugin? Plugin { get; init; }

    /// <summary>
    ///   The type of the plugin, if it has been loaded.
    /// </summary>
    public required Type? PluginType { get; init; }

    /// <summary>
    ///   The type used for the plugin to register its services with the core.
    /// </summary>
    public required Type? ServiceRegistrationType { get; init; }

    /// <summary>
    ///   The type used for the plugin to register it's application options with 
    ///   the core.
    /// </summary>
    public required Type? ApplicationRegistrationType { get; init; }

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
