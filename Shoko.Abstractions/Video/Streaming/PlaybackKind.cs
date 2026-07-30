namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   The delivery mechanism a <see cref="PlaybackProgressContext"/> was
///   observed on.
/// </summary>
public enum PlaybackKind
{
    /// <summary>
    ///   Raw byte-range passthrough streaming (the default <c>/Stream</c> endpoint).
    /// </summary>
    Progressive,

    /// <summary>
    ///   HLS segment streaming produced by an <see cref="IVideoStreamTransform"/>.
    /// </summary>
    Hls,
}
