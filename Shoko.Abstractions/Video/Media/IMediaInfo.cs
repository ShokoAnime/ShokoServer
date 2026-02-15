using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Video.Media;

/// <summary>
/// Media container metadata.
/// </summary>
public interface IMediaInfo
{
    /// <summary>
    /// General title for the media.
    /// </summary>
    string? Title { get; }

    /// <summary>
    /// Overall duration of the media.
    /// </summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// Overall bit-rate across all streams in the media container.
    /// </summary>
    int BitRate { get; }

    /// <summary>
    /// Average frame-rate across all the streams in the media container.
    /// </summary>
    decimal FrameRate { get; }

    /// <summary>
    /// Date when encoding took place, if known.
    /// </summary>
    DateTime? Encoded { get; }

    /// <summary>
    /// Indicates the media is streaming-friendly.
    /// </summary>
    bool IsStreamable { get; }

    /// <summary>
    /// Common file extension for the media container format.
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// The media container format name.
    /// </summary>
    string ContainerName { get; }

    /// <summary>
    /// The media container format version.
    /// </summary>
    int ContainerVersion { get; }

    /// <summary>
    /// First available video stream in the media container.
    /// </summary>
    IVideoStream? VideoStream { get; }

    /// <summary>
    /// Video streams in the media container.
    /// </summary>
    IReadOnlyList<IVideoStream> VideoStreams { get; }

    /// <summary>
    /// Audio streams in the media container.
    /// </summary>
    IReadOnlyList<IAudioStream> AudioStreams { get; }

    /// <summary>
    /// Sub-title (text) streams in the media container.
    /// </summary>
    IReadOnlyList<ITextStream> TextStreams { get; }

    /// <summary>
    /// Chapter information present in the media container.
    /// </summary>
    IReadOnlyList<IChapterInfo> Chapters { get; }

    /// <summary>
    /// Container file attachments.
    /// </summary>
    IReadOnlyList<string> Attachments { get; }
}
