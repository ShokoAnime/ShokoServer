using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Server.API.v3.Models.Configuration;
using Shoko.Server.API.v3.Models.Plugin;

#nullable enable
namespace Shoko.Server.API.v3.Models.Airing;

/// <summary>
/// An airing schedule provider.
/// </summary>
/// <param name="info">Internal airing schedule provider info.</param>
/// <exception cref="ArgumentNullException"><paramref name="info"/> is <c>null</c>.</exception>
public class AiringScheduleProvider(AiringScheduleProviderInfo info)
{
    /// <summary>
    /// The unique ID of the provider.
    /// </summary>
    [Required]
    public Guid ID { get; init; } = info.ID;

    /// <summary>
    /// The version of the airing schedule provider.
    /// </summary>
    [Required]
    public Version Version { get; init; } = info.Version;

    /// <summary>
    /// The display name of the airing schedule provider.
    /// </summary>
    [Required]
    public string Name { get; init; } = info.Name;

    /// <summary>
    /// Describes what the airing schedule provider is for.
    /// </summary>
    [Required]
    public string Description { get; init; } = string.IsNullOrEmpty(info.Description) ? string.Empty : info.Description;

    /// <summary>
    /// The source order of the provider. It decides whose airing wins when two
    /// providers report the same channel for an episode, and breaks ties during
    /// selection. It is not a ranking of results.
    /// </summary>
    [Required]
    public int Priority { get; init; } = info.Priority;

    /// <summary>
    /// Whether or not the provider is enabled for automatic usage. A provider
    /// with no enabled kinds is disabled.
    /// </summary>
    [Required]
    public bool IsEnabled { get; init; } = info.Enabled;

    /// <summary>
    /// The kinds the provider can supply.
    /// </summary>
    [Required]
    public IReadOnlyList<AiringKind> AvailableKinds { get; init; } = [.. info.Provider.AvailableKinds.Order()];

    /// <summary>
    /// The kinds the provider is allowed to supply. Tracks of a disabled kind
    /// are still stored, but hidden from reads.
    /// </summary>
    [Required]
    public IReadOnlyList<AiringKind> EnabledKinds { get; init; } = [.. info.EnabledKinds.Order()];

    /// <summary>
    /// Information about the configuration the airing schedule provider uses.
    /// </summary>
    public ConfigurationInfo? Configuration { get; init; } = info.ConfigurationInfo is null ? null : new(info.ConfigurationInfo);

    /// <summary>
    /// Information about the plugin that the airing schedule provider belongs
    /// to.
    /// </summary>
    [Required]
    public PluginInfo Plugin { get; init; } = new(info.PluginInfo);
}
