using System;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Connectivity;
using Shoko.Abstractions.Video.Release;
using Shoko.Server.API.v3.Models.Configuration;
using Shoko.Server.API.v3.Models.Plugin;
using Shoko.Server.API.v3.Models.Suspension;

namespace Shoko.Server.API.v3.Models.Release;

/// <summary>
/// A release info provider.
/// </summary>
/// <param name="info">Internal release info provider.</param>
/// <param name="availability">Whether the provider can be used now.</param>
public class ReleaseInfoProvider(ReleaseProviderInfo info, ProviderAvailability availability)
{
    /// <summary>
    /// The unique ID of the provider.
    /// </summary>
    [Required]
    public Guid ID { get; init; } = info.ID;

    /// <summary>
    /// The version of the release info provider.
    /// </summary>
    [Required]
    public Version Version { get; init; } = info.Version;

    /// <summary>
    /// The display name of the release info provider.
    /// </summary>
    [Required]
    public string Name { get; init; } = info.Name;

    /// <summary>
    /// Describes what the release info provider is for.
    /// </summary>
    [Required]
    public string Description { get; init; } = string.IsNullOrEmpty(info.Description) ? string.Empty : info.Description;

    /// <summary>
    /// The priority of the provider during automatic usage.
    /// </summary>
    [Required]
    public int Priority { get; init; } = info.Priority;

    /// <summary>
    /// Whether or not the provider is enabled for automatic usage.
    /// </summary>
    [Required]
    public bool IsEnabled { get; init; } = info.Enabled;

    /// <summary>
    /// Whether the provider can be used now, and if not, what stops it.
    /// </summary>
    [Required]
    public ProviderAvailabilityStatus Availability { get; init; } = new(availability);

    /// <summary>
    /// How many of the provider's jobs may run at once, if it sets a limit.
    /// </summary>
    public int? MaxConcurrentJobs { get; init; } = info.MaxConcurrentJobs;

    /// <summary>
    /// Information about the configuration the release info provider uses.
    /// </summary>
    public ConfigurationInfo? Configuration { get; init; } = info.ConfigurationInfo is null ? null : new(info.ConfigurationInfo);

    /// <summary>
    /// Information about the plugin that the release info provider belongs to.
    /// </summary>
    [Required]
    public PluginInfo Plugin { get; init; } = new(info.PluginInfo);
}
