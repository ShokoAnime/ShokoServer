using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Video;

namespace Shoko.Abstractions.User;

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
    /// Gets the position in the video where playback was last tracked, be it
    /// live during playback, when it was last paused, or when it last ended.
    /// </summary>
    TimeSpan ProgressPosition { get; }

    /// <summary>
    /// Gets the date and time when the video was last played.
    /// </summary>
    DateTime? LastPlayedAt { get; }

    /// <summary>
    ///   Indicates that the video has been watched to completion at least once
    ///   by the user locally.
    /// </summary>
    bool IsWatched => LastPlayedAt.HasValue || PlaybackCount > 0;

    /// <summary>
    /// Gets the video associated with this user data, if available.
    /// </summary>
    IVideo? Video { get; }

    /// <summary>
    ///   Gets the last selected video stream index, if known.
    /// </summary>
    int? LastVideoStreamIndex { get; }

    /// <summary>
    ///   Gets the last selected audio stream index, if known.
    /// </summary>
    int? LastAudioStreamIndex { get; }

    /// <summary>
    ///   Gets the last selected subtitle stream index, if known.
    /// </summary>
    int? LastSubtitleStreamIndex { get; }

    /// <summary>
    ///   Gets a read-only dictionary of client-specific data associated with
    ///   this video for the user. Each value is an opaque <see cref="JToken"/>
    ///   owned by the client/plugin that wrote it; the server does not
    ///   interpret its contents.
    /// </summary>
    IReadOnlyDictionary<string, JToken> ClientData { get; }
}
