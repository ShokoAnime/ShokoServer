using System;
using System.IO;

namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   A resource served by an <see cref="IStreamRenditionResources"/>, e.g. a
///   playlist, a segment, a subtitle track or a font.
/// </summary>
public class StreamResource
{
    /// <summary>
    ///   The content of the resource. The core disposes of it once the
    ///   response has been written.
    /// </summary>
    public required Stream Stream { get; init; }

    /// <summary>
    ///   The MIME type of the resource, e.g.
    ///   <c>application/vnd.apple.mpegurl</c> for a playlist,
    ///   <c>video/mp4</c> for a segment or <c>font/ttf</c> for a font.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    ///   Optional. An entity tag for the resource, without quotes. When set,
    ///   the core answers a matching <c>If-None-Match</c> with
    ///   <c>304 Not Modified</c>.
    /// </summary>
    public string? ETag { get; init; }

    /// <summary>
    ///   Optional. The playback position this resource starts at. Set it only
    ///   on the media segments that should count as playback progress (e.g.
    ///   the video segments, and not the audio segments of the same timeline);
    ///   enabled <see cref="IPlaybackObserver"/>s are notified for every
    ///   resource that has one.
    /// </summary>
    public TimeSpan? Position { get; init; }

    /// <summary>
    ///   Optional. The segment index to report to observers alongside
    ///   <see cref="Position"/>.
    /// </summary>
    public int? SegmentIndex { get; init; }

    /// <summary>
    ///   Whether this is the last media segment of the timeline
    ///   <see cref="Position"/> is on, i.e. playback reached the end.
    /// </summary>
    public bool IsFinalSegment { get; init; }
}
