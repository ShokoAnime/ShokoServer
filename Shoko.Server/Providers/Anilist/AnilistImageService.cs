using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;

#nullable enable
namespace Shoko.Server.Providers.Anilist;

/// <summary>
/// Registers AniList images with the image manager. Mirrors
/// <see cref="TMDB.TmdbImageService"/>, with one difference: AniList exposes
/// exactly one image per type per entity, so when the linked image changes the
/// previous cross-reference for that type is unregistered.
/// </summary>
public class AnilistImageService(ILogger<AnilistImageService> logger, IImageManager imageManager)
{
    /// <summary>
    /// The CDN base every AniList image URL starts with when we have not seen
    /// anything else yet.
    /// </summary>
    public const string DefaultImageServerUrl = "https://s4.anilist.co/file/anilistcdn/";

    private const string CdnPathMarker = "/file/anilistcdn/";

    private static string _imageServerUrl = DefaultImageServerUrl;

    /// <summary>
    /// The CDN base observed on the most recent image URL AniList handed us,
    /// so the template URL follows AniList if they move their CDN. Falls back
    /// to <see cref="DefaultImageServerUrl"/>.
    /// </summary>
    public static string ImageServerUrl => _imageServerUrl;

    /// <summary>
    /// Convert an absolute AniList image URL into the resource ID stored on the
    /// image, which is the path relative to the CDN base. Also records the CDN
    /// base so <see cref="ImageServerUrl"/> stays current.
    /// </summary>
    /// <param name="url">The absolute image URL, or <see langword="null"/>.</param>
    /// <returns>The relative resource ID, or <see langword="null"/> if the URL is empty.</returns>
    public static string? ToResourceID(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var index = url.IndexOf(CdnPathMarker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return url;

        var baseUrl = url[..(index + CdnPathMarker.Length)];
        if (!string.Equals(baseUrl, _imageServerUrl, StringComparison.Ordinal))
            _imageServerUrl = baseUrl;

        return url[(index + CdnPathMarker.Length)..];
    }

    /// <summary>
    /// Convert a resource ID stored on an image back into the absolute image
    /// URL, using the CDN base from <see cref="ImageServerUrl"/>.
    /// </summary>
    /// <param name="resourceID">The relative resource ID, or <see langword="null"/>.</param>
    /// <returns>The absolute image URL, or <see langword="null"/> if the resource ID is empty.</returns>
    public static string? ToImageUrl(string? resourceID)
    {
        if (string.IsNullOrWhiteSpace(resourceID))
            return null;

        // Anything that never matched the CDN marker was stored as-is by
        // ToResourceID, so it is already absolute and must not be prefixed.
        if (resourceID.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || resourceID.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return resourceID;

        return _imageServerUrl + resourceID;
    }

    /// <summary>
    /// Register the image of the given type for the entity, unregister any
    /// other AniList image of that type still linked to it, and schedule the
    /// download if wanted.
    /// </summary>
    /// <param name="resourceID">The resource ID relative to the CDN, or <see langword="null"/> to unregister the type.</param>
    /// <param name="imageType">The image type.</param>
    /// <param name="entity">The entity.</param>
    /// <param name="isDesired">Whether the image should be downloaded per the settings.</param>
    /// <param name="forceDownload">Re-download even if already available.</param>
    public async Task SyncImageOfType(string? resourceID, ImageEntityType imageType, IWithImages entity, bool isDesired, bool forceDownload = false)
    {
        var existing = imageManager.GetImageCrossReferencesForEntity(entity, new() { ImageSource = DataSource.AniList, ImageType = imageType, XrefSource = DataSource.AniList });
        if (string.IsNullOrEmpty(resourceID))
        {
            foreach (var stale in existing)
                imageManager.RemoveImageCrossReference(stale);
            return;
        }

        var image = imageManager.GetImageBySourceAndRemoteResourceID(DataSource.AniList, resourceID)
            ?? imageManager.AddImage(new()
            {
                Source = DataSource.AniList,
                ResourceID = resourceID,
            });

        // AniList has one image per type, so anything else of this type is a leftover from before the image changed.
        foreach (var stale in existing.Where(xref => xref.ImageID != image.ID))
        {
            logger.LogDebug("Unregistering replaced AniList {ImageType} image. (ImageID={ImageID})", imageType, stale.ImageID);
            imageManager.RemoveImageCrossReference(stale);
        }

        var xref = existing.FirstOrDefault(xref => xref.ImageID == image.ID)
            ?? imageManager.AddImageCrossReference(entity, image, new() { ImageType = imageType, Source = DataSource.AniList, IsDesired = isDesired });
        imageManager.UpdateImageCrossReference(xref, new() { Ordering = 0, IsDesired = isDesired });

        if (!isDesired || (!forceDownload && image.IsAvailable))
            return;

        await imageManager.ScheduleDownloadOfImage(image, force: forceDownload).ConfigureAwait(false);
    }

    /// <summary>
    /// Remove every image cross-reference for the entity.
    /// </summary>
    public void PurgeImages(IWithImages entity)
    {
        if (!imageManager.TryGetMetadataForEntity(entity, out var entitySource, out var entityType, out var entityID, out _, out _, out _) || entitySource is not DataSource.AniList)
        {
            logger.LogWarning("Unable to purge images for {Type} with id {EntityId}", entityType.ToString().ToLowerInvariant(), entityID);
            return;
        }

        var imagesToRemove = imageManager.GetImageCrossReferencesForEntity(entity);
        logger.LogDebug("Removing {Count} image cross-references for {Type} with id {EntityId}", imagesToRemove.Count, entityType.ToString().ToLowerInvariant(), entityID);
        foreach (var xref in imagesToRemove)
            imageManager.RemoveImageCrossReference(xref);
    }
}
