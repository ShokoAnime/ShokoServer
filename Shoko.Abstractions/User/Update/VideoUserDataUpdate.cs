using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Shoko.Abstractions.User.Update;

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
            if (value is not null && value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(ProgressPosition), "Progress position cannot be less than zero.");
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
    ///   When set to <c>true</c>, will prevent the watch status from
    ///   propagating to any episodes associated with the video.
    /// </summary>
    public bool NoEpisodePropagation { get; set; }

    /// <summary>
    ///   Indicates if <see cref="LastVideoStreamIndex"/> has been set to a
    ///   value.
    /// </summary>
    public bool HasLastVideoStreamIndex { get; private set; }

    private int? _lastVideoStreamIndex;

    /// <summary>
    ///   Override or set the last selected video stream index.
    /// </summary>
    public int? LastVideoStreamIndex
    {
        get => _lastVideoStreamIndex;
        set
        {
            HasLastVideoStreamIndex = true;
            _lastVideoStreamIndex = value;
        }
    }

    /// <summary>
    ///   Indicates if <see cref="LastAudioStreamIndex"/> has been set to a
    ///   value.
    /// </summary>
    public bool HasLastAudioStreamIndex { get; private set; }

    private int? _lastAudioStreamIndex;

    /// <summary>
    ///   Override or set the last selected audio stream index.
    /// </summary>
    public int? LastAudioStreamIndex
    {
        get => _lastAudioStreamIndex;
        set
        {
            HasLastAudioStreamIndex = true;
            _lastAudioStreamIndex = value;
        }
    }

    /// <summary>
    ///   Indicates if <see cref="LastSubtitleStreamIndex"/> has been set to a
    ///   value.
    /// </summary>
    public bool HasLastSubtitleStreamIndex { get; private set; }

    private int? _lastSubtitleStreamIndex;

    /// <summary>
    ///   Override or set the last selected subtitle stream index.
    /// </summary>
    public int? LastSubtitleStreamIndex
    {
        get => _lastSubtitleStreamIndex;
        set
        {
            HasLastSubtitleStreamIndex = true;
            _lastSubtitleStreamIndex = value;
        }
    }

    /// <summary>
    ///   When set to <c>true</c>, will clear all client data stored for this
    ///   video.
    /// </summary>
    public bool ClearClientData { get; set; }

    internal Dictionary<string, JToken?> _pendingClientData = new();

    /// <summary>
    ///   Gets the pending client data changes (key-value pairs).
    /// </summary>
    public IReadOnlyDictionary<string, JToken?> PendingClientData => _pendingClientData;

    /// <summary>
    ///   Set a client-specific data value. Pass a <c>null</c> reference to
    ///   remove the key. To store an explicit JSON <c>null</c>, pass a
    ///   <see cref="JValue"/> created via <see cref="JValue.CreateNull"/>.
    /// </summary>
    public void SetClientData(string clientKey, JToken? value)
        => _pendingClientData[clientKey] = value;

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
        LastVideoStreamIndex = userData.LastVideoStreamIndex;
        LastAudioStreamIndex = userData.LastAudioStreamIndex;
        LastSubtitleStreamIndex = userData.LastSubtitleStreamIndex;
    }
}
