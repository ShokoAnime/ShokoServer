using System;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Server.API.v3.Models.Plugin;

#nullable enable
namespace Shoko.Server.API.v3.Models.Release;

public class ReleaseInfoProvider(ReleaseProviderInfo info)
{
    public Guid ID { get; init; } = info.ID;

    public Version Version { get; init; } = info.Version;

    public string Name { get; init; } = info.Name;

    public string Description { get; init; } = string.IsNullOrEmpty(info.Description) ? string.Empty : info.Description;

    public int Priority { get; init; } = info.Priority;

    public bool IsEnabled { get; init; } = info.Enabled;

    /// <summary>
    /// Information about the plugin that the release info provider belongs to.
    /// </summary>
    public PluginInfo Plugin { get; init; } = new(info.PluginInfo);
}
