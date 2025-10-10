using System;
using Namotion.Reflection;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

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
    /// The path to the configuration.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The action to perform.
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// The contextual type of the class or sub-class.
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
