namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   A resource of a stream session with no dedicated place in
///   <see cref="StreamDescription"/>.
/// </summary>
public class StreamExtraDescription
{
    /// <summary>
    ///   What the resource is, in lowercase kebab-case, e.g.
    ///   <c>seek-preview</c>.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    ///   The path of the resource, relative to the session.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    ///   The MIME type of the resource.
    /// </summary>
    public required string ContentType { get; init; }
}
