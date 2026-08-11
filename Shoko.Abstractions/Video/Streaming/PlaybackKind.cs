namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   The delivery mechanism a <see cref="PlaybackProgressContext"/> was
///   observed on.
/// </summary>
public enum PlaybackKind
{
    /// <summary>
    ///   Byte-range streaming of a single file -- either raw passthrough
    ///   (the default <c>/Stream</c> endpoint) or a transform's
    ///   <see cref="IProgressiveStreamRendition"/> output.
    /// </summary>
    Progressive,

    /// <summary>
    ///   HLS segment streaming produced by an <see cref="IVideoStreamTransform"/>.
    /// </summary>
    Hls,
}
