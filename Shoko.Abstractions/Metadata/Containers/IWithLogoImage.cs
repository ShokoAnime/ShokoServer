using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
///   Interface for entities that may have a logo image.
/// </summary>
public interface IWithLogoImage : IWithImages
{
    /// <summary>
    ///   The logo image of the entity. The preferred or first available
    ///   image of type <see cref="ImageEntityType.Logo"/> for the entity.
    /// </summary>
    IImage? LogoImage
        => GetBestImageForType(ImageEntityType.Logo);

    /// <summary>
    ///   The cross-reference for the logo image for the entity. Same as the
    ///   logo image, it returns the cross-reference for the preferred or
    ///   first available image of type <see cref="ImageEntityType.Logo"/>
    ///   for the entity.
    /// </summary>
    IImageCrossReference? LogoImageCrossReference
        => GetBestImageCrossReferenceForType(ImageEntityType.Logo);
}
