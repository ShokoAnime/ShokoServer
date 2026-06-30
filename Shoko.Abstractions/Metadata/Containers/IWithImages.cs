using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;

#pragma warning disable CS0618 // Type or member is obsolete
namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
///   Interface for entities that can have one or more images.
/// </summary>
public interface IWithImages : IMetadata
{
    #region Preferred Images

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
    ///   Get all preferred images for the entity.
    /// </summary>
    /// <returns>
    ///   All preferred images for the entity.
    /// </returns>
    IEnumerable<IImage> GetPreferredImages()
    {
        foreach (var imageType in Enum.GetValues<ImageEntityType>().Except([ImageEntityType.None]))
            if (GetPreferredImageForType(imageType) is { } image)
                yield return image;
    }

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

    /// <summary>
    ///   Get all cross-references for all preferred images for the entity.
    /// </summary>
    /// <returns>
    ///   All cross-references for all preferred images for the entity.
    /// </returns>
    IEnumerable<IImageCrossReference> GetPreferredImageCrossReferences()
    {
        foreach (var imageType in Enum.GetValues<ImageEntityType>().Except([ImageEntityType.None]))
            if (GetPreferredImageCrossReferenceForType(imageType) is { } image)
                yield return image;
    }

    #endregion

    #region Default Images

    /// <summary>
    ///   The cross-reference for the default primary image for the entity, if
    ///   it has one.
    /// </summary>
    IImageCrossReference? DefaultPrimaryImageCrossReference { get => null; }

    /// <summary>
    ///   The cross-reference for the default backdrop image for the entity, if
    ///   it has one.
    /// </summary>
    IImageCrossReference? DefaultBackdropImageCrossReference { get => null; }

    /// <summary>
    ///   The cross-reference for the default logo image for the entity, if
    ///   it has one.
    /// </summary>
    IImageCrossReference? DefaultLogoImageCrossReference { get => null; }

    /// <summary>
    ///   The cross-reference for the default banner image for the entity, if
    ///   it has one.
    /// </summary>
    IImageCrossReference? DefaultBannerImageCrossReference { get => null; }

    /// <summary>
    ///   The cross-reference for the default disc image for the entity, if
    ///   it has one.
    /// </summary>
    IImageCrossReference? DefaultDiscImageCrossReference { get => null; }

    /// <summary>
    ///   Get the default image for the given <paramref name="imageType"/> for
    ///   the entity, or <c>null</c> if no default image is set.
    /// </summary>
    /// <param name="imageType">
    ///   The image type to check.
    /// </param>
    /// <param name="primaryImage">
    ///   Optional. If <c>true</c>, will return the primary image for the
    ///   default image cross-reference. If <c>false</c>, will return the
    ///   non-primary image for the default image cross-reference.
    /// </param>
    /// <returns>
    ///   The default image metadata for the given type, or <c>null</c> if not
    ///   set.
    /// </returns>
    IImage? GetDefaultImageForType(ImageEntityType imageType, bool primaryImage = true)
        => GetDefaultImageCrossReferenceForType(imageType) is { } xref && (primaryImage ? xref.GetPrimaryImage() : xref.GetImage()) is { } image
            ? ImageStub.Wrap(image, xref, ISystemService.StaticServices.GetRequiredService<IImageManager>().IsLinkedCrossReference(this, xref))
            : null;

    /// <summary>
    ///   Get all default images for the entity.
    /// </summary>
    /// <returns>
    ///   An enumerable of image metadata for the default images.
    /// </returns>
    IEnumerable<IImage> GetDefaultImages()
    {
        foreach (var imageType in Enum.GetValues<ImageEntityType>().Except([ImageEntityType.None]))
            if (GetDefaultImageForType(imageType) is { } image)
                yield return image;
    }

    /// <summary>
    ///   Get the default image cross-reference for the given
    ///   <paramref name="imageType"/> for the entity, or <c>null</c> if no
    ///   default image is set.
    /// </summary>
    /// <param name="imageType">
    ///   The image type to check.
    /// </param>
    /// <returns>
    ///   The default image cross-reference for the given type, or <c>null</c> if
    ///   no default image is set.
    /// </returns>
    IImageCrossReference? GetDefaultImageCrossReferenceForType(ImageEntityType imageType)
        => imageType switch
        {
            ImageEntityType.Primary => DefaultPrimaryImageCrossReference,
            ImageEntityType.Backdrop => DefaultBackdropImageCrossReference,
            ImageEntityType.Logo => DefaultLogoImageCrossReference,
            ImageEntityType.Banner => DefaultBannerImageCrossReference,
            ImageEntityType.Disc => DefaultDiscImageCrossReference,
            _ => null,
        };

    /// <summary>
    ///   Get all cross-references for all default images for the entity.
    /// </summary>
    /// <returns>
    ///   An enumerable of image cross-references for the default images.
    /// </returns>
    IEnumerable<IImageCrossReference> GetDefaultImageCrossReferences()
    {
        foreach (var imageType in Enum.GetValues<ImageEntityType>().Except([ImageEntityType.None]))
            if (GetPreferredImageCrossReferenceForType(imageType) is { } image)
                yield return image;
    }

    #endregion

    #region Best Images

    /// <summary>
    ///   Get the best image for the given <paramref name="imageType"/> for the
    ///   entity, or <c>null</c> if no best image is set.
    /// </summary>
    /// <param name="imageType">
    ///   The image type to check.
    /// </param>
    /// <param name="primaryImage">
    ///   Optional. If <c>true</c>, will return the primary image for the
    ///   best image cross-reference. If <c>false</c>, will return the
    ///   non-primary image for the best image cross-reference.
    /// </param>
    /// <returns>
    ///   The best image metadata for the given type, or <c>null</c> if not
    ///   set.
    /// </returns>
    IImage? GetBestImageForType(ImageEntityType imageType, bool primaryImage = true)
        => GetBestImageCrossReferenceForType(imageType) is { } xref && (primaryImage ? xref.GetPrimaryImage() : xref.GetImage()) is { } image
            ? ImageStub.Wrap(image, xref, ISystemService.StaticServices.GetRequiredService<IImageManager>().IsLinkedCrossReference(this, xref))
            : null;

    /// <summary>
    ///   Get all best images for the entity.
    /// </summary>
    /// <param name="primaryImage">
    ///   Optional. If <c>true</c>, will return the primary image for the
    ///   best image cross-reference. If <c>false</c>, will return the
    ///   non-primary image for the best image cross-reference.
    /// </param>
    /// <returns>
    ///   An enumerable of image metadata for the best images.
    /// </returns>
    IEnumerable<IImage> GetBestImages(bool primaryImage = true)
    {
        foreach (var imageType in Enum.GetValues<ImageEntityType>().Except([ImageEntityType.None]))
            if (GetBestImageForType(imageType, primaryImage) is { } image)
                yield return image;
    }

    /// <summary>
    ///   Get the best image cross-reference for the given
    ///   <paramref name="imageType"/> for the entity, or <c>null</c> if no
    ///   best image is set.
    /// </summary>
    /// <param name="imageType">
    ///   The image type to check.
    /// </param>
    /// <param name="primaryImage">
    ///   Optional. If <c>true</c>, will check the primary image for the
    ///   best image cross-reference. If <c>false</c>, will check the
    ///   non-primary image for the best image cross-reference.
    /// </param>
    /// <returns>
    ///   The best image cross-reference for the given type, or <c>null</c> if
    ///   no best image is set.
    /// </returns>
    IImageCrossReference? GetBestImageCrossReferenceForType(ImageEntityType imageType, bool primaryImage = true)
    {
        if (primaryImage)
        {
            // If a preferred image is set and available for the entity, return it.
            if (GetPreferredImageCrossReferenceForType(imageType) is { IsEnabled: true, IsPrimaryAvailable: true } preferredImageCrossReference)
                return preferredImageCrossReference;

            // If a default image is set and available for the entity, return it.
            if (GetDefaultImageCrossReferenceForType(imageType) is { IsEnabled: true, IsPrimaryAvailable: true } defaultImageCrossReference)
                return defaultImageCrossReference;

            // Otherwise, return the first available image, first enabled image, or the first image.
            var selectedImageCrossReference = GetImageCrossReferences(imageType: imageType) is { Count: > 0 } xrefs ? (
                xrefs.FirstOrDefault(i => i is { IsEnabled: true, IsDesired: true, IsPrimaryAvailable: true }) ??
                xrefs.FirstOrDefault(i => i is { IsEnabled: true, IsPrimaryAvailable: true }) ??
                xrefs.FirstOrDefault(i => i is { IsEnabled: true, IsDesired: true }) ??
                xrefs.FirstOrDefault(i => i is { IsEnabled: true })
            ) : null;
            if (selectedImageCrossReference is not null)
                return selectedImageCrossReference;
        }
        else
        {
            if (GetPreferredImageCrossReferenceForType(imageType) is { IsEnabled: true, IsAvailable: true } preferredImageCrossReference)
                return preferredImageCrossReference;

            if (GetDefaultImageCrossReferenceForType(imageType) is { IsEnabled: true, IsAvailable: true } defaultImageCrossReference)
                return defaultImageCrossReference;

            var selectedImageCrossReference = GetImageCrossReferences(imageType: imageType) is { Count: > 0 } xrefs ? (
                xrefs.FirstOrDefault(i => i is { IsEnabled: true, IsDesired: true, IsAvailable: true }) ??
                xrefs.FirstOrDefault(i => i is { IsEnabled: true, IsAvailable: true }) ??
                xrefs.FirstOrDefault(i => i is { IsEnabled: true, IsDesired: true }) ??
                xrefs.FirstOrDefault(i => i is { IsEnabled: true })
            ) : null;
            if (selectedImageCrossReference is not null)
                return selectedImageCrossReference;
        }

        return null;
    }

    /// <summary>
    ///   Get all best image cross-references for the entity.
    /// </summary>
    /// <param name="primaryImage">
    ///   Optional. If <c>true</c>, will check the primary image for the
    ///   best image cross-reference. If <c>false</c>, will check the
    ///   non-primary image for the best image cross-reference.
    /// </param>
    /// <returns>
    ///   An enumerable of image cross-references for the best images.
    /// </returns>
    IEnumerable<IImageCrossReference> GetBestImageCrossReferences(bool primaryImage = true)
    {
        foreach (var imageType in Enum.GetValues<ImageEntityType>().Except([ImageEntityType.None]))
            if (GetBestImageCrossReferenceForType(imageType, primaryImage) is { } image)
                yield return image;
    }

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
