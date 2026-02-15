using System;
using System.Collections.Generic;
using Namotion.Reflection;
using NJsonSchema;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Services;

namespace Shoko.Abstractions.Config;

/// <summary>
/// Information about a configuration and which plugin it belongs to.
/// </summary>
/// <param name="configurationService">Configuration service.</param>
public class ConfigurationInfo(IConfigurationService configurationService)
{
    /// <summary>
    /// The configuration service.
    /// </summary>
    private readonly IConfigurationService _configurationService = configurationService;

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
    public required string? Path { get; init; }

    /// <summary>
    /// The display name of the configuration.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Describes what the configuration is for.
    /// </summary>
    public required string Description { get; init; }

    private bool? _isHidden = null;

    /// <summary>
    /// Whether the configuration should be hidden from the UI.
    /// </summary>
    public bool IsHidden => _isHidden ??= Type.IsAssignableTo(typeof(IHiddenConfiguration));

    private bool? _isBase = null;

    /// <summary>
    /// Wether or not the configuration is a base configuration.
    /// </summary>
    public bool IsBase => _isBase ??= Type.IsAssignableTo(typeof(IBaseConfiguration));

    /// <summary>
    /// Whether or not the configuration has custom actions.
    /// </summary>
    public required bool HasCustomActions { get; init; }

    /// <summary>
    /// Whether or not the configuration has a custom new factory.
    /// </summary>
    public required bool HasCustomNewFactory { get; init; }

    /// <summary>
    /// Whether or not the configuration has custom validation.
    /// </summary>
    public required bool HasCustomValidation { get; init; }

    /// <summary>
    /// Whether or not the configuration has a custom save action.
    /// </summary>
    public required bool HasCustomSave { get; init; }

    /// <summary>
    /// Whether or not the configuration has a custom load action.
    /// </summary>
    public required bool HasCustomLoad { get; init; }

    /// <summary>
    /// Whether or not the configuration support live editing the in-memory
    /// configuration.
    /// </summary>
    public required bool HasLiveEdit { get; init; }

    /// <summary>
    /// A set of paths for properties that need a restart to take effect.
    /// </summary>
    public IReadOnlySet<string> RestartPendingFor
        => _configurationService.RestartPendingFor.TryGetValue(ID, out var restartPendingFor) ? restartPendingFor : new HashSet<string>();

    /// <summary>
    /// A set of environment variables that have been loaded.
    /// </summary>
    public IReadOnlySet<string> LoadedEnvironmentVariables
        => _configurationService.LoadedEnvironmentVariables.TryGetValue(ID, out var loadedEnvVars) ? loadedEnvVars : new HashSet<string>();

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
