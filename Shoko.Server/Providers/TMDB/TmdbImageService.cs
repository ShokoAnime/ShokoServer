using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Providers.TMDB;

/// <summary>
///   Links the one logo TMDB names for a company or network, and unlinks
///   the images of what TMDB's tables no longer hold.
/// </summary>
/// <remarks>
///   Every other TMDB image goes through the core's image job, which asks
///   the TMDB provider for its candidates.
/// </remarks>
/// <param name="logger">Where the unlinked images are logged.</param>
/// <param name="imageManager">Adds, links and unlinks the images.</param>
public class TmdbImageService(ILogger<TmdbImageService> logger, IImageManager imageManager)
{
    #region Image

    /// <summary>
    ///   Links one image to an entity, as its only image of the type from
    ///   TMDB, and schedules its download when it is wanted.
    /// </summary>
    /// <param name="filePath">TMDB's path for the image.</param>
    /// <param name="imageType">The image type.</param>
    /// <param name="entity">The entity.</param>
    /// <param name="isDesired">Whether the image is to be downloaded.</param>
    /// <param name="forceDownload">Whether to download it again even when it is there.</param>
    /// <returns>A task that completes once it is linked.</returns>
    public async Task DownloadImageByType(string filePath, ImageEntityType imageType, IWithImages entity, bool isDesired = true, bool forceDownload = false)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        filePath = SafeTransformResourceID(filePath);
        var image = imageManager.GetImageBySourceAndRemoteResourceID(MetadataSource.TMDB, filePath)
            ?? imageManager.AddImage(new()
            {
                Source = MetadataSource.TMDB,
                ResourceID = filePath,
            });

        var imageEntity = imageManager.GetImageCrossReferencesForEntity(entity)
            .FirstOrDefault(xref => xref.ImageType == imageType && xref.Source == MetadataSource.TMDB && xref.ImageID == image.ID) ??
            imageManager.AddImageCrossReference(entity, image, new() { ImageType = imageType, Source = MetadataSource.TMDB });
        imageManager.UpdateImageCrossReference(imageEntity, new() { Ordering = 0, IsDesired = isDesired });
        if (!forceDownload && image.IsAvailable)
            return;

        await imageManager.ScheduleDownloadOfImage(image, force: forceDownload).ConfigureAwait(false);
    }

    /// <summary>
    ///   Unlinks every image of a TMDB entity.
    /// </summary>
    /// <param name="entity">The entity, on TMDB.</param>
    public void PurgeImages(IWithImages entity)
    {
        var entityID = entity.ID;
        if (entityID.Source != MetadataSource.TMDB)
        {
            logger.LogWarning("Unable to purge images for {EntityID}", entityID);
            return;
        }

        var imagesToRemove = imageManager.GetImageCrossReferencesForEntity(entity);
        logger.LogDebug("Removing {count} image cross-references for {EntityID}", imagesToRemove.Count, entityID);
        foreach (var xref in imagesToRemove)
            imageManager.RemoveImageCrossReference(xref);
    }

    /// <summary>
    ///   The resource ID an image is stored under for TMDB's path: the path
    ///   without its leading slash, with an SVG asked for as a PNG.
    /// </summary>
    /// <param name="resourceID">TMDB's path for the image.</param>
    /// <returns>The resource ID.</returns>
    public static string SafeTransformResourceID(string resourceID)
        => resourceID.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? resourceID[1..^4] + ".png" : resourceID[1..];

    #endregion
}
