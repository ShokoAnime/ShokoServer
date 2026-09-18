namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   How an <see cref="IVideoStreamTransform"/>'s output is delivered to the
///   client. Determines which concrete <see cref="IStreamRendition"/> shape
///   <see cref="IVideoStreamTransform.GetRenditionAsync"/> returns, and which
///   set of <c>/Stream/...</c> endpoints apply.
/// </summary>
public enum StreamDeliveryMode
{
    /// <summary>
    ///   HLS VOD: either the core builds a manifest and requests the init
    ///   segment and media segments by index from an
    ///   <see cref="IHlsStreamRendition"/>, or an
    ///   <see cref="IHlsPresentationRendition"/> serves its own playlists and
    ///   segments. Adaptive, widely compatible, but cannot carry
    ///   styled/positioned (ASS/SSA) or image-based (PGS/VobSub) subtitle
    ///   tracks or chapters, and the core's own manifest carries a single
    ///   variant with no alternate audio -- see <see cref="Progressive"/> when
    ///   full container fidelity matters more than adaptive delivery.
    /// </summary>
    Hls,

    /// <summary>
    ///   A single (possibly still being produced) file served via HTTP
    ///   byte-range requests against an <see cref="IProgressiveStreamRendition"/>.
    ///   Full container fidelity -- e.g. all original audio/subtitle/chapter
    ///   tracks stream-copied alongside a transformed video track into one
    ///   MKV -- at the cost of adaptive segment delivery and exact
    ///   byte-range seek precision (a requested byte offset outside what's
    ///   been produced so far can only be approximately mapped back to a
    ///   source time position).
    /// </summary>
    Progressive,
}
