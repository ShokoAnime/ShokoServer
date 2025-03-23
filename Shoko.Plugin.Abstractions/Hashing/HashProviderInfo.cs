
using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Plugin;

namespace Shoko.Plugin.Abstractions.Hashing;

/// <summary>
/// Contains information about a <see cref="IHashProvider"/>.
/// </summary>
public class HashProviderInfo
{
    /// <summary>
    /// The unique ID of the provider.
    /// </summary>
    public required Guid ID { get; init; }

    /// <summary>
    /// The version of the hash provider.
    /// </summary>
    public Version Version => Provider.Version;

    /// <summary>
    /// The display name of the hash provider.
    /// </summary>
    public string Name => Provider.Name;

    /// <summary>
    /// Describes what the hash provider is for.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The <see cref="IHashProvider"/> that this info is for.
    /// </summary>
    public required IHashProvider Provider { get; init; }

    /// <summary>
    /// Information about the configuration that the hash provider uses.
    /// </summary>
    public required ConfigurationInfo? ConfigurationInfo { get; init; }

    /// <summary>
    /// Information about the plugin that the hash provider belongs to.
    /// </summary>
    public required PluginInfo PluginInfo { get; init; }

    /// <summary>
    /// The enabled hash types.
    /// </summary>
    public required HashSet<string> EnabledHashTypes { get; set; }

    /// <summary>
    /// The priority of the hash provider when running in sequential mode.
    /// </summary>
    public required int Priority { get; set; }
}
