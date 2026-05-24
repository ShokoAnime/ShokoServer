using System.Linq;
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
    {
        get =>
            GetPreferredImageForType(ImageEntityType.Disc) ??
            DefaultDiscImage ??
            (GetImages(imageType: ImageEntityType.Disc, primaryImage: true) is { Count: > 0 } images ? (
                images.FirstOrDefault(i => i is { IsEnabled: true, IsAvailable: true }) ??
                images.FirstOrDefault(i => i is { IsEnabled: true }) ??
                images.FirstOrDefault()
            ) : null);
    }

    /// <summary>
    ///   The cross-reference for the disc image for the entity. Same as the
    ///   disc image, it returns the cross-reference for the preferred or
    ///   first available image of type <see cref="ImageEntityType.Disc"/>
    ///   for the entity.
    /// </summary>
    IImageCrossReference? DiscImageCrossReference
    {
        get =>
            GetPreferredImageCrossReferenceForType(ImageEntityType.Disc) ??
            DefaultDiscImageCrossReference ??
            (GetImageCrossReferences(imageType: ImageEntityType.Disc) is { Count: > 0 } xrefs ? (
                xrefs.FirstOrDefault(xref => xref.GetPrimaryImage() is { IsEnabled: true, IsAvailable: true }) ??
                xrefs.FirstOrDefault(xref => xref is { IsEnabled: true }) ??
                xrefs.FirstOrDefault()
            ) : null);
    }

    /// <summary>
    ///   The default disc image for the entity, if it has one.
    /// </summary>
    IImage? DefaultDiscImage { get => null; }

    /// <summary>
    ///   The cross-reference for the default disc image for the entity, if
    ///   it has one.
    /// </summary>
    IImageCrossReference? DefaultDiscImageCrossReference { get => null; }
}
