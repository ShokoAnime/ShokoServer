using Microsoft.AspNetCore.Http;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   A request for one resource of an <see cref="IHlsPresentationRendition"/>.
/// </summary>
public class HlsResourceRequest
{
    /// <summary>
    ///   The path of the resource relative to the session root, e.g.
    ///   <c>master.m3u8</c> or <c>audio/1/segment-4.m4s</c>. Taken from the
    ///   request URL, so it must be validated before being used to address
    ///   anything on disk.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    ///   The user making the request, if known.
    /// </summary>
    public IUser? User { get; init; }

    /// <summary>
    ///   The raw query string parameters from the request.
    /// </summary>
    public required IQueryCollection QueryParameters { get; init; }
}
