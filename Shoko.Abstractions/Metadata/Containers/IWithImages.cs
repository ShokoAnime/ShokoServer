using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;

#pragma warning disable CS0618 // Type or member is obsolete
namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
///   Interface for entities that can have one or more images.
/// </summary>
public interface IWithImages : IMetadata
{
    #region Preferred Image

    /// <summary>
    ///   Get the preferred image for the given <paramref name="imageType"/> for
    ///   the entity, or <c>null</c> if no preferred image is set.
    /// </summary>
    /// <param name="imageType">
    ///   The image type to check.
    /// </param>
    /// <returns>
    ///   The preferred image metadata for the given type, or <c>null</c> if
    ///   not set.
    /// </returns>
    IImage? GetPreferredImageForType(ImageEntityType imageType)
        => GetImages(imageType: imageType).FirstOrDefault(image => image.IsPreferred);

    /// <summary>
    ///   Get the cross-reference for the preferred image for the given
    ///   <paramref name="imageType"/> for the entity, or <c>null</c> if no
    ///   preferred image is set. The cross-reference contains additional
    ///   metadata about the image-entity relationship including; ordering,
    ///   enabled state, and auto-download, etc..
    /// </summary>
    /// <param name="imageType">
    ///   The image type to check.
    /// </param>
    /// <returns>
    ///   The preferred image cross-reference for the given type, or <c>null</c> if
    ///   not set.
    /// </returns>
    IImageCrossReference? GetPreferredImageCrossReferenceForType(ImageEntityType imageType)
        => GetImageCrossReferences(imageType: imageType).FirstOrDefault(xref => xref.IsPreferred);

    #endregion

    #region Images

    /// <summary>
    ///   Get all or a filtered view of the images for the entity. Images can be
    ///   filtered by type, image source, cross-reference source, and enabled
    ///   state.
    /// </summary>
    /// <param name="imageSource">
    ///   Optional. If set, will restrict the returned list to only containing
    ///   images from the given source (e.g. AniDB, TMDB, AniList, User, etc.).
    /// </param>
    /// <param name="imageType">
    ///   Optional. If set, will restrict the returned list to only containing
    ///   the images of the given type.
    /// </param>
    /// <param name="xrefSource">
    ///   Optional. If set, will restrict the returned list to only containing
    ///   images that have cross-references from the given source (e.g. AniDB,
    ///   TMDB, AniList, User, etc.).
    /// </param>
    /// <param name="isEnabled">
    ///   Optional. Filter by enabled state. Pass <c>true</c> to get only
    ///   enabled, <c>false</c> to get only disabled, or <c>null</c> to get
    ///   both. Defaults to <c>null</c>.
    /// </param>
    /// <param name="isDesired">
    ///   Optional. Filter by desired state. Pass <c>true</c> to get only
    ///   desired, <c>false</c> to get only undesired, or <c>null</c> to get
    ///   both. Defaults to <c>null</c>.
    /// </param>
    /// <param name="isAvailable">
    ///   Optional. Filter by available state. Pass <c>true</c> to get only
    ///   available, <c>false</c> to get only unavailable, or <c>null</c> to
    ///   get both. Defaults to <c>null</c>.
    /// </param>
    /// <param name="primaryImage">
    ///   Optional. Set to <c>true</c> to retrieve the primary image if the
    ///   image is part of a linked image list.
    /// </param>
    /// <param name="linkedEntityImages">
    ///   Optional. Set to <c>false</c> to only retrieve the entity's own
    ///   images. Set to <c>true</c> to also retrieve images from other entities
    ///   linked to the entity. Set to <c>null</c> to let the service decide
    ///   based on the entity. Defaults to <c>null</c>.
    /// </param>
    /// <returns>
    ///   A read-only list of images that are linked to the entity, filtered by
    ///   the provided criteria.
    /// </returns>
    IReadOnlyList<IImage> GetImages(
        DataSource? imageSource = null,
        ImageEntityType? imageType = null,
        DataSource? xrefSource = null,
        bool? isEnabled = null,
        bool? isDesired = null,
        bool? isAvailable = null,
        bool primaryImage = false,
        bool? linkedEntityImages = null
    )
        => ISystemService.StaticServices.GetRequiredService<IImageManager>()
            .GetImagesForEntity(this, imageSource, imageType, xrefSource, isEnabled, isDesired, isAvailable, primaryImage, linkedEntityImages);

    /// <summary>
    ///   Get all or a filtered view of the image cross-references for the
    ///   entity. Can be filtered by image type, image source, cross-reference
    ///   source, and enabled state.
    /// </summary>
    /// <param name="imageSource">
    ///   Optional. If set, will restrict the returned list to only containing
    ///   cross-references for images from the given image source.
    /// </param>
    /// <param name="imageType">
    ///   Optional. If set, will restrict the returned list to only containing
    ///   the cross-references of the given image type.
    /// </param>
    /// <param name="xrefSource">
    ///   If set, will restrict the returned list to only containing
    ///   cross-references from the given source.
    /// </param>
    /// <param name="isEnabled">
    ///   Optional. Filter by enabled state. Pass <c>true</c> to get only
    ///   enabled, <c>false</c> to get only disabled, or <c>null</c> to get
    ///   both. Defaults to <c>null</c>.
    /// </param>
    /// <param name="isDesired">
    ///   Optional. Filter by desired state. Pass <c>true</c> to get only
    ///   desired, <c>false</c> to get only undesired, or <c>null</c> to get
    ///   both. Defaults to <c>null</c>.
    /// </param>
    /// <param name="isAvailable">
    ///   Optional. Filter by available state. Pass <c>true</c> to get only
    ///   available, <c>false</c> to get only unavailable, or <c>null</c> to
    ///   get both. Defaults to <c>null</c>.
    /// </param>
    /// <param name="primaryImage">
    ///   Optional. Set to <c>true</c> to retrieve the primary image if the
    ///   image is part of a linked image list.
    /// </param>
    /// <param name="linkedEntityImages">
    ///   Optional. Set to <c>false</c> to only retrieve cross-references for
    ///   the entity's own images. Set to <c>true</c> to also retrieve
    ///   cross-references for images from other entities linked to the entity.
    ///   Set to <c>null</c> to let the service decide based on the entity.
    ///   Defaults to <c>null</c>.
    /// </param>
    /// <returns>
    ///   A read-only list of cross-references that are linked to the entity,
    ///   filtered by the provided criteria.
    /// </returns>
    IReadOnlyList<IImageCrossReference> GetImageCrossReferences(
        DataSource? imageSource = null,
        ImageEntityType? imageType = null,
        DataSource? xrefSource = null,
        bool? isEnabled = null,
        bool? isDesired = null,
        bool? isAvailable = null,
        bool? primaryImage = null,
        bool? linkedEntityImages = null
    )
        => ISystemService.StaticServices.GetRequiredService<IImageManager>()
            .GetImageCrossReferencesForEntity(this, imageSource, imageType, xrefSource, isEnabled, isDesired, isAvailable, primaryImage, linkedEntityImages);

    #endregion
}
