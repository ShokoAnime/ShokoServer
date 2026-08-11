using System;

namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   Base type for a session-scoped rendition of a video produced by an
///   <see cref="IVideoStreamTransform"/>. Delivered either as HLS segments
///   (<see cref="IHlsStreamRendition"/>) or as a single progressively
///   downloaded file (<see cref="IProgressiveStreamRendition"/>), per the
///   producing transform's own <see cref="IVideoStreamTransform.DeliveryMode"/>
///   -- the core dispatches to the matching set of endpoints and casts to the
///   concrete interface, so an implementation only ever needs to implement
///   one of the two, never both.
/// </summary>
/// <remarks>
///   Implementations are free to produce output however they like (a single
///   long-lived ffmpeg process, a VapourSynth pipe feeding ffmpeg, etc.) --
///   the core only cares that output can eventually be opened as a
///   <see cref="System.IO.Stream"/>.
/// </remarks>
public interface IStreamRendition : IAsyncDisposable
{
    /// <summary>
    ///   The MIME type of the produced container (e.g. <c>video/mp4</c> for
    ///   fragmented MP4 HLS segments, <c>video/x-matroska</c> for a
    ///   progressive MKV rendition).
    /// </summary>
    string ContainerMimeType { get; }
}
