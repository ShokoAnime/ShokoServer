using System;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Image.Exceptions;

/// <summary>
///   Thrown when attempting to add image data for a resource which already
///   exists.
/// </summary>
public class ImageDataExistsException() : Exception($"An image with the provided source and resource ID already exists.")
{
    /// <summary>
    ///   The source of the image.
    /// </summary>
    public required DataSource ImageSource { get; init; }

    /// <summary>
    ///   Remote identifier relative to the <see cref="ImageSource"/>. This will
    ///   be the MD5 hash digest of the resource for user uploaded resources.
    /// </summary>
    public required string ImageResourceID { get; init; }
}
