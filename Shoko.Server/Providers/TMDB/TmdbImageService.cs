using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using TMDbLib.Objects.General;

namespace Shoko.Server.Providers.TMDB;

public class TmdbImageService(ILogger<TmdbImageService> logger, IImageManager imageManager)
{
    #region Image

    public async Task DownloadImageByType(string filePath, ImageEntityType imageType, IWithImages entity, bool isDesired = true, bool forceDownload = false)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        filePath = SafeTransformResourceID(filePath);
        var image = imageManager.GetImageBySourceAndRemoteResourceID(DataSource.TMDB, filePath)
            ?? imageManager.AddImage(new()
            {
                Source = DataSource.TMDB,
                ResourceID = filePath,
            });

        var imageEntity = imageManager.GetImageCrossReferencesForEntity(entity)
            .FirstOrDefault(xref => xref.ImageType == imageType && xref.Source == DataSource.TMDB && xref.ImageID == image.ID) ??
            imageManager.AddImageCrossReference(entity, image, new() { ImageType = imageType, Source = DataSource.TMDB });
        imageManager.UpdateImageCrossReference(imageEntity, new() { Ordering = 0, IsDesired = isDesired });
        if (!forceDownload && image.IsAvailable)
            return;

        await imageManager.ScheduleDownloadOfImage(image, force: forceDownload).ConfigureAwait(false);
    }

    public async Task DownloadImagesByType(
        string? defaultForType,
        IReadOnlyList<ImageData> images,
        ImageEntityType imageType,
        IWithImages entity,
        int maxCount,
        List<TitleLanguage> languages,
        bool forceDownload = false
    )
    {
        var defaultId = string.IsNullOrEmpty(defaultForType)
            ? (Guid?)null
            : IImageManager.GetIDForImageSourceAndResourceID(DataSource.TMDB, defaultForType);
        var desiredImages = images
            .Select((image, index) => (Image: image, Language: (image?.Iso_639_1 ?? string.Empty).GetTitleLanguage(), Index: index))
            .Where(tuple => !string.IsNullOrEmpty(tuple.Image.FilePath) && (languages.Count == 0 || languages.Contains(tuple.Language)))
            .OrderBy(tuple => languages.IndexOf(tuple.Language))
            .ThenBy(tuple => tuple.Index)
            .Take(maxCount > 0 ? maxCount : int.MaxValue)
            .Select(tuple => IImageManager.GetIDForImageSourceAndResourceID(DataSource.TMDB, SafeTransformResourceID(tuple.Image.FilePath!)))
            .ToHashSet();
        var orderedImages = images
            .Where(a => !string.IsNullOrEmpty(a.FilePath))
            .Select((image, index) => (Image: image, Language: (image?.Iso_639_1 ?? string.Empty).GetTitleLanguage(), Index: index))
            .OrderByDescending(tuple => languages.Contains(tuple.Language))
            .ThenBy(tuple => languages.IndexOf(tuple.Language))
            .ThenBy(tuple => tuple.Index)
            .Select((tuple, index) => (tuple.Image, index))
            .ToList();
        var xrefs = imageManager.GetImageCrossReferencesForEntity(entity, new() { ImageSource = DataSource.TMDB, ImageType = imageType, XrefSource = DataSource.TMDB })
            .ToDictionary(xref => xref.ImageID);
        var validImageCrossReferences = new HashSet<Guid>();
        foreach (var (imageData, index) in orderedImages)
        {
            var imageFilePath = SafeTransformResourceID(imageData.FilePath!);
            if (!validImageCrossReferences.Add(IImageManager.GetIDForImageSourceAndResourceID(DataSource.TMDB, imageFilePath)))
                continue;

            var image = imageManager.GetImageBySourceAndRemoteResourceID(DataSource.TMDB, imageFilePath)
                ?? imageManager.AddImage(new()
                {
                    Source = DataSource.TMDB,
                    ResourceID = imageFilePath,
                    Width = imageData.Width,
                    Height = imageData.Height,
                    LanguageCode = imageData.Iso_639_1,
                    CountryCode = imageData.Iso_3166_1,
                });
            var isDesired = desiredImages.Contains(image.ID) || (defaultId.HasValue && image.ID == defaultId.Value);
            if (!xrefs.TryGetValue(image.ID, out var xref))
            {
                var data = new ImageCrossReferenceData()
                {
                    ImageType = imageType,
                    Source = DataSource.TMDB,
                    Ordering = index,
                    IsDesired = isDesired,
                    IsEnabled = true,
                };
                if (imageData.VoteCount > 0 && imageData.VoteAverage >= 1)
                {
                    data.Rating = imageData.VoteAverage;
                    data.RatingVotes = imageData.VoteCount;
                }
                xref = imageManager.AddImageCrossReference(entity, image, data);
            }
            else
            {
                var updateData = new ImageCrossReferenceUpdateData()
                {
                    Ordering = index,
                    IsDesired = isDesired,
                };
                if (imageData.VoteCount > 0 && imageData.VoteAverage >= 1)
                {
                    updateData.Rating = imageData.VoteAverage;
                    updateData.RatingVotes = imageData.VoteCount;
                }
                xref = imageManager.UpdateImageCrossReference(xref, updateData);
            }
        }

        foreach (var xref in xrefs.Values.Where(xref => !validImageCrossReferences.Contains(xref.ImageID)))
            imageManager.RemoveImageCrossReference(xref);

        await imageManager.ScheduleAutoDownloadsForEntity(entity, xrefSource: DataSource.TMDB, force: forceDownload).ConfigureAwait(false);
    }

    public void PurgeImages(IWithImages entity)
    {
        if (!imageManager.TryGetMetadataForEntity(entity, out var entitySource, out var entityType, out var entityID, out _, out _, out _))
        {
            logger.LogWarning("Unable to purge images for {type} with id {EntityId}", entityType.ToString().ToLowerInvariant(), entityID);
            return;
        }

        if (entitySource is not DataSource.TMDB)
        {
            logger.LogWarning("Unable to purge images for {type} with id {EntityId}", entityType.ToString().ToLowerInvariant(), entityID);
            return;
        }

        var imagesToRemove = imageManager.GetImageCrossReferencesForEntity(entity);
        logger.LogDebug(
            "Removing {count} image cross-references for {type} with id {EntityId}",
            imagesToRemove.Count,
            entityType.ToString().ToLowerInvariant(),
            entityID);
        foreach (var xref in imagesToRemove)
            imageManager.RemoveImageCrossReference(xref);
    }

    public static string SafeTransformResourceID(string resourceID)
        => resourceID.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? resourceID[1..^4] + ".png" : resourceID[1..];

    #endregion
}
