using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   Optionally implemented by an <see cref="IStreamRendition"/> of either
///   delivery mode to serve resources beside its stream, e.g. subtitle tracks,
///   font attachments, or the playlists and segments of an
///   <see cref="IHlsPresentationRendition"/>.
/// </summary>
/// <remarks>
///   Resources are served under the session the rendition belongs to:
///   <c>Stream/Hls/{sessionID}/{path}</c> for
///   <see cref="StreamDeliveryMode.Hls"/> and
///   <c>Stream/Direct/{sessionID}/{path}</c> for
///   <see cref="StreamDeliveryMode.Progressive"/>. A relative reference to a
///   resource does not inherit the query string of the document it appears
///   in, so any URI a rendition writes should repeat the query parameters
///   (e.g. <c>apikey</c>) the next request needs.
/// </remarks>
public interface IStreamRenditionResources
{
    /// <summary>
    ///   Describes the tracks of the rendition and the resources it serves, for
    ///   the user and query string in <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The context of the request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The description.</returns>
    Task<StreamDescription> DescribeAsync(VideoStreamTransformContext context, CancellationToken cancellationToken);

    /// <summary>
    ///   Opens a resource of the rendition. For a resource that is still being
    ///   produced, throw <see cref="StreamResourceNotReadyException"/> so the
    ///   player retries, or wait for it; the core cancels a wait after its
    ///   configured segment timeout and answers <c>504</c>.
    /// </summary>
    /// <param name="request">The requested resource.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The resource, or <c>null</c> if the rendition has no resource at that path (<c>404</c>).</returns>
    /// <exception cref="StreamResourceNotFoundException">The resource does not exist; the core answers <c>404</c> with the reason.</exception>
    /// <exception cref="StreamResourceUnsupportedException">The request cannot be served; the core answers <c>400</c> with the reason.</exception>
    /// <exception cref="StreamResourceNotReadyException">The resource is not ready yet; the core answers <c>503</c> with the reason and any <c>Retry-After</c>.</exception>
    Task<StreamResource?> OpenResourceAsync(StreamResourceRequest request, CancellationToken cancellationToken);
}
