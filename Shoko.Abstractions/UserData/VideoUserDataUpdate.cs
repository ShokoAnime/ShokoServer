using System;

namespace Shoko.Abstractions.UserData;

/// <summary>
///   Represents an update to the user-specific data associated with a video.
/// </summary>
public class VideoUserDataUpdate
{
    /// <summary>
    ///   Override or set the number of times the video has been played.
    /// </summary>
    public int? PlaybackCount { get; set; }

    /// <summary>
    ///   Indicates if <see cref="ProgressPosition"/> has been set to a value.
    /// </summary>
    public bool HasProgressPosition { get; private set; }

    private TimeSpan? _progressPosition;

    /// <summary>
    ///   Override or set the position in the video where playback was last
    ///   tracked, be it live during playback, when it was last paused, or when
    ///   it last ended.
    /// </summary>
    public TimeSpan? ProgressPosition
    {
        get => _progressPosition;
        set
        {
            HasProgressPosition = true;
            _progressPosition = value;
        }
    }

    private DateTime? _lastPlayedAt;

    /// <summary>
    ///   Indicates if <see cref="LastPlayedAt"/> has been set to a value.
    /// </summary>
    public bool HasLastPlayedAt { get; private set; }

    /// <summary>
    ///   Override or set the date and time the video was last played.
    /// </summary>
    public DateTime? LastPlayedAt
    {
        get => _lastPlayedAt;
        set
        {
            HasLastPlayedAt = true;
            _lastPlayedAt = value;
        }
    }

    /// <summary>
    ///   Override when the data was last updated. If not set, then the current
    ///   time will be used.
    /// </summary>
    public DateTime? LastUpdatedAt { get; set; }

    /// <summary>
    ///   Initializes a new instance of the <see cref="VideoUserDataUpdate"/> class.
    /// </summary>
    public VideoUserDataUpdate() { }

    /// <summary>
    ///   Initializes a new instance of the <see cref="VideoUserDataUpdate"/> class.
    /// </summary>
    /// <param name="userData">
    ///   An existing <see cref="IVideoUserData"/> to derive data from.
    /// </param>
    public VideoUserDataUpdate(IVideoUserData userData) : this()
    {
        PlaybackCount = userData.PlaybackCount;
        ProgressPosition = userData.ProgressPosition;
        LastPlayedAt = userData.LastPlayedAt;
        LastUpdatedAt = userData.LastUpdatedAt;
    }
}
