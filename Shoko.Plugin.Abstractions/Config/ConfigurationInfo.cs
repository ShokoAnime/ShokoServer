using System;
using Namotion.Reflection;
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
    /// The absolute path to where the the configuration is saved on disk.
    /// </summary>
    /// <remarks>
    /// The settings file may not necessarily exist if it has never been saved.
    /// </remarks>
    /// <value>The path.</value>
    public required string Path { get; init; }

    /// <summary>
    /// The display name of the configuration.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Describes what the configuration is for.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Whether the configuration should be hidden from the UI.
    /// </summary>
    public bool IsHidden => Type.IsAssignableTo(typeof(IHiddenConfiguration));

    /// <summary>
    /// Whether or not the configuration has custom new factory.
    /// </summary>
    public bool HasCustomNewFactory => Definition is IConfigurationDefinitionNewFactory;

    /// <summary>
    /// Whether or not the configuration has custom validation.
    /// </summary>
    public bool HasCustomValidation => Definition is IConfigurationDefinitionWithCustomValidation;

    /// <summary>
    /// Whether or not the configuration has custom actions.
    /// </summary>
    public bool HasCustomActions => Definition is IConfigurationDefinitionWithCustomActions;

    /// <summary>
    /// The definition of the configuration.
    /// </summary>
    public required IConfigurationDefinition? Definition { get; init; }

    /// <summary>
    /// The type of the configuration.
    /// </summary>
    public required Type Type { get; init; }

    /// <summary>
    /// The contextual type of the class or sub-class.
    /// </summary>
    public required ContextualType ContextualType { get; init; }

    /// <summary>
    /// The JSON schema for the configuration type.
    /// </summary>
    public required JsonSchema Schema { get; init; }

    /// <summary>
    /// Information about the plugin that the configuration belongs to.
    /// </summary>
    public required PluginInfo PluginInfo { get; init; }
}
