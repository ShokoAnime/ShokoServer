using System;

namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Represents user-specific data associated with a video.
/// </summary>
public interface IVideoUserData : IUserData
{
    /// <summary>
    /// Gets the ID of the video.
    /// </summary>
    int VideoID { get; }

    /// <summary>
    /// Gets the number of times the video has been played.
    /// </summary>
    int PlaybackCount { get; }

    /// <summary>
    /// Gets the position in the video where playback was last resumed.
    /// </summary>
    TimeSpan ResumePosition { get; }

    /// <summary>
    /// Gets the date and time when the video was last played.
    /// </summary>
    DateTime? LastPlayedAt { get; }

    /// <summary>
    /// Gets the video associated with this user data.
    /// </summary>
    IVideo Video { get; }
}
