using System.IO;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
/// Information about a package or plugin thumbnail.
/// </summary>
public sealed class PackageThumbnailInfo
{
    /// <summary>
    /// The mime type of the thumbnail image.
    /// </summary>
    public required string MimeType { get; init; }

    /// <summary>
    /// The width of the thumbnail image.
    /// </summary>
    public required uint Width { get; init; }

    /// <summary>
    /// The height of the thumbnail image.
    /// </summary>
    public required uint Height { get; init; }

    /// <summary>
    /// The path to the thumbnail image.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Get the thumbnail image stream.
    /// </summary>
    public Stream GetStream() => File.OpenRead(FilePath);
}
