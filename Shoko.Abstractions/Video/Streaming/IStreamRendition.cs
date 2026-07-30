using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   A session-scoped rendition of a video produced by an
///   <see cref="IVideoStreamTransform"/>, delivered as HLS segments.
/// </summary>
/// <remarks>
///   Implementations are free to produce segments however they like (a single
///   long-lived ffmpeg process, a VapourSynth pipe feeding ffmpeg, etc.) — the
///   core only cares that a segment can eventually be opened as a
///   <see cref="Stream"/>. The core computes the manifest (segment count, VOD
///   duration) itself from <see cref="SegmentDuration"/> and the video's known
///   duration, so implementations do not need to track or report that.
/// </remarks>
public interface IStreamRendition : IAsyncDisposable
{
    /// <summary>
    ///   The MIME type of the segment container (e.g. <c>video/mp4</c> for
    ///   fragmented MP4 segments).
    /// </summary>
    string ContainerMimeType { get; }

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
