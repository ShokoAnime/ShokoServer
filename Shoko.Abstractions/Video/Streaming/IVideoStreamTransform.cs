using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Config;

namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   Base interface for all video stream pre-processing transforms to
///   implement (e.g. transcoding, frame interpolation). Transforms are
///   opt-in — by default, streams are served as raw passthrough with no
///   processing. At most one transform is selected per stream session; a
///   transform that needs multiple internal processing steps (e.g. frame
///   interpolation followed by encoding) composes those steps itself.
/// </summary>
public interface IVideoStreamTransform
{
    /// <summary>
    ///   Friendly name of the transform.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   Optional. Description of the transform.
    /// </summary>
    string? Description { get => null; }

    /// <summary>
    ///   Version of the transform.
    /// </summary>
    Version Version { get => GetType().Assembly.GetName().Version ?? new Version(0, 0, 0, 0); }

    /// <summary>
    ///   Which delivery mechanism <see cref="GetRenditionAsync"/> produces.
    ///   Determines whether the core offers this transform's output as an
    ///   HLS manifest (the default) or as a progressive-download URL, and
    ///   which concrete <see cref="IStreamRendition"/> interface the
    ///   returned rendition must implement (<see cref="IHlsStreamRendition"/>
    ///   or <see cref="IProgressiveStreamRendition"/> respectively). Known
    ///   ahead of creating a rendition so the core can pick the right URL
    ///   scheme without invoking the transform first.
    /// </summary>
    StreamDeliveryMode DeliveryMode { get => StreamDeliveryMode.Hls; }

    /// <summary>
    ///   Checks whether this transform can handle the given video right now
    ///   (e.g. required hardware/tooling is available, the source codec is
    ///   supported). This is called before the transform is offered to a
    ///   client or selected automatically, so it should be a cheap,
    ///   synchronous-feeling check rather than a real capability probe done
    ///   per request.
    /// </summary>
    /// <param name="video">The video to check.</param>
    /// <param name="context">The stream transform context.</param>
    /// <returns><c>true</c> if this transform can produce a rendition for the video.</returns>
    bool SupportsVideo(IVideo video, VideoStreamTransformContext context);

    /// <summary>
    ///   Creates a new session-scoped rendition of the video. The caller owns
    ///   the returned rendition's lifetime and will dispose of it when the
    ///   stream session expires.
    /// </summary>
    /// <param name="video">The video to produce a rendition for.</param>
    /// <param name="context">The stream transform context.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The rendition.</returns>
    Task<IStreamRendition> GetRenditionAsync(IVideo video, VideoStreamTransformContext context, CancellationToken cancellationToken);
}

/// <summary>
///   Indicates that the video stream transform supports configuration, and
///   which configuration type to display in the UI.
/// </summary>
/// <typeparam name="TConfiguration">The transform configuration type.</typeparam>
public interface IVideoStreamTransform<TConfiguration> : IVideoStreamTransform where TConfiguration : IVideoStreamTransformConfiguration { }
