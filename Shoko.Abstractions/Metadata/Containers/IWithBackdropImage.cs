using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
///   Interface for entities that may have a backdrop image.
/// </summary>
public interface IWithBackdropImage : IWithImages
{
    /// <summary>
    ///   The backdrop image of the entity. The preferred or first available
    ///   image of type <see cref="ImageEntityType.Backdrop"/> for the entity.
    /// </summary>
    IImage? BackdropImage
        => GetBestImageForType(ImageEntityType.Backdrop);

    /// <summary>
    ///   The cross-reference for the backdrop image for the entity. Same as the
    ///   backdrop image, it returns the cross-reference for the preferred or
    ///   first available image of type <see cref="ImageEntityType.Backdrop"/>
    ///   for the entity.
    /// </summary>
    IImageCrossReference? BackdropImageCrossReference
        => GetBestImageCrossReferenceForType(ImageEntityType.Backdrop);
}
