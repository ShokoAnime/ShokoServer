using System.Linq;
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
    {
        get =>
            GetPreferredImageForType(ImageEntityType.Logo) ??
            DefaultLogoImage ??
            (GetImages(imageType: ImageEntityType.Logo, primaryImage: true) is { Count: > 0 } images ? (
                images.FirstOrDefault(i => i is { IsEnabled: true, IsAvailable: true }) ??
                images.FirstOrDefault(i => i is { IsEnabled: true }) ??
                images.FirstOrDefault()
            ) : null);
    }

    /// <summary>
    ///   The cross-reference for the logo image for the entity. Same as the
    ///   logo image, it returns the cross-reference for the preferred or
    ///   first available image of type <see cref="ImageEntityType.Logo"/>
    ///   for the entity.
    /// </summary>
    IImageCrossReference? LogoImageCrossReference
    {
        get =>
            GetPreferredImageCrossReferenceForType(ImageEntityType.Logo) ??
            DefaultLogoImageCrossReference ??
            (GetImageCrossReferences(imageType: ImageEntityType.Logo) is { Count: > 0 } xrefs ? (
                xrefs.FirstOrDefault(xref => xref.GetPrimaryImage() is { IsEnabled: true, IsAvailable: true }) ??
                xrefs.FirstOrDefault(xref => xref is { IsEnabled: true }) ??
                xrefs.FirstOrDefault()
            ) : null);
    }

    /// <summary>
    ///   The default logo image for the entity, if it has one.
    /// </summary>
    IImage? DefaultLogoImage { get => null; }

    /// <summary>
    ///   The cross-reference for the default logo image for the entity, if
    ///   it has one.
    /// </summary>
    IImageCrossReference? DefaultLogoImageCrossReference { get => null; }
}
