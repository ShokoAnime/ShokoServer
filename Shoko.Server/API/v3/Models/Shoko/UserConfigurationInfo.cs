using System;

namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
/// A user configuration as one user sees it, or as registered.
/// </summary>
public class UserConfigurationInfo
{
    /// <summary>
    /// The configuration's kebab-case ID, as the routes take it.
    /// </summary>
    public string ID { get; init; }

    /// <summary>
    /// The configuration's display name, or its ID when it has none.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// The configuration's description, if it has one.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether a loaded plugin registered the configuration, so it can be saved.
    /// </summary>
    public bool IsRegistered { get; init; }

    /// <summary>
    /// Whether the configuration is checked against a schema.
    /// </summary>
    public bool HasSchema { get; init; }

    /// <summary>
    /// The ID of the configuration type describing it, for its schema and
    /// generated UI under <c>/api/v3/Configuration/{configurationID}</c>, or
    /// <c>null</c> when it has none.
    /// </summary>
    public Guid? ConfigurationTypeID { get; init; }

    /// <summary>
    /// The ID of the plugin the configuration belongs to.
    /// </summary>
    public Guid PluginID { get; init; }

    /// <summary>
    /// The name of the plugin the configuration belongs to, or <c>null</c> when it
    /// is not loaded.
    /// </summary>
    public string? PluginName { get; init; }

    /// <summary>
    /// When the configuration was last saved, or <c>null</c> when none is stored.
    /// </summary>
    public DateTime? UpdatedAt { get; init; }

    /// <summary>
    /// Builds the model from a configuration.
    /// </summary>
    /// <param name="info">The configuration.</param>
    public UserConfigurationInfo(Abstractions.Config.UserConfigurationInfo info)
    {
        ID = info.ID;
        Name = info.Name;
        Description = info.Description;
        IsRegistered = info.IsRegistered;
        HasSchema = info.HasSchema;
        ConfigurationTypeID = info.ConfigurationInfo?.ID;
        PluginID = info.PluginID;
        PluginName = info.PluginInfo?.Name;
        UpdatedAt = info.UpdatedAt;
    }
}
