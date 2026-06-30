using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Image.Options;
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
        => GetImages(new() { ImageType = imageType, IsPreferred = true }).FirstOrDefault();

    /// <summary>
    ///   Get all preferred images for the entity.
    /// </summary>
    /// <returns>
    ///   All preferred images for the entity.
    /// </returns>
    IReadOnlyList<IImage> GetPreferredImages()
        => GetImages(new() { IsPreferred = true });

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
        => GetImageCrossReferences(new() { ImageType = imageType, IsPreferred = true }).FirstOrDefault();

    /// <summary>
    ///   Get all cross-references for all preferred images for the entity.
    /// </summary>
    /// <returns>
    ///   All cross-references for all preferred images for the entity.
    /// </returns>
    IReadOnlyList<IImageCrossReference> GetPreferredImageCrossReferences()
        => GetImageCrossReferences(new() { IsPreferred = true });

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
            var selectedImageCrossReference = GetImageCrossReferences(new() { ImageType = imageType }) is { Count: > 0 } xrefs ? (
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

            var selectedImageCrossReference = GetImageCrossReferences(new() { ImageType = imageType }) is { Count: > 0 } xrefs ? (
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
    ///   Get all or a filtered view of the images for the entity, using the
    ///   specified <paramref name="options"/>.
    /// </summary>
    /// <param name="options">
    ///   Optional filtering options. See <see cref="ImageFilteringOptions"/>
    ///   for available filter fields. Pass <c>null</c> to return all images
    ///   for the entity.
    /// </param>
    /// <returns>
    ///   A read-only list of images for the entity, filtered by the
    ///   provided criteria.
    /// </returns>
    IReadOnlyList<IImage> GetImages(ImageFilteringOptions? options = null)
        => ISystemService.StaticServices.GetRequiredService<IImageManager>()
            .GetImagesForEntity(this, options);

    /// <summary>
    ///   Get all or a filtered view of the image cross-references for the
    ///   entity, using the specified <paramref name="options"/>.
    /// </summary>
    /// <param name="options">
    ///   Optional filtering options. See
    ///   <see cref="ImageCrossReferenceFilteringOptions"/> for available
    ///   filter fields. Pass <c>null</c> to return all cross-references
    ///   for the entity.
    /// </param>
    /// <returns>
    ///   A read-only list of cross-references for the entity, filtered by
    ///   the provided criteria.
    /// </returns>
    IReadOnlyList<IImageCrossReference> GetImageCrossReferences(ImageCrossReferenceFilteringOptions? options = null)
        => ISystemService.StaticServices.GetRequiredService<IImageManager>()
            .GetImageCrossReferencesForEntity(this, options);

    #endregion
}
