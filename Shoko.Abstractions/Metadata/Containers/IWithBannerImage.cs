using System.Linq;
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
    {
        get =>
            GetPreferredImageForType(ImageEntityType.Banner) ??
            DefaultBannerImage ??
            (GetImages(imageType: ImageEntityType.Banner, primaryImage: true) is { Count: > 0 } images ? (
                images.FirstOrDefault(i => i is { IsEnabled: true, IsAvailable: true }) ??
                images.FirstOrDefault(i => i is { IsEnabled: true }) ??
                images.FirstOrDefault()
            ) : null);
    }

    /// <summary>
    ///   The cross-reference for the banner image for the entity. Same as the
    ///   banner image, it returns the cross-reference for the preferred or
    ///   first available image of type <see cref="ImageEntityType.Banner"/>
    ///   for the entity.
    /// </summary>
    IImageCrossReference? BannerImageCrossReference
    {
        get =>
            GetPreferredImageCrossReferenceForType(ImageEntityType.Banner) ??
            DefaultBannerImageCrossReference ??
            (GetImageCrossReferences(imageType: ImageEntityType.Banner) is { Count: > 0 } xrefs ? (
                xrefs.FirstOrDefault(xref => xref.GetPrimaryImage() is { IsEnabled: true, IsAvailable: true }) ??
                xrefs.FirstOrDefault(xref => xref is { IsEnabled: true }) ??
                xrefs.FirstOrDefault()
            ) : null);
    }

    /// <summary>
    ///   The default banner image for the entity, if it has one.
    /// </summary>
    IImage? DefaultBannerImage { get => null; }

    /// <summary>
    ///   The cross-reference for the default banner image for the entity, if
    ///   it has one.
    /// </summary>
    IImageCrossReference? DefaultBannerImageCrossReference { get => null; }
}
