using System;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Image.CrossReferences;

namespace Shoko.Abstractions.Metadata.Image.Exceptions;

/// <summary>
///   Thrown when attempting to add a cross-reference for an image and entity
///   when one already exists.
/// /// </summary>
public class ImageCrossReferenceExistsException : Exception
{
    /// <summary>
    ///   The cross-reference for the image and entity.
    /// </summary>
    public required IImageCrossReference CrossReference { get; init; }

    /// <summary>
    ///   The image.
    /// </summary>
    public required IImage Image { get; init; }

    /// <summary>
    ///   The entity.
    /// </summary>
    public required IWithImages Entity { get; init; }
}
