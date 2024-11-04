using System;

namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Input data for updating a <see cref="IVideoUserData"/>.
/// </summary>
/// <param name="userData">An existing <see cref="IVideoUserData"/> to derive data from.</param>
public class VideoUserDataUpdate(IVideoUserData? userData = null)
{
    /// <summary>
    /// Override or set the number of times the video has been played.
    /// </summary>
    public int? PlaybackCount { get; set; } = userData?.PlaybackCount;

    /// <summary>
    /// Override or set the position at which the video should be resumed.
    /// </summary>
    public TimeSpan? ResumePosition { get; set; } = userData?.ResumePosition;

    /// <summary>
    /// Override or set the date and time the video was last played.
    /// </summary>
    public DateTime? LastPlayedAt { get; set; } = userData?.LastPlayedAt;

    /// <summary>
    /// Override when the data was last updated. If not set, then the current
    /// time will be used.
    /// </summary>
    public DateTime? LastUpdatedAt { get; set; } = userData?.LastUpdatedAt;
}
