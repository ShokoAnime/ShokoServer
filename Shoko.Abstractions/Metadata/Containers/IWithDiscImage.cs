using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
///   Interface for entities that may have a disc image.
/// </summary>
public interface IWithDiscImage : IWithImages
{
    /// <summary>
    ///   The disc image of the entity. The preferred or first available
    ///   image of type <see cref="ImageEntityType.Disc"/> for the entity.
    /// </summary>
    IImage? DiscImage
        => GetBestImageForType(ImageEntityType.Disc);

    /// <summary>
    ///   The cross-reference for the disc image for the entity. Same as the
    ///   disc image, it returns the cross-reference for the preferred or
    ///   first available image of type <see cref="ImageEntityType.Disc"/>
    ///   for the entity.
    /// </summary>
    IImageCrossReference? DiscImageCrossReference
        => GetBestImageCrossReferenceForType(ImageEntityType.Disc);
}
