using System;
using Shoko.Server.API.v3.Models.Plugin;
using AbstractConfigurationInfo = Shoko.Plugin.Abstractions.Config.ConfigurationInfo;

#nullable enable
namespace Shoko.Server.API.v3.Models.Configuration;

public class ConfigurationInfo(AbstractConfigurationInfo info)
{
    /// <summary>
    /// The ID of the configuration.
    /// </summary>
    public Guid ID { get; init; } = info.ID;

    /// <summary>
    /// The display name of the configuration.
    /// </summary>
    public string Name { get; init; } = info.Name;

    /// <summary>
    /// Describes what the configuration is for.
    /// </summary>
    public string? Description { get; init; } = string.IsNullOrEmpty(info.Description) ? null : info.Description;

    /// <summary>
    /// Whether or not the configuration is hidden from the client.
    /// </summary>
    public bool IsHidden { get; init; } = info.IsHidden;

    /// <summary>
    /// Whether or not the configuration has custom new factory.
    /// </summary>
    public bool HasCustomNewFactory { get; init; } = info.HasCustomNewFactory;

    /// <summary>
    /// Whether or not the configuration has custom validation.
    /// </summary>
    public bool HasCustomValidation { get; init; } = info.HasCustomValidation;

    /// <summary>
    /// Whether or not the configuration has custom actions.
    /// </summary>
    public bool HasCustomActions { get; init; } = info.HasCustomActions;

    /// <summary>
    /// Information about the plugin that the configuration belongs to.
    /// </summary>
    public PluginInfo Plugin { get; init; } = new(info.PluginInfo);
}
