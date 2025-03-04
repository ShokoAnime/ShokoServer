using System;
using NJsonSchema;
using Shoko.Plugin.Abstractions.Plugin;

namespace Shoko.Plugin.Abstractions.Config;

/// <summary>
/// Information about a configuration and which plugin it belongs to.
/// </summary>
public class ConfigurationInfo
{
    /// <summary>
    /// The ID of the configuration.
    /// </summary>
    public required Guid ID { get; init; }

    /// <summary>
    /// The display name of the configuration.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Describes what the configuration is for.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Information about the plugin that the configuration belongs to.
    /// </summary>
    public required PluginInfo PluginInfo { get; init; }

    /// <summary>
    /// The type of the configuration.
    /// </summary>
    public required Type Type { get; init; }

    /// <summary>
    /// The absolute path to where the the configuration is saved on disk.
    /// </summary>
    /// <remarks>
    /// The settings file may not necessarily exist if it has never been saved.
    /// </remarks>
    /// <value>The path.</value>
    public required string Path { get; init; }

    /// <summary>
    /// The JSON schema for the configuration type.
    /// </summary>
    public required JsonSchema Schema { get; init; }
}
