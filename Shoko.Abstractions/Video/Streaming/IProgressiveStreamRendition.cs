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
///   Implementations should write output to a real file on disk that grows
///   over time (e.g. ffmpeg muxing directly to a file path, not a pipe) and
///   let the core poll/read from it as it grows -- the same idea as
///   <see cref="IHlsStreamRendition"/>'s segment-directory pattern, just one
///   continuous file instead of discrete segments. This mirrors how other
///   on-demand "direct stream" implementations (e.g. Jellyfin, Emby) already
///   serve a still-transcoding file over HTTP range requests.
///
///   A <see cref="OpenAsync"/> call for a byte offset outside what's
///   currently been produced (a seek) should be treated the same way
///   <see cref="IHlsStreamRendition.OpenSegmentAsync"/> implementations
///   already treat an out-of-window segment index: tear down and restart
///   underlying production seeked to the source time position implied by
///   the requested offset and <see cref="EstimatedBytesPerSecond"/>, with a
///   small backward overlap so decoder/filter state can warm up. This
///   mapping is necessarily approximate (bitrate is rarely perfectly
///   constant) -- exact frame-accurate seeking is not available in this
///   delivery mode, which is a disclosed trade-off against
///   <see cref="StreamDeliveryMode.Hls"/>'s exact segment-index addressing,
///   not a bug to work around.
/// </remarks>
public interface IProgressiveStreamRendition : IStreamRendition
{
    /// <summary>
    ///   Approximate output bitrate in bytes/second. The core has no other
    ///   way to know the eventual file size for a rendition that's still
    ///   being produced, so it uses
    ///   <c>EstimatedBytesPerSecond * video duration</c> as an estimated
    ///   total for <c>Content-Length</c>/<c>Content-Range</c> response
    ///   headers -- the actual produced file may end up smaller or larger
    ///   than that estimate; players built for on-demand transcoded
    ///   streaming already tolerate this.
    /// </summary>
    long EstimatedBytesPerSecond { get; }

    /// <summary>
    ///   Opens a stream yielding bytes starting at (approximately)
    ///   <paramref name="rangeStart"/> bytes into the estimated output.
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
