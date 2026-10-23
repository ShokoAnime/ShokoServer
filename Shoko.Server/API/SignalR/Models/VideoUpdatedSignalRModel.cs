using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Events;

namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// Dispatched when a video's own settings change.
/// </summary>
/// <param name="args">The event arguments.</param>
public class VideoUpdatedSignalRModel(VideoUpdatedEventArgs args)
{
    /// <summary>
    /// The video ID.
    /// </summary>
    public int FileID { get; } = args.Video.LocalID;

    /// <summary>
    /// What changed.
    /// </summary>
    [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
    public IReadOnlyList<VideoChanges> Changes { get; } = [.. Enum.GetValues<VideoChanges>().Where(change => change is not VideoChanges.None && args.Changes.HasFlag(change))];

    /// <summary>
    /// Whether the video is now marked as ignored.
    /// </summary>
    public bool IsIgnored { get; } = args.Video.IsIgnored;

    /// <summary>
    /// Whether the video is now marked as a variation.
    /// </summary>
    public bool IsVariation { get; } = args.Video.IsVariation;
}
