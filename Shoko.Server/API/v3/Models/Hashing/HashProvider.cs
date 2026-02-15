using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Hashing;
using Shoko.Server.API.v3.Models.Configuration;
using Shoko.Server.API.v3.Models.Plugin;

#nullable enable
namespace Shoko.Server.API.v3.Models.Hashing;

/// <summary>
/// A hash provider.
/// </summary>
/// <param name="info">Internal hash provider info.</param>
public class HashProvider(HashProviderInfo info)
{
    /// <summary>
    /// The unique ID of the provider.
    /// </summary>
    public Guid ID { get; init; } = info.ID;

    /// <summary>
    /// The version of the hash provider.
    /// </summary>
    public Version Version { get; init; } = info.Version;

    /// <summary>
    /// The display name of the hash provider.
    /// </summary>
    public string Name { get; init; } = info.Name;

    /// <summary>
    /// Describes what the hash provider is for.
    /// </summary>
    public string? Description { get; init; } = string.IsNullOrEmpty(info.Description) ? null : info.Description;

    /// <summary>
    ///   Gets all available hash types for the provider.
    /// </summary>
    public HashSet<string> AvailableHashTypes { get; init; } = info.Provider.AvailableHashTypes.ToHashSet();

    /// <summary>
    /// The enabled hash types.
    /// </summary>
    public HashSet<string> EnabledHashTypes { get; init; } = info.EnabledHashTypes.ToHashSet();

    /// <summary>
    /// Information about the configuration the hash provider uses.
    /// </summary>
    public ConfigurationInfo? Configuration { get; init; } = info.ConfigurationInfo is null ? null : new(info.ConfigurationInfo);

    /// <summary>
    /// Information about the plugin that the hash provider belongs to.
    /// </summary>
    public PluginInfo Plugin { get; init; } = new(info.PluginInfo);
}
