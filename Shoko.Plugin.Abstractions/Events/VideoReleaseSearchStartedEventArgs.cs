
using System;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Dispatched when a video release search is started.
/// </summary>
public class VideoReleaseSearchStartedEventArgs : VideoEventArgs
{
    /// <summary>
    /// Indicates if the found releases should be saved.
    /// </summary>
    public required bool ShouldSave { get; init; }

    /// <summary>
    /// The time the search started.
    /// </summary>
    public required DateTime StartedAt { get; init; }
}
