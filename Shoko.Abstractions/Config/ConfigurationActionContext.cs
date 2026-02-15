using System;
using Microsoft.Extensions.Logging;
using Namotion.Reflection;
using NJsonSchema;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Config;

/// <summary>
/// The context for a configuration action.
/// </summary>
public class ConfigurationActionContext
{
    /// <summary>
    /// The logger for the given context.
    /// </summary>
    public required ILogger Logger { get; init; }

    /// <summary>
    /// The configuration instance.
    /// </summary>
    public IConfiguration Configuration { get; init; } = null!;

    /// <summary>
    /// The configuration info for the configuration.
    /// </summary>
    public required ConfigurationInfo Info { get; init; }

    /// <summary>
    /// The configuration service.
    /// </summary>
    public required IConfigurationService ConfigurationService { get; init; }

    /// <summary>
    /// The plugin manager.
    /// </summary>
    public required IPluginManager PluginManager { get; init; }

    /// <summary>
    /// The path leading to the value related to the action being performed to
    /// the configuration.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The reactive event type to perform for live-editing.
    /// </summary>
    public required ReactiveEventType ReactiveEventType { get; init; }

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
    public IUser? User { get; init; }

    /// <summary>
    /// The base URI used to access the server by the user, if applicable.
    /// </summary>
    public Uri? Uri { get; init; }
}

/// <summary>
/// The context for a configuration action.
/// </summary>
/// <typeparam name="TConfig"></typeparam>
public class ConfigurationActionContext<TConfig> : ConfigurationActionContext where TConfig : class, IConfiguration, new()
{
    /// <summary>
    /// The configuration instance.
    /// </summary>
    public new required TConfig Configuration { get => (TConfig)base.Configuration; init => base.Configuration = value; }
}
