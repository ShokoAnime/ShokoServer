using Microsoft.AspNetCore.Http;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   Context passed to an <see cref="IVideoStreamTransform"/> when checking
///   applicability or building a rendition for a stream request.
/// </summary>
public class VideoStreamTransformContext
{
    /// <summary>
    ///   The user requesting the stream, if known.
    /// </summary>
    public required IUser? User { get; init; }

    /// <summary>
    ///   The raw query string parameters from the originating HTTP request.
    ///   Lets a transform read custom parameters (e.g. a quality/profile hint,
    ///   or a legacy flag it wants to stay compatible with) without the core
    ///   needing to know about them.
    /// </summary>
    public required IQueryCollection QueryParameters { get; init; }
}
