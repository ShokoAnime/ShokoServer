
using System;
using System.ComponentModel;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Release;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Dispatched when a video release is saved or deleted.
/// </summary>
public class VideoReleaseEventArgs : VideoEventArgs
{
    /// <summary>
    /// The release information for the video.
    /// </summary>
    public required IReleaseInfo ReleaseInfo { get; init; }
}
