using System;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Video.Streaming;
using Shoko.Server.API.v3.Models.Configuration;
using Shoko.Server.API.v3.Models.Plugin;

namespace Shoko.Server.API.v3.Models.Streaming;

/// <summary>
/// A playback observer.
/// </summary>
/// <param name="info">Internal playback observer info.</param>
public class PlaybackObserver(PlaybackObserverInfo info)
{
    /// <summary>
    /// The unique ID of the observer.
    /// </summary>
    [Required]
    public string ID { get; init; } = info.ID;

    /// <summary>
    /// The version of the playback observer.
    /// </summary>
    [Required]
    public Version Version { get; init; } = info.Version;

    /// <summary>
    /// The display name of the playback observer.
    /// </summary>
    [Required]
    public string Name { get; init; } = info.Name;

    /// <summary>
    /// Describes what the playback observer is for.
    /// </summary>
    [Required]
    public string Description { get; init; } = string.IsNullOrEmpty(info.Description) ? string.Empty : info.Description;

    /// <summary>
    /// Whether or not the observer is enabled. All enabled observers run on
    /// every stream request — there is no priority ordering.
    /// </summary>
    [Required]
    public bool IsEnabled { get; init; } = info.Enabled;

    /// <summary>
    /// Information about the configuration the playback observer uses.
    /// </summary>
    public ConfigurationInfo? Configuration { get; init; } = info.ConfigurationInfo is null ? null : new(info.ConfigurationInfo);

    /// <summary>
    /// Information about the plugin that the playback observer belongs to.
    /// </summary>
    [Required]
    public PluginInfo Plugin { get; init; } = new(info.PluginInfo);
}
