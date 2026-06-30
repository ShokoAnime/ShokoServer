using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
///   Interface for entities that may have a primary image.
/// </summary>
public interface IWithPrimaryImage : IWithImages
{
    /// <summary>
    ///   The primary image of the entity. The preferred or first available
    ///   image of type <see cref="ImageEntityType.Primary"/> for the entity.
    /// </summary>
    IImage? PrimaryImage
        => GetBestImageForType(ImageEntityType.Primary);

    /// <summary>
    ///   The cross-reference for the primary image for the entity. Same as the
    ///   primary image, it returns the cross-reference for the preferred or
    ///   first available image of type <see cref="ImageEntityType.Primary"/>
    ///   for the entity.
    /// </summary>
    IImageCrossReference? PrimaryImageCrossReference
        => GetBestImageCrossReferenceForType(ImageEntityType.Primary);
}
