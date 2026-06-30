using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
///   Interface for entities that may have a banner image.
/// </summary>
public interface IWithBannerImage : IWithImages
{
    /// <summary>
    ///   The banner image of the entity. The preferred or first available
    ///   image of type <see cref="ImageEntityType.Banner"/> for the entity.
    /// </summary>
    IImage? BannerImage
        => GetBestImageForType(ImageEntityType.Banner);

    /// <summary>
    ///   The cross-reference for the banner image for the entity. Same as the
    ///   banner image, it returns the cross-reference for the preferred or
    ///   first available image of type <see cref="ImageEntityType.Banner"/>
    ///   for the entity.
    /// </summary>
    IImageCrossReference? BannerImageCrossReference
        => GetBestImageCrossReferenceForType(ImageEntityType.Banner);
}
