using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shoko.Abstractions.Core;
using Shoko.Server.Services;

using AbstractPluginInfo = Shoko.Abstractions.Plugin.Models.LocalPluginInfo;

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
    [Required]
    public Guid ID { get; init; } = pluginInfo.ID;

    /// <summary>
    ///   The name of the plugin.
    /// </summary>
    [Required]
    public string Name { get; init; } = pluginInfo.Name;

    /// <summary>
    ///   The description of the plugin.
    /// </summary>
    [Required]
    public string Description { get; init; } = pluginInfo.Description;

    /// <summary>
    ///   The version of the plugin.
    /// </summary>
    [Required]
    public Version Version { get; init; } = pluginInfo.Version.Version;

    /// <summary>
    ///   The .NET runtime identifier (e.g. <c>"win-64"</c>, <c>"linux-x64"</c>, etc.) the component was built for.
    /// </summary>
    [Required]
    public string RuntimeIdentifier { get; init; } = pluginInfo.Version.RuntimeIdentifier;

    /// <summary>
    ///   The version of the plugin abstractions that the plugin was built
    ///   against.
    /// </summary>
    [Required]
    public Version AbstractionVersion { get; init; } = pluginInfo.Version.AbstractionVersion;

    /// <summary>
    ///   The source revision of the component, if available.
    /// </summary>
    public string? SourceRevision { get; init; } = pluginInfo.Version.SourceRevision;

    /// <summary>
    ///   The release tag tied to the source revision, if available.
    /// </summary>
    public string? ReleaseTag { get; init; } = pluginInfo.Version.ReleaseTag;

    /// <summary>
    /// The release channel of the component.
    /// </summary>
    [Required]
    public ReleaseChannel Channel { get; init; } = pluginInfo.Version.Channel;

    /// <summary>
    /// The date and time the component was released.
    /// </summary>
    [Required]
    public DateTime ReleasedAt { get; init; } = pluginInfo.Version.ReleasedAt;

    /// <summary>
    ///   The author(s) of the plugin.
    /// </summary>
    public string? Authors { get; set; } = pluginInfo.Authors;

    /// <summary>
    ///   The order in which the plugin was loaded.
    /// </summary>
    [Required]
    public int LoadOrder { get; init; } = pluginInfo.LoadOrder;

    /// <summary>
    ///   The thumbnail for the plugin, if it is available for the plugin.
    /// </summary>
    public PackageThumbnailInfo? Thumbnail { get; init; } = pluginInfo.Thumbnail is null ? null : new PackageThumbnailInfo(pluginInfo.Thumbnail);

    /// <summary>
    /// When the plugin was installed locally.
    /// </summary>
    [Required]
    public DateTime InstalledAt { get; init; } = pluginInfo.InstalledAt;

    /// <summary>
    ///   Indicates the plugin is currently installed. Will only be <c>false</c>
    ///   if the plugin has be uninstalled in the current session.
    /// </summary>
    [Required]
    public bool IsInstalled { get; init; } = pluginInfo.IsInstalled;

    /// <summary>
    ///   Indicates the plugin is currently enabled for use in the current
    ///   session or for the next session.
    /// </summary>
    [Required]
    public bool IsEnabled { get; init; } = pluginInfo.IsEnabled;

    /// <summary>
    ///   Indicates the plugin is currently loaded in the current session.
    /// </summary>
    [Required]
    public bool IsActive { get; init; } = pluginInfo.IsActive;

    /// <summary>
    ///   Indicates the plugin requires a restart for changes to take effect.
    /// </summary>
    [Required]
    public bool RestartPending { get; init; } = pluginInfo.RestartPending;

    /// <summary>
    ///   Indicates the plugin can be loaded by the current runtime. Missing
    ///   assemblies or incompatible ABI versions will prevent loading.
    /// </summary>
    [Required]
    public bool CanLoad { get; init; } = pluginInfo.CanLoad;

    /// <summary>
    ///   Indicates if the plugin can be uninstalled by the user.
    /// </summary>
    [Required]
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
    [Required]
    public IReadOnlyList<string> DLLs { get; init; } = pluginInfo.DLLs
        .Select(path => path.Replace(ApplicationPaths.Instance.PluginsPath, "%PluginsPath%").Replace(ApplicationPaths.Instance.ApplicationPath, "%ApplicationPath%"))
        .ToArray();
}
