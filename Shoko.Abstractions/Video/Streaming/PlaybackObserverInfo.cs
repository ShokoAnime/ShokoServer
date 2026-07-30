using System;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
/// Contains information about a <see cref="IPlaybackObserver"/>.
/// </summary>
public class PlaybackObserverInfo
{
    /// <summary>
    /// The unique ID of the observer.
    /// </summary>
    public required Guid ID { get; init; }

    /// <summary>
    /// The version of the playback observer.
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    /// The display name of the playback observer.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Describes what the playback observer is for.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The <see cref="IPlaybackObserver"/> that this info is for.
    /// </summary>
    public required IPlaybackObserver Observer { get; init; }

    /// <summary>
    /// Information about the configuration that the playback observer uses.
    /// </summary>
    public required ConfigurationInfo? ConfigurationInfo { get; init; }

    /// <summary>
    /// Information about the plugin that the playback observer belongs to.
    /// </summary>
    public required LocalPluginInfo PluginInfo { get; init; }

    /// <summary>
    /// Whether or not the observer is enabled. All enabled observers run on
    /// every stream request — there is no priority ordering.
    /// </summary>
    public required bool Enabled { get; set; }
}
