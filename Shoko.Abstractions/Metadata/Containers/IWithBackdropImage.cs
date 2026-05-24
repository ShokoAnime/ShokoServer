using System.Linq;
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
    {
        get =>
            GetPreferredImageForType(ImageEntityType.Backdrop) ??
            DefaultBackdropImage ??
            (GetImages(imageType: ImageEntityType.Backdrop, primaryImage: true) is { Count: > 0 } images ? (
                images.FirstOrDefault(i => i is { IsEnabled: true, IsAvailable: true }) ??
                images.FirstOrDefault(i => i is { IsEnabled: true }) ??
                images.FirstOrDefault()
            ) : null);
    }

    /// <summary>
    ///   The cross-reference for the backdrop image for the entity. Same as the
    ///   backdrop image, it returns the cross-reference for the preferred or
    ///   first available image of type <see cref="ImageEntityType.Backdrop"/>
    ///   for the entity.
    /// </summary>
    IImageCrossReference? BackdropImageCrossReference
    {
        get =>
            GetPreferredImageCrossReferenceForType(ImageEntityType.Backdrop) ??
            DefaultBackdropImageCrossReference ??
            (GetImageCrossReferences(imageType: ImageEntityType.Backdrop) is { Count: > 0 } xrefs ? (
                xrefs.FirstOrDefault(xref => xref.GetPrimaryImage() is { IsEnabled: true, IsAvailable: true }) ??
                xrefs.FirstOrDefault(xref => xref is { IsEnabled: true }) ??
                xrefs.FirstOrDefault()
            ) : null);
    }

    /// <summary>
    ///   The default backdrop image for the entity, if it has one.
    /// </summary>
    IImage? DefaultBackdropImage { get => null; }

    /// <summary>
    ///   The cross-reference for the default backdrop image for the entity, if
    ///   it has one.
    /// </summary>
    IImageCrossReference? DefaultBackdropImageCrossReference { get => null; }
}
