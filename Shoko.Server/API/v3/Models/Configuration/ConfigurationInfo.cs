using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
    [Required]
    public Guid ID { get; init; } = info.ID;

    /// <summary>
    /// The display name of the configuration.
    /// </summary>
    [Required]
    public string Name { get; init; } = info.Name;

    /// <summary>
    /// A set of paths for properties that need a restart to take effect.
    /// </summary>
    [Required]
    public HashSet<string> RestartPendingFor { get; init; } = info.RestartPendingFor.ToHashSet();

    /// <summary>
    /// A set of environment variables that have been loaded.
    /// </summary>
    [Required]
    public HashSet<string> LoadedEnvironmentVariables { get; init; } = info.LoadedEnvironmentVariables.ToHashSet();

    /// <summary>
    /// Describes what the configuration is for.
    /// </summary>
    public string? Description { get; init; } = string.IsNullOrEmpty(info.Description) ? null : info.Description;

    /// <summary>
    /// Whether or not the configuration is hidden from the client.
    /// </summary>
    [Required]
    public bool IsHidden { get; init; } = info.IsHidden;

    /// <summary>
    /// Wether or not the configuration is a base configuration.
    /// </summary>
    [Required]
    public bool IsBase => info.IsBase;

    /// <summary>
    /// Whether or not the configuration has custom actions.
    /// </summary>
    [Required]
    public bool HasCustomActions { get; init; } = info.HasCustomActions;

    /// <summary>
    /// Whether or not the configuration has custom new factory.
    /// </summary>
    [Required]
    public bool HasCustomNewFactory { get; init; } = info.HasCustomNewFactory;

    /// <summary>
    /// Whether or not the configuration has custom validation.
    /// </summary>
    [Required]
    public bool HasCustomValidation { get; init; } = info.HasCustomValidation;

    /// <summary>
    /// Whether or not the configuration has custom save action.
    /// </summary>
    [Required]
    public bool HasCustomSave { get; init; } = info.HasCustomSave;

    /// <summary>
    /// Whether or not the configuration has custom load action.
    /// </summary>
    [Required]
    public bool HasCustomLoad { get; init; } = info.HasCustomLoad;

    /// <summary>
    /// Whether or not the configuration support live editing the in-memory
    /// configuration.
    /// </summary>
    [Required]
    public bool HasLiveEdit { get; init; } = info.HasLiveEdit;

    /// <summary>
    /// Information about the plugin that the configuration belongs to.
    /// </summary>
    [Required]
    public PluginInfo Plugin { get; init; } = new(info.PluginInfo);
}
