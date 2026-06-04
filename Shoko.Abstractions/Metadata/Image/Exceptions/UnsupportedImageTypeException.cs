using System;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Image.Exceptions;

/// <summary>
///   Thrown when attempting to add or download image data for a resource
///   whose file extension maps to an unsupported image MIME type.
/// </summary>
public class UnsupportedImageTypeException() : Exception($"The resource identifier contains a file extension that maps to an unsupported image type.")
{
    /// <summary>
    ///   The source of the image.
    /// </summary>
    public required DataSource ImageSource { get; init; }

    /// <summary>
    ///   Remote identifier relative to the <see cref="ImageSource"/>.
    /// </summary>
    public required string ImageResourceID { get; init; }

    /// <summary>
    ///   The file extension detected in the resource identifier.
    /// </summary>
    public required string FileExtension { get; init; }

    /// <summary>
    ///   The MIME type that the extension mapped to.
    /// </summary>
    public required string DetectedMimeType { get; init; }
}
