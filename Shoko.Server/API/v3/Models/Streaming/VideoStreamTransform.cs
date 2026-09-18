using System;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Video.Streaming;
using Shoko.Server.API.v3.Models.Configuration;
using Shoko.Server.API.v3.Models.Plugin;

namespace Shoko.Server.API.v3.Models.Streaming;

/// <summary>
/// A video stream transform.
/// </summary>
/// <param name="info">Internal video stream transform info.</param>
public class VideoStreamTransform(VideoStreamTransformInfo info)
{
    /// <summary>
    /// The unique ID of the transform.
    /// </summary>
    [Required]
    public string ID { get; init; } = info.ID;

    /// <summary>
    /// The version of the video stream transform.
    /// </summary>
    [Required]
    public Version Version { get; init; } = info.Version;

    /// <summary>
    /// The display name of the video stream transform.
    /// </summary>
    [Required]
    public string Name { get; init; } = info.Name;

    /// <summary>
    /// Describes what the video stream transform is for.
    /// </summary>
    [Required]
    public string Description { get; init; } = string.IsNullOrEmpty(info.Description) ? string.Empty : info.Description;

    /// <summary>
    /// The priority of the transform during automatic selection.
    /// </summary>
    [Required]
    public int Priority { get; init; } = info.Priority;

    /// <summary>
    /// Which delivery mechanism this transform's output uses -- determines
    /// whether the client should request <c>Stream/Hls/master.m3u8</c> or
    /// <c>Stream/Direct</c> for this transform.
    /// </summary>
    [Required]
    public StreamDeliveryMode DeliveryMode { get; init; } = info.Transform.DeliveryMode;

    /// <summary>
    /// Whether or not the transform is enabled for use.
    /// </summary>
    [Required]
    public bool IsEnabled { get; init; } = info.Enabled;

    /// <summary>
    /// Information about the configuration the video stream transform uses.
    /// </summary>
    public ConfigurationInfo? Configuration { get; init; } = info.ConfigurationInfo is null ? null : new(info.ConfigurationInfo);

    /// <summary>
    /// Information about the plugin that the video stream transform belongs to.
    /// </summary>
    [Required]
    public PluginInfo Plugin { get; init; } = new(info.PluginInfo);
}
