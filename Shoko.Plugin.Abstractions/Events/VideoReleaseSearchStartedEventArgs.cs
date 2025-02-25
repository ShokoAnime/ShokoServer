
using System;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Dispatched when a video release search is started.
/// </summary>
/// <param name="video">The video.</param>
/// <param name="shouldSave">if set to <c>true</c> [should save].</param>
/// <param name="startedAt">The time the search started.</param>
public class VideoReleaseSearchStartedEventArgs(IVideo video, bool shouldSave, DateTime startedAt) : VideoEventArgs(video)
{
    /// <summary>
    /// Indicates if the found releases should be saved.
    /// </summary>
    public bool ShouldSave { get; } = shouldSave;

    /// <summary>
    /// The time the search started.
    /// </summary>
    public DateTime StartedAt { get; } = startedAt;
}
