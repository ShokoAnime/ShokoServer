namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   A video or audio track of a stream session.
/// </summary>
public class StreamTrackDescription
{
    /// <summary>
    ///   The position of the track among the tracks of its kind, counting from
    ///   zero.
    /// </summary>
    public required int Ordinal { get; init; }

    /// <summary>
    ///   Optional. The codec name, e.g. <c>h264</c> or <c>aac</c>.
    /// </summary>
    public string? Codec { get; init; }

    /// <summary>
    ///   Optional. The RFC 6381 codec string, e.g. <c>avc1.640028</c>.
    /// </summary>
    public string? CodecString { get; init; }

    /// <summary>
    ///   Optional. The language of the track, as a BCP 47 tag.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    ///   Optional. The title of the track.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    ///   Optional. The width of a video track, in pixels.
    /// </summary>
    public int? Width { get; init; }

    /// <summary>
    ///   Optional. The height of a video track, in pixels.
    /// </summary>
    public int? Height { get; init; }

    /// <summary>
    ///   Optional. The channel count of an audio track.
    /// </summary>
    public int? Channels { get; init; }

    /// <summary>
    ///   Whether the track is the one played when the client picks none.
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    ///   Optional. The preference order of the track among the tracks of its
    ///   kind for the requesting user, best first, counting from zero.
    /// </summary>
    public int? Rank { get; init; }
}
