using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   An <see cref="IStreamRendition"/> delivered as a single, possibly still-
///   growing, file served via HTTP byte-range requests -- full container
///   fidelity (multi-track audio, subtitle formats HLS can't carry like
///   ASS/PGS, chapters, all stream-copied through unmodified) at the cost of
///   adaptive delivery and exact seek precision.
/// </summary>
/// <remarks>
///   Implementations typically write output to a real file on disk that grows
///   over time (e.g. ffmpeg muxing directly to a file path, not a pipe) and
///   let the core poll/read from it as it grows -- the same idea as
///   <see cref="IHlsStreamRendition"/>'s segment-directory pattern, just one
///   continuous file instead of discrete segments. This mirrors how other
///   on-demand "direct stream" implementations (e.g. Jellyfin, Emby) already
///   serve a still-transcoding file over HTTP range requests. A rendition is
///   equally free to proxy someone else's HTTP resource; the core only ever
///   asks it for a stream at an offset.
///
///   A <see cref="OpenAsync"/> call for a byte offset outside what's
///   currently been produced (a seek) should be treated the same way
///   <see cref="IHlsStreamRendition.OpenSegmentAsync"/> implementations
///   already treat an out-of-window segment index: tear down and restart
///   underlying production seeked to the source time position implied by
///   the requested offset, with a small backward overlap so decoder/filter
///   state can warm up. Translating that offset into a time is the
///   rendition's own business, and is necessarily approximate for a
///   variable-bitrate encode -- exact frame-accurate seeking is not
///   available in this delivery mode, which is a disclosed trade-off against
///   <see cref="StreamDeliveryMode.Hls"/>'s exact segment-index addressing,
///   not a bug to work around.
/// </remarks>
public interface IProgressiveStreamRendition : IStreamRendition
{
    /// <summary>
    ///   How long the rendition declares its output to be, in bytes, or
    ///   <c>null</c> if it cannot say.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Necessarily an estimate: the output is still being produced and a
    ///     variable-bitrate encoder has not decided its real size yet. The
    ///     rendition is nonetheless the only party that can name it, because
    ///     this is not merely a response header -- it is the scale of the
    ///     byte-to-time map the rendition inverts when the core hands back an
    ///     offset from a seek, and for some implementations it is also the
    ///     scale the served container's own seek index was built at. A total
    ///     computed anywhere else would put every seek out by the ratio
    ///     between the two numbers, silently and proportionally, so the core
    ///     asks rather than calculates.
    ///   </para>
    ///   <para>
    ///     Must be settled before the first <see cref="OpenAsync"/> call and
    ///     stable thereafter: it is what the core sends as
    ///     <c>Content-Length</c> and <c>Content-Range</c>, so it has to exist
    ///     before any byte goes out and must not move underneath a client
    ///     mid-session.
    ///   </para>
    ///   <para>
    ///     <c>null</c> turns byte-range serving off for this rendition
    ///     entirely -- the core answers <c>200</c> from the start of the
    ///     stream and ignores any <c>Range</c> header, because a <c>206</c>
    ///     must name a concrete last-byte-position and there would be none to
    ///     name. A rendition that wants to be seekable has to commit to a
    ///     length.
    ///   </para>
    /// </remarks>
    long? EstimatedTotalBytes { get; }

    /// <summary>
    ///   Opens a stream yielding bytes starting at (approximately)
    ///   <paramref name="rangeStart"/> bytes into the declared output.
    /// </summary>
    /// <param name="rangeStart">Requested byte offset, or <c>null</c> for the start of the stream.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    ///   A stream positioned at (approximately) <paramref name="rangeStart"/>,
    ///   or <c>null</c> if that offset can't be served (e.g. past the end of
    ///   the video).
    /// </returns>
    Task<Stream?> OpenAsync(long? rangeStart, CancellationToken cancellationToken);
}
