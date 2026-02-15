using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.API.v3.Models.Plugin;
using AbstractConfigurationInfo = Shoko.Abstractions.Config.ConfigurationInfo;

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
    /// A set of paths for properties that need a restart to take effect.
    /// </summary>
    public HashSet<string> RestartPendingFor { get; init; } = info.RestartPendingFor.ToHashSet();

    /// <summary>
    /// A set of environment variables that have been loaded.
    /// </summary>
    public HashSet<string> LoadedEnvironmentVariables { get; init; } = info.LoadedEnvironmentVariables.ToHashSet();

    /// <summary>
    /// Describes what the configuration is for.
    /// </summary>
    public string? Description { get; init; } = string.IsNullOrEmpty(info.Description) ? null : info.Description;

    /// <summary>
    /// Whether or not the configuration is hidden from the client.
    /// </summary>
    public bool IsHidden { get; init; } = info.IsHidden;

    /// <summary>
    /// Wether or not the configuration is a base configuration.
    /// </summary>
    public bool IsBase => info.IsBase;

    /// <summary>
    /// Whether or not the configuration has custom actions.
    /// </summary>
    public bool HasCustomActions { get; init; } = info.HasCustomActions;

    /// <summary>
    /// Whether or not the configuration has a custom new factory.
    /// </summary>
    public bool HasCustomNewFactory { get; init; } = info.HasCustomNewFactory;

    /// <summary>
    /// Whether or not the configuration has custom validation.
    /// </summary>
    public bool HasCustomValidation { get; init; } = info.HasCustomValidation;

    /// <summary>
    /// Whether or not the configuration has a custom save action.
    /// </summary>
    public bool HasCustomSave { get; init; } = info.HasCustomSave;

    /// <summary>
    /// Whether or not the configuration has a custom load action.
    /// </summary>
    public bool HasCustomLoad { get; init; } = info.HasCustomLoad;

    /// <summary>
    /// Whether or not the configuration support live editing the in-memory
    /// configuration.
    /// </summary>
    public bool HasLiveEdit { get; init; } = info.HasLiveEdit;

    /// <summary>
    /// Information about the plugin that the configuration belongs to.
    /// </summary>
    public PluginInfo Plugin { get; init; } = new(info.PluginInfo);
}
