using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   An <see cref="IStreamRendition"/> delivered as HLS where the rendition,
///   not the core, owns the whole presentation: the master playlist, every
///   media playlist it names, and every init and media segment those name.
///   Use it instead of <see cref="IHlsStreamRendition"/> when the core's
///   built-in playlist cannot describe the output, e.g. alternate audio
///   renditions (<c>EXT-X-MEDIA</c>), a bitrate ladder of variants, or
///   segments cut at irregular keyframe positions.
/// </summary>
/// <remarks>
///   The core mints the session, then redirects the client to
///   <c>Stream/Hls/{sessionID}/master.m3u8</c>. Every request under that
///   session path, including the master playlist itself, is handed to
///   <see cref="OpenResourceAsync"/> with the path relative to the session
///   root and the request's query string. URIs written into a playlist should
///   therefore be relative, and should carry any query parameters the client
///   needs on the next request (e.g. <c>apikey</c>), since a relative
///   reference does not inherit the playlist's query string.
/// </remarks>
public interface IHlsPresentationRendition : IStreamRendition
{
    /// <summary>
    ///   Opens a resource of the presentation. Implementations should wait for
    ///   a resource that is still being produced rather than failing; the core
    ///   cancels the request after its configured segment timeout.
    /// </summary>
    /// <param name="request">The requested resource.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The resource, or <c>null</c> if the path is not part of the presentation.</returns>
    Task<HlsResource?> OpenResourceAsync(HlsResourceRequest request, CancellationToken cancellationToken);
}
