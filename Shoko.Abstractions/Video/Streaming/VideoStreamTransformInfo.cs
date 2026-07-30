using System;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
/// Contains information about a <see cref="IVideoStreamTransform"/>.
/// </summary>
public class VideoStreamTransformInfo
{
    /// <summary>
    /// The unique ID of the transform.
    /// </summary>
    public required Guid ID { get; init; }

    /// <summary>
    /// The version of the video stream transform.
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    /// The display name of the video stream transform.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Describes what the video stream transform is for.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The <see cref="IVideoStreamTransform"/> that this info is for.
    /// </summary>
    public required IVideoStreamTransform Transform { get; init; }

    /// <summary>
    /// Information about the configuration that the video stream transform uses.
    /// </summary>
    public required ConfigurationInfo? ConfigurationInfo { get; init; }

    /// <summary>
    /// Information about the plugin that the video stream transform belongs to.
    /// </summary>
    public required LocalPluginInfo PluginInfo { get; init; }

    /// <summary>
    /// Whether or not the transform is enabled for use.
    /// </summary>
    public required bool Enabled { get; set; }

    /// <summary>
    /// The priority of the transform during automatic selection.
    /// </summary>
    public required int Priority { get; set; }
}
