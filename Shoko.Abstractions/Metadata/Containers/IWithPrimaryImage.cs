using System.Linq;
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
    {
        get =>
            GetPreferredImageForType(ImageEntityType.Primary) ??
            DefaultPrimaryImage ??
            GetImages(imageType: ImageEntityType.Primary, primaryImage: true).FirstOrDefault();
    }

    /// <summary>
    ///   The cross-reference for the primary image for the entity. Same as the
    ///   primary image, it returns the cross-reference for the preferred or
    ///   first available image of type <see cref="ImageEntityType.Primary"/>
    ///   for the entity.
    /// </summary>
    IImageCrossReference? PrimaryImageCrossReference
    {
        get =>
            GetPreferredImageCrossReferenceForType(ImageEntityType.Primary) ??
            DefaultPrimaryImageCrossReference ??
            GetImageCrossReferences(imageType: ImageEntityType.Primary, primaryImage: true).FirstOrDefault();
    }

    /// <summary>
    ///   The default primary image for the entity, if it has one.
    /// </summary>
    IImage? DefaultPrimaryImage { get => null; }

    /// <summary>
    ///   The cross-reference for the default primary image for the entity, if
    ///   it has one.
    /// </summary>
    IImageCrossReference? DefaultPrimaryImageCrossReference { get => null; }
}
