
using System;
using System.ComponentModel;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Release;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Dispatched when a video release is saved or deleted.
/// </summary>
/// <param name="video">The video.</param>
/// <param name="releaseInfo">The release info.</param>
public class VideoReleaseEventArgs(IVideo video, IReleaseInfo releaseInfo)
{
    /// <summary>
    /// The video.
    /// </summary>
    public IVideo Video { get; } = video;

    /// <summary>
    /// The release information for the video.
    /// </summary>
    public IReleaseInfo ReleaseInfo { get; } = releaseInfo;
}
