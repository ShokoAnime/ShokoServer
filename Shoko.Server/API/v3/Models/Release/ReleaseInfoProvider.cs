using System;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Server.API.v3.Models.Configuration;
using Shoko.Server.API.v3.Models.Plugin;

#nullable enable
namespace Shoko.Server.API.v3.Models.Release;

/// <summary>
/// A release info provider.
/// </summary>
/// <param name="info">Internal release info provider.</param>
public class ReleaseInfoProvider(ReleaseProviderInfo info)
{
    /// <summary>
    /// The unique ID of the provider.
    /// </summary>
    public Guid ID { get; init; } = info.ID;

    /// <summary>
    /// The version of the release info provider.
    /// </summary>
    public Version Version { get; init; } = info.Version;

    /// <summary>
    /// The display name of the release info provider.
    /// </summary>
    public string Name { get; init; } = info.Name;

    /// <summary>
    /// Describes what the release info provider is for.
    /// </summary>
    public string Description { get; init; } = string.IsNullOrEmpty(info.Description) ? string.Empty : info.Description;

    /// <summary>
    /// The priority of the provider during automatic usage.
    /// </summary>
    public int Priority { get; init; } = info.Priority;

    /// <summary>
    /// Whether or not the provider is enabled for automatic usage.
    /// </summary>
    public bool IsEnabled { get; init; } = info.Enabled;

    /// <summary>
    /// Information about the configuration the release info provider uses.
    /// </summary>
    public ConfigurationInfo? Configuration { get; init; } = info.ConfigurationInfo is null ? null : new(info.ConfigurationInfo);

    /// <summary>
    /// Information about the plugin that the release info provider belongs to.
    /// </summary>
    public PluginInfo Plugin { get; init; } = new(info.PluginInfo);
}
