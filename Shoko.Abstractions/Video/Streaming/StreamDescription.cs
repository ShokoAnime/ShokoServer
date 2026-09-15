using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   What a stream session offers a client beyond the stream itself: the
///   tracks it carries, and the subtitles, attachments, chapters and extras
///   served beside it through <see cref="IStreamRenditionResources"/>.
/// </summary>
public class StreamDescription
{
    /// <summary>
    ///   The video tracks, in ordinal order.
    /// </summary>
    public IReadOnlyList<StreamTrackDescription> VideoTracks { get; init; } = [];

    /// <summary>
    ///   The audio tracks, in ordinal order. For HLS, in the same order as the
    ///   master playlist's <c>EXT-X-MEDIA</c> audio renditions.
    /// </summary>
    public IReadOnlyList<StreamTrackDescription> AudioTracks { get; init; } = [];

    /// <summary>
    ///   The subtitle tracks served as resources, in ordinal order.
    /// </summary>
    public IReadOnlyList<StreamSubtitleDescription> Subtitles { get; init; } = [];

    /// <summary>
    ///   The attachments served as resources, e.g. fonts.
    /// </summary>
    public IReadOnlyList<StreamAttachmentDescription> Attachments { get; init; } = [];

    /// <summary>
    ///   The chapters, in playback order.
    /// </summary>
    public IReadOnlyList<StreamChapterDescription> Chapters { get; init; } = [];

    /// <summary>
    ///   Any other resources the rendition serves for clients that know them,
    ///   e.g. a low-resolution video for seek previews.
    /// </summary>
    public IReadOnlyList<StreamExtraDescription> Extras { get; init; } = [];

    /// <summary>
    ///   Optional. Rendition-specific data with no place above, e.g. why a
    ///   track was ranked where it is. Its shape is the rendition's own
    ///   contract.
    /// </summary>
    public JObject? Metadata { get; init; }
}
