using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   An <see cref="IStreamRendition"/> delivered as an HLS VOD stream (the
///   original, and still default, delivery shape). The core turns this into
///   an HLS VOD manifest (<c>#EXT-X-PLAYLIST-TYPE:VOD</c>) and streams it to
///   the client as <c>init.mp4</c> + <c>segment-{index}.m4s</c> requests. The
///   core computes segment count from the video's known duration and
///   <see cref="SegmentDuration"/> -- an implementation does not need to
///   track or report total duration or segment count itself.
/// </summary>
/// <remarks>
///   Implementations should run one long-lived background process per active
///   viewing window rather than spinning up a fresh process per segment or
///   running the whole file up front:
///   <list type="bullet">
///     <item>Let the underlying tool own HLS segmenting (e.g. ffmpeg's own
///     muxer: <c>-f hls -hls_segment_type fmp4 -hls_time N
///     -hls_flags independent_segments -hls_list_size 0</c>) writing into a
///     session-scoped cache directory, and watch that directory for newly
///     produced segments to resolve <see cref="OpenSegmentAsync"/>.</item>
///     <item>If a requested segment index is far outside the currently
///     produced window (a seek), tear down the running process and restart
///     it seeked to that timestamp, with a small backward overlap (~1-2s) so
///     decoder/filter state can warm up before the requested segment.</item>
///   </list>
///   This is the same approach used by other on-demand HLS transcoders
///   (Jellyfin, Emby, Plex) -- it isn't novel, and the core abstraction
///   deliberately stays out of the way of however an implementation wants to
///   do it.
/// </remarks>
public interface IHlsStreamRendition : IStreamRendition
{
    /// <summary>
    ///   The nominal segment duration this rendition targets. Actual segment
    ///   durations may drift slightly; this is only used to compute the
    ///   segment count and manifest timings.
    /// </summary>
    TimeSpan SegmentDuration { get; }

    /// <summary>
    ///   Opens the fragmented-MP4 initialization segment for this rendition,
    ///   if the container format requires one.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The init segment stream, or <c>null</c> if not applicable.</returns>
    Task<Stream?> OpenInitSegmentAsync(CancellationToken cancellationToken);

    /// <summary>
    ///   Opens the media segment at the given index. Implementations should
    ///   produce segments in roughly sequential order; a request far outside
    ///   the currently produced window should be treated as a seek and may
    ///   restart internal production from that position.
    /// </summary>
    /// <param name="segmentIndex">The zero-based segment index.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The segment stream, or <c>null</c> if the index is out of range.</returns>
    Task<Stream?> OpenSegmentAsync(int segmentIndex, CancellationToken cancellationToken);
}
