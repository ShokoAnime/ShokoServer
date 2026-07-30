using System;
using System.Collections.Generic;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Server.Services;

namespace Shoko.Server.Settings;

/// <summary>
/// Settings for the <see cref="VideoStreamPipelineService"/>.
/// <br/>
/// These are separate from the <see cref="ServerSettings"/> to prevent
/// clients from modifying them through the settings endpoint.
/// </summary>
public class VideoStreamPipelineSettings : INewtonsoftJsonConfiguration, IHiddenConfiguration
{
    /// <summary>
    /// A dictionary containing the enabled state of each transform by id.
    /// </summary>
    [Visibility(DisplayVisibility.ReadOnly)]
    public Dictionary<Guid, bool> TransformEnabled { get; set; } = [];

    /// <summary>
    /// A list of transform ids in order of priority.
    /// </summary>
    [Visibility(DisplayVisibility.ReadOnly)]
    public List<Guid> TransformPriority { get; set; } = [];

    /// <summary>
    /// A dictionary containing the enabled state of each playback observer by id.
    /// </summary>
    [Visibility(DisplayVisibility.ReadOnly)]
    public Dictionary<Guid, bool> ObserverEnabled { get; set; } = [];

    /// <summary>
    /// The nominal HLS segment duration to request from transforms, in seconds.
    /// </summary>
    public int DefaultSegmentDurationSeconds { get; set; } = 6;

    /// <summary>
    /// How long a stream session may sit idle (no segment/init requests) before
    /// it's evicted and its rendition disposed.
    /// </summary>
    public int SessionIdleTimeoutMinutes { get; set; } = 3;

    /// <summary>
    /// How long to wait for a requested segment to become available before
    /// giving up and returning an error to the client.
    /// </summary>
    public int SegmentRequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Optional override for the stream cache directory. Defaults to
    /// <see cref="Shoko.Abstractions.Plugin.IApplicationPaths.StreamCachePath"/> when unset.
    /// </summary>
    public string? CacheDirectoryOverride { get; set; }
}
