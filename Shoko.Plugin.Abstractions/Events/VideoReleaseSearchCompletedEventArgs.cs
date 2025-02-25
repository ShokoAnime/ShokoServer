
using System;
using System.Diagnostics.CodeAnalysis;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Release;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Dispatched when a video release search is completed.
/// </summary>
/// <param name="video">The video.</param>
/// <param name="releaseInfo">The release info.</param>
/// <param name="isSaved">Indicates the found release is saved.</param>
/// <param name="startedAt">The time the search started.</param>
/// <param name="completedAt">The time the search completed.</param>
public class VideoReleaseSearchCompletedEventArgs(IVideo video, IReleaseInfo? releaseInfo, bool isSaved, DateTime startedAt, DateTime completedAt) : VideoEventArgs(video)
{
    /// <summary>
    /// The found release info, if successful.
    /// </summary>
    public IReleaseInfo? ReleaseInfo { get; } = releaseInfo;

    /// <summary>
    /// Indicates if the found releases should be saved.
    /// </summary>
    public bool IsSaved { get; } = isSaved;

    /// <summary>
    /// Indicates if the search was successful.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ReleaseInfo))]
    public bool IsSuccessful => ReleaseInfo is not null;

    /// <summary>
    /// The time the search started.
    /// </summary>
    public DateTime StartedAt { get; } = startedAt;

    /// <summary>
    /// The time the search completed.
    /// </summary>
    public DateTime CompletedAt { get; } = completedAt;
}
