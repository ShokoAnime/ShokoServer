using System;
using Namotion.Reflection;
using NJsonSchema;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Services;

namespace Shoko.Plugin.Abstractions.Config;

/// <summary>
/// The context for a configuration action.
/// </summary>
/// <typeparam name="TConfig"></typeparam>
public class ConfigurationActionContext<TConfig> where TConfig : class, IConfiguration, new()
{
    /// <summary>
    /// The configuration instance.
    /// </summary>
    public required TConfig Configuration { get; init; }

    /// <summary>
    /// The configuration info for the configuration.
    /// </summary>
    public required ConfigurationInfo Info { get; init; }

    /// <summary>
    /// The configuration service.
    /// </summary>
    public required IConfigurationService Service { get; init; }

    /// <summary>
    /// The path leading to the value related to the action being performed to
    /// the configuration.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The name of the custom action to perform on the configuration.
    /// </summary>
    /// <remarks>
    /// Will be set to an empty string if this is not a custom action.
    /// </remarks>
    public string ActionName { get; init; } = string.Empty;

    /// <summary>
    /// The type of action to perform on the configuration.
    /// </summary>
    public ConfigurationActionType ActionType { get; init; } = ConfigurationActionType.Custom;

    /// <summary>
    /// The JSON schema for the value at the <see cref="Path"/>.
    /// </summary>
    public required JsonSchema Schema { get; init; }

    /// <summary>
    /// The contextual type for the value at the <see cref="Path"/>.
    /// </summary>
    public required ContextualType Type { get; init; }

    /// <summary>
    /// The user performing the action, if applicable.
    /// </summary>
    public IShokoUser? User { get; init; }

    /// <summary>
    /// The base URI used to access the server by the user, if applicable.
    /// </summary>
    public Uri? Uri { get; init; }
}
