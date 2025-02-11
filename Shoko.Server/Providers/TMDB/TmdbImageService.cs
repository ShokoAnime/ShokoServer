using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Server;
using TMDbLib.Objects.General;

// Suggestions we don't need in this file.
#pragma warning disable CA1822
#pragma warning disable CA1826

#nullable enable
namespace Shoko.Server.Providers.TMDB;

public class TmdbImageService
{
    private readonly ILogger<TmdbImageService> _logger;

    private readonly ISchedulerFactory _schedulerFactory;

    private readonly TMDB_ImageRepository _tmdbImages;

    private readonly TMDB_Image_EntityRepository _tmdbImageEntities;

    private readonly AniDB_Anime_PreferredImageRepository _preferredImages;

    public TmdbImageService(
        ILogger<TmdbImageService> logger,
        ISchedulerFactory schedulerFactory,
        TMDB_ImageRepository tmdbImages,
        TMDB_Image_EntityRepository tmdbImageEntities,
        AniDB_Anime_PreferredImageRepository preferredImages
    )
    {
        _logger = logger;
        _schedulerFactory = schedulerFactory;
        _tmdbImages = tmdbImages;
        _tmdbImageEntities = tmdbImageEntities;
        _preferredImages = preferredImages;
    }

    #region Image

    public async Task DownloadImageByType(string filePath, ImageEntityType type, ForeignEntityType foreignType, int foreignId, bool forceDownload = false)
    {
        var image = _tmdbImages.GetByRemoteFileName(filePath) ?? new(filePath);
        if (string.IsNullOrEmpty(image.LocalPath))
            return;

        _tmdbImages.Save(image);

        // Skip downloading if it already exists and we're not forcing it.
        if (File.Exists(image.LocalPath) && !forceDownload)
            return;

        await (await _schedulerFactory.GetScheduler().ConfigureAwait(false)).StartJob<DownloadTmdbImageJob>(c =>
        {
            c.ImageID = image.TMDB_ImageID;
            c.ImageType = image.ImageType;
            c.ForceDownload = forceDownload;
        });
    }

    public async Task DownloadImagesByType(DateOnly? releasedAt, IReadOnlyList<ImageData> images, ImageEntityType type, ForeignEntityType foreignType, int foreignId, int maxCount, List<TitleLanguage> languages, bool forceDownload = false)
    {
        var count = 0;
        var isLimitEnabled = maxCount > 0;
        var validImages = images
            .Select(a => a.FilePath)
            .ToHashSet();
        if (languages.Count > 0)
            images = isLimitEnabled
                ? images
                    .Select(image => (Image: image, Language: (image.Iso_639_1 ?? string.Empty).GetTitleLanguage()))
                    .Where(x => languages.Contains(x.Language))
                    .OrderBy(x => languages.IndexOf(x.Language))
                    .Select(x => x.Image)
                    .ToList()
                : images
                    .Where(x => languages.Contains((x.Iso_639_1 ?? string.Empty).GetTitleLanguage()))
                    .ToList();
        foreach (var imageData in images)
        {
            if (isLimitEnabled && count >= maxCount)
                break;

            count++;
            var image = _tmdbImages.GetByRemoteFileName(imageData.FilePath) ?? new(imageData.FilePath, type);
            var updated = image.Populate(imageData);
            if (updated)
                _tmdbImages.Save(image);

            var imageEntity = _tmdbImageEntities.GetByForeignIDAndTypeAndRemoteFileName(foreignId, foreignType, type, imageData.FilePath) ?? new(imageData.FilePath, type, foreignType, foreignId);
            updated = imageEntity.Populate(count, releasedAt);
            if (updated)
                _tmdbImageEntities.Save(imageEntity);
        }

        count = 0;
        var scheduler = await _schedulerFactory.GetScheduler();
        var storedImages = _tmdbImages.GetByForeignIDAndType(foreignId, foreignType, type);
        if (languages.Count > 0 && isLimitEnabled)
            storedImages = storedImages
                .OrderBy(x => languages.IndexOf(x.Language) is var index && index >= 0 ? index : int.MaxValue)
                .ToList();
        foreach (var image in storedImages)
        {
            // Clean up invalid entries.
            var path = image.LocalPath;
            if (string.IsNullOrEmpty(path))
            {
                RemoveImageFromEntity(image, type, foreignType, foreignId);
                continue;
            }

            // Remove the relation to the image if it's not linked to the foreign entity anymore.
            if (!validImages.Contains(image.RemoteFileName))
            {
                RemoveImageFromEntity(image, type, foreignType, foreignId);
                continue;
            }

            // Delete it from the local cache and database if we've reached the limit.
            if (isLimitEnabled && count >= maxCount)
            {
                RemoveImageFromEntity(image, type, foreignType, foreignId);
                continue;
            }

            // Skip downloading if it already exists and we're not forcing it.
            if (!forceDownload && File.Exists(path))
                continue;

            // Otherwise scheduled the image to be downloaded.
            await scheduler.StartJob<DownloadTmdbImageJob>(c =>
            {
                c.ImageID = image.TMDB_ImageID;
                c.ImageType = image.ImageType;
                c.ForceDownload = forceDownload;
            });
            count++;
        }
    }

    public void PurgeAllUnusedImages()
    {
        var toRemove = new List<TMDB_Image>();
        foreach (var image in _tmdbImages.GetAll())
        {
            var references = _tmdbImageEntities.GetByRemoteFileName(image.RemoteFileName);
            if (references.Count == 0)
                toRemove.Add(image);
        }

        _logger.LogDebug(
            "Removing {count} unused images",
            toRemove.Count
        );

        foreach (var image in toRemove)
            RemoveImageFromEntity(image, null, ForeignEntityType.None, 0);
    }

    public void PurgeImages(ForeignEntityType foreignType, int foreignId)
    {
        var imagesToRemove = _tmdbImages.GetByForeignID(foreignId, foreignType);

        _logger.LogDebug(
            "Removing {count} images for {type} with id {EntityId}",
            imagesToRemove.Count,
            foreignType.ToString().ToLowerInvariant(),
            foreignId);
        foreach (var image in imagesToRemove)
            RemoveImageFromEntity(image, null, foreignType, foreignId);
    }

    private void RemoveImageFromEntity(TMDB_Image image, ImageEntityType? imageType, ForeignEntityType? foreignType, int? foreignId)
    {
        if (foreignType.HasValue && foreignId.HasValue)
        {
            var entity = _tmdbImageEntities.GetByForeignID(foreignId.Value, foreignType.Value)
                .Where(x => !imageType.HasValue || x.ImageType == imageType.Value)
                .ToList();
            _tmdbImageEntities.Delete(entity);
            if (_tmdbImageEntities.GetByRemoteFileName(image.RemoteFileName).Count > 0)
                return;
        }

        // Only delete the image metadata and/or file if all references were removed.
        if (!string.IsNullOrEmpty(image.LocalPath) && File.Exists(image.LocalPath))
            File.Delete(image.LocalPath);

        _tmdbImages.Delete(image.TMDB_ImageID);
    }

    public void ResetPreferredImage(int anidbAnimeId, ForeignEntityType foreignType, int foreignId)
    {
        var images = _preferredImages.GetByAnimeID(anidbAnimeId);
        foreach (var defaultImage in images)
        {
            if (defaultImage.ImageSource == DataSourceType.TMDB)
            {
                var image = _tmdbImages.GetByID(defaultImage.ImageID);
                if (image == null)
                {
                    _logger.LogTrace("Removing preferred image for anime {AnimeId} because the preferred image could not be found.", anidbAnimeId);
                    _preferredImages.Delete(defaultImage);
                }
                else if (_tmdbImageEntities.GetByForeignIDAndTypeAndRemoteFileName(foreignId, foreignType, defaultImage.ImageType, image.RemoteFileName) is { })
                {
                    _logger.LogTrace("Removing preferred image for anime {AnimeId} because it belongs to now TMDB {Type} {Id}", anidbAnimeId, foreignType.ToString(), foreignId);
                    _preferredImages.Delete(defaultImage);
                }
            }
        }
    }

    #endregion
}
