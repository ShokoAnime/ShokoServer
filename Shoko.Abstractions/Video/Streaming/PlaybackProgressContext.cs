using System;
using Microsoft.AspNetCore.Http;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   Describes a single unit of playback progress (a served byte-range or
///   HLS segment) for an <see cref="IPlaybackObserver"/> to consume.
/// </summary>
public class PlaybackProgressContext
{
    /// <summary>
    /// The video being played.
    /// </summary>
    public required IVideo Video { get; init; }

    /// <summary>
    /// The user playing the video, if known.
    /// </summary>
    public IUser? User { get; init; }

    /// <summary>
    ///   The raw query string parameters from the originating HTTP request.
    ///   Lets an observer read custom parameters — e.g. a legacy per-request
    ///   opt-in flag it wants to stay backwards-compatible with — without the
    ///   core needing to know about them.
    /// </summary>
    public required IQueryCollection QueryParameters { get; init; }

    /// <summary>
    /// Which delivery mechanism this progress was observed on.
    /// </summary>
    public required PlaybackKind Kind { get; init; }

    /// <summary>
    /// The best-known position reached in the video, if it could be determined.
    /// </summary>
    public TimeSpan? Position { get; init; }

    /// <summary>
    /// The total duration of the video, if known.
    /// </summary>
    public TimeSpan? TotalDuration { get; init; }

    /// <summary>
    /// The start of the served byte range. Only set for <see cref="PlaybackKind.Progressive"/>.
    /// </summary>
    public long? RangeStart { get; init; }

    /// <summary>
    /// The end of the served byte range. Only set for <see cref="PlaybackKind.Progressive"/>.
    /// </summary>
    public long? RangeEnd { get; init; }

    /// <summary>
    /// The total file size, in bytes. Only set for <see cref="PlaybackKind.Progressive"/>.
    /// </summary>
    public long? TotalBytes { get; init; }

    /// <summary>
    /// The segment index served. Only set for <see cref="PlaybackKind.Hls"/>.
    /// </summary>
    public int? SegmentIndex { get; init; }

    /// <summary>
    /// Whether this was the last byte-range/segment of the video, i.e. playback reached the end.
    /// </summary>
    public required bool IsFinalUnit { get; init; }
}
