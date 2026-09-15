namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   An attachment served as a resource of a stream session, e.g. a font a
///   styled subtitle track needs.
/// </summary>
public class StreamAttachmentDescription
{
    /// <summary>
    ///   The position of the attachment, counting from zero.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    ///   The file name of the attachment.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    ///   The MIME type of the attachment, e.g. <c>font/ttf</c>.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    ///   The path of the resource, relative to the session.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    ///   Optional. The size of the attachment, in bytes.
    /// </summary>
    public long? Size { get; init; }
}
