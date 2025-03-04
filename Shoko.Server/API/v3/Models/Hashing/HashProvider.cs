using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.Hashing;
using Shoko.Server.API.v3.Models.Plugin;

#nullable enable
namespace Shoko.Server.API.v3.Models.Hashing;

public class HashProvider(HashProviderInfo info)
{
    public Guid ID { get; init; } = info.ID;

    public Version Version { get; init; } = info.Version;

    public string Name { get; init; } = info.Name;

    public string? Description { get; init; } = string.IsNullOrEmpty(info.Description) ? null : info.Description;

    public int Priority { get; init; } = info.Priority;

    public HashSet<string> AvailableHashTypes { get; init; } = info.Provider.AvailableHashTypes.ToHashSet();

    public HashSet<string> DefaultEnabledHashTypes { get; init; } = info.Provider.DefaultEnabledHashTypes.ToHashSet();

    public HashSet<string> EnabledHashTypes { get; init; } = info.EnabledHashTypes.ToHashSet();

    /// <summary>
    /// Information about the plugin that the configuration belongs to.
    /// </summary>
    public PluginInfo Plugin { get; init; } = new(info.PluginInfo);
}
