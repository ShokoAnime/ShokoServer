using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentNHibernate.Data;
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

    private readonly AniDB_Episode_PreferredImageRepository _preferredEpisodeImages;

    public TmdbImageService(
        ILogger<TmdbImageService> logger,
        ISchedulerFactory schedulerFactory,
        TMDB_ImageRepository tmdbImages,
        TMDB_Image_EntityRepository tmdbImageEntities,
        AniDB_Anime_PreferredImageRepository preferredImages,
        AniDB_Episode_PreferredImageRepository preferredEpisodeImages
    )
    {
        _logger = logger;
        _schedulerFactory = schedulerFactory;
        _tmdbImages = tmdbImages;
        _tmdbImageEntities = tmdbImageEntities;
        _preferredImages = preferredImages;
        _preferredEpisodeImages = preferredEpisodeImages;
    }

    #region Image

    public async Task DownloadImageByType(string filePath, ImageEntityType imageType, ForeignEntityType foreignType, int foreignId, bool forceDownload = false)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        var image = _tmdbImages.GetByRemoteFileName(filePath) ?? new(filePath);
        var updated = image.TMDB_ImageID is 0;
        if (updated)
            _tmdbImages.Save(image);

        var imageEntity = _tmdbImageEntities.GetByForeignIDAndTypeAndRemoteFileName(foreignId, foreignType, imageType, filePath) ?? new(filePath, imageType, foreignType, foreignId);
        updated = imageEntity.Populate(0, null);
        if (updated)
            _tmdbImageEntities.Save(imageEntity);

        if (!forceDownload && File.Exists(image.LocalPath))
            return;

        await (await _schedulerFactory.GetScheduler().ConfigureAwait(false)).StartJob<DownloadTmdbImageJob>(c =>
        {
            c.ImageID = image.TMDB_ImageID;
            c.ImageType = image.ImageType;
            c.ForceDownload = forceDownload;
        });
    }

    public async Task DownloadImagesByType(
        string? defaultForType,
        DateOnly? releasedAt,
        IReadOnlyList<ImageData> images,
        ImageEntityType imageType,
        ForeignEntityType foreignType,
        int foreignId,
        int maxCount,
        List<TitleLanguage> languages,
        bool forceDownload = false
    )
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var isLimitEnabled = maxCount > 0;
        var validImages = images.Select(a => a.FilePath).Where(a => !string.IsNullOrEmpty(a)).ToHashSet();
        var visitedImages = new HashSet<string>();
        var orderedImages = images
            .Select((image, index) => (Image: image, Language: (image.GetLanguageCode() ?? string.Empty).GetTitleLanguage(), Index: index))
            .Where(tuple => !string.IsNullOrEmpty(tuple.Image.FilePath) && (languages.Count == 0 || languages.Contains(tuple.Language)))
            .OrderBy(tuple => languages.IndexOf(tuple.Language))
            .ThenBy(tuple => tuple.Index)
            .Take(isLimitEnabled ? maxCount : int.MaxValue)
            .OrderByDescending(tuple => tuple.Image.FilePath.Equals(defaultForType))
            .ThenBy(tuple => tuple.Index)
            .Select((tuple, index) => (tuple.Image, index))
            .ToList();
        foreach (var (imageData, index) in orderedImages)
        {
            visitedImages.Add(imageData.FilePath);

            var image = _tmdbImages.GetByRemoteFileName(imageData.FilePath) ?? new(imageData.FilePath, imageType);
            var updated = image.Populate(imageData);
            if (updated)
                _tmdbImages.Save(image);

            var imageEntity = _tmdbImageEntities.GetByForeignIDAndTypeAndRemoteFileName(foreignId, foreignType, imageType, imageData.FilePath) ?? new(imageData.FilePath, imageType, foreignType, foreignId);
            updated = imageEntity.Populate(index, releasedAt);
            if (updated)
                _tmdbImageEntities.Save(imageEntity);

            if (!forceDownload && File.Exists(image.LocalPath))
                continue;

            await scheduler.StartJob<DownloadTmdbImageJob>(c =>
            {
                c.ImageID = image.TMDB_ImageID;
                c.ImageType = image.ImageType;
                c.ForceDownload = forceDownload;
            });
        }

        var count = visitedImages.Count;
        var storedImages = _tmdbImageEntities.GetByForeignIDAndType(foreignId, foreignType, imageType)
            .Select(e => (Image: e.GetTmdbImage(), Entity: e))
            .Where(x => x.Image is null || !visitedImages.Contains(x.Image.RemoteFileName))
            .OrderByDescending(x => x.Entity.RemoteFileName.Equals(defaultForType))
            .ThenBy(x => x.Image is null ? int.MinValue : languages.IndexOf(x.Image.Language) is var index && index >= 0 ? index : int.MaxValue)
            .ThenBy(x => x.Entity.Ordering)
            .ToList();
        foreach (var (image, imageEntity) in storedImages)
        {
            if (image is null)
            {
                _tmdbImageEntities.Delete(imageEntity);
                continue;
            }

            if (!validImages.Contains(image.RemoteFileName) || (isLimitEnabled && count >= maxCount))
            {
                RemoveImageFromEntity(image, foreignType, foreignId, imageType);
                continue;
            }

            var updated = imageEntity.Populate(count++, releasedAt);
            if (updated)
                _tmdbImageEntities.Save(imageEntity);

            if (!forceDownload && File.Exists(image.LocalPath))
                continue;

            await scheduler.StartJob<DownloadTmdbImageJob>(c =>
            {
                c.ImageID = image.TMDB_ImageID;
                c.ImageType = image.ImageType;
                c.ForceDownload = forceDownload;
            });
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
            RemoveImageFromEntity(image);
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
            RemoveImageFromEntity(image, foreignType, foreignId);
    }

    private void RemoveImageFromEntity(TMDB_Image image, ForeignEntityType? foreignType = null, int? foreignId = null, ImageEntityType? imageType = null)
    {
        if (foreignType.HasValue && foreignId.HasValue)
        {
            var entities = _tmdbImageEntities.GetByForeignID(foreignId.Value, foreignType.Value)
                .Where(x => !imageType.HasValue || x.ImageType == imageType.Value)
                .ToList();
            foreach (var linkedEntity in entities)
            {
                switch (linkedEntity.GetTmdbEntity())
                {
                    case TMDB_Movie movie:
                        ShokoEventHandler.Instance.OnMovieUpdated(movie, UpdateReason.ImageRemoved);
                        break;

                    case TMDB_Show show:
                        ShokoEventHandler.Instance.OnSeriesUpdated(show, UpdateReason.ImageRemoved);
                        break;

                    case TMDB_Season season:
                    {
                        if (season.TmdbShow is not { } tmdbShow)
                            continue;

                        // Until we have proper season entities in the abstraction, just emit it for the series/show.
                        ShokoEventHandler.Instance.OnSeriesUpdated(tmdbShow, UpdateReason.ImageRemoved);
                        break;
                    }

                    case TMDB_Episode episode:
                    {
                        if (episode.TmdbShow is not { } tmdbShow)
                            continue;
                        ShokoEventHandler.Instance.OnEpisodeUpdated(tmdbShow, episode, UpdateReason.ImageRemoved);
                        break;
                    }
                }
            }
            _tmdbImageEntities.Delete(entities);
        }

        // Only delete the image metadata and/or file if all references were removed.
        if (_tmdbImageEntities.GetByRemoteFileName(image.RemoteFileName).Count > 0)
            return;

        if (!string.IsNullOrEmpty(image.LocalPath) && File.Exists(image.LocalPath))
            File.Delete(image.LocalPath);

        _tmdbImages.Delete(image);

        foreach (var iT in Enum.GetValues<ImageEntityType>())
        {
            var preferredAnimeImages = _preferredImages.GetByImageSourceAndTypeAndID(DataSourceType.TMDB, iT, image.TMDB_ImageID);
            var preferredEpisodeImages = _preferredEpisodeImages.GetByImageSourceAndTypeAndID(DataSourceType.TMDB, iT, image.TMDB_ImageID);
            _preferredImages.Delete(preferredAnimeImages);
            _preferredEpisodeImages.Delete(preferredEpisodeImages);
        }
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
