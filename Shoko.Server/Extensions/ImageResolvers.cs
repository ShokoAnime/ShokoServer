using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Extensions;

public static class ImageResolvers
{
    private static string ResolveAnidbImageUrl(string relativePath)
        => string.Format(string.Format(Constants.URLS.AniDB_Images, Constants.URLS.AniDB_Images_Domain), relativePath);

    public static IReadOnlyList<IImageMetadata> GetImages(this TvDB_Series tvdbSeries, ImageEntityType? entityType = null, IReadOnlyDictionary<ImageEntityType, IImageMetadata>? preferredImages = null)
    {
        var images = new List<IImageMetadata>();

        if (!entityType.HasValue || entityType is ImageEntityType.Backdrop)
            images.AddRange(RepoFactory.TvDB_ImageFanart.GetBySeriesID(tvdbSeries.SeriesID).Select(f => f.GetImageMetadata()).WhereNotNull());
        if (!entityType.HasValue || entityType is ImageEntityType.Poster)
            images.AddRange(RepoFactory.TvDB_ImagePoster.GetBySeriesID(tvdbSeries.SeriesID).Select(f => f.GetImageMetadata()).WhereNotNull());
        if (!entityType.HasValue || entityType is ImageEntityType.Banner)
            images.AddRange(RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(tvdbSeries.SeriesID).Select(f => f.GetImageMetadata()).WhereNotNull());

        if (preferredImages is not null)
            return images
                .GroupBy(i => i.ImageType)
                .SelectMany(gB => preferredImages.TryGetValue(gB.Key, out var pI) ? gB.Select(i => i.Equals(pI) ? pI : i) : gB)
                .ToList();

        return images;
    }

    public static IReadOnlyList<IImageMetadata> GetImages(this TvDB_Episode tvdbSeries, ImageEntityType? entityType = null, IReadOnlyDictionary<ImageEntityType, IImageMetadata>? preferredImages = null)
    {
        var images = new List<IImageMetadata>();

        if ((!entityType.HasValue || entityType is ImageEntityType.Thumbnail) && GetImageMetadata(tvdbSeries) is { } thumbnail)
            images.Add(thumbnail);

        if (preferredImages is not null)
            return images
                .GroupBy(i => i.ImageType)
                .SelectMany(gB => preferredImages.TryGetValue(gB.Key, out var pI) ? gB.Select(i => i.Equals(pI) ? pI : i) : gB)
                .ToList();

        return images;
    }

    public static IImageMetadata? GetImageMetadata(this TvDB_Episode episode, bool preferred = false)
        => !string.IsNullOrEmpty(episode.Filename)
            ? new Image_Base(DataSourceEnum.TvDB, ImageEntityType.Thumbnail, episode.TvDB_EpisodeID, episode.GetFullImagePath(), string.Format(Constants.URLS.TvDB_Images, episode.Filename))
            {
                IsEnabled = true,
                IsPreferred = preferred,
            }
            : null;

    public static string GetFullImagePath(this TvDB_Episode episode)
    {
        if (string.IsNullOrEmpty(episode.Filename))
            return string.Empty;

        var fname = episode.Filename.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
    }

    public static IImageMetadata? GetImageMetadata(this TvDB_ImageFanart fanart, bool preferred = false)
        => !string.IsNullOrEmpty(fanart.BannerPath)
            ? new Image_Base(DataSourceEnum.TvDB, ImageEntityType.Backdrop, fanart.TvDB_ImageFanartID, GetFullImagePath(fanart), string.Format(Constants.URLS.TvDB_Images, fanart.BannerPath))
            {
                IsEnabled = fanart.Enabled == 1,
                IsPreferred = preferred,
            }
            : null;

    public static string GetFullImagePath(this TvDB_ImageFanart fanart)
    {
        if (string.IsNullOrEmpty(fanart.BannerPath))
            return string.Empty;

        var fname = fanart.BannerPath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
    }

    public static IImageMetadata? GetImageMetadata(this TvDB_ImagePoster poster, bool preferred = false)
        => !string.IsNullOrEmpty(poster.BannerPath)
            ? new Image_Base(DataSourceEnum.TvDB, ImageEntityType.Poster, poster.TvDB_ImagePosterID, GetFullImagePath(poster), string.Format(Constants.URLS.TvDB_Images, poster.BannerPath))
            {
                IsEnabled = poster.Enabled == 1,
                IsPreferred = preferred,
            }
            : null;

    public static string GetFullImagePath(this TvDB_ImagePoster poster)
    {
        if (string.IsNullOrEmpty(poster.BannerPath))
            return string.Empty;

        var fname = poster.BannerPath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
    }

    public static IImageMetadata? GetImageMetadata(this TvDB_ImageWideBanner banner, bool preferred = false)
        => !string.IsNullOrEmpty(banner.BannerPath)
            ? new Image_Base(DataSourceEnum.TvDB, ImageEntityType.Banner, banner.TvDB_ImageWideBannerID, GetFullImagePath(banner), string.Format(Constants.URLS.TvDB_Images, banner.BannerPath))
            {
                IsEnabled = banner.Enabled == 1,
                IsPreferred = preferred,
            }
            : null;

    public static string GetFullImagePath(this TvDB_ImageWideBanner banner)
    {
        if (string.IsNullOrEmpty(banner.BannerPath))
            return string.Empty;

        var fname = banner.BannerPath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
    }

    public static IImageMetadata? GetImageMetadata(this AniDB_Character character, bool preferred = false)
        => !string.IsNullOrEmpty(character.PicName)
            ? new Image_Base(DataSourceEnum.AniDB, ImageEntityType.Character, character.CharID, character.GetFullImagePath(), ResolveAnidbImageUrl(character.PicName))
            {
                IsEnabled = true,
                IsPreferred = preferred,
            }
            : null;

    public static IImageMetadata GetImageMetadata(this AniDB_Anime anime, bool? preferred = null) =>
        !string.IsNullOrEmpty(anime.Picname)
            ? new Image_Base(DataSourceEnum.AniDB, ImageEntityType.Poster, anime.AnimeID, GetFullImagePath(anime), ResolveAnidbImageUrl(anime.Picname))
            {
                IsEnabled = anime.ImageEnabled == 1,
                IsPreferred = preferred ??
                    (RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(anime.AnimeID, ImageEntityType.Poster) is { } preferredImage &&
                    preferredImage!.ImageSource == Shoko.Models.Enums.DataSourceType.AniDB),
            }
            : throw new NullReferenceException($"AniDB Anime {anime.AnimeID} does not have a poster path set!");

    public static string GetFullImagePath(this AniDB_Anime anime)
    {
        if (string.IsNullOrEmpty(anime.Picname))
            return string.Empty;

        return Path.Combine(ImageUtils.GetAniDBImagePath(anime.AnimeID), anime.Picname);
    }

    public static string GetFullImagePath(this AniDB_Character character)
    {
        if (string.IsNullOrEmpty(character.PicName))
            return string.Empty;

        return Path.Combine(ImageUtils.GetAniDBCharacterImagePath(character.CharID), character.PicName);
    }

    public static IImageMetadata? GetImageMetadata(this AniDB_Seiyuu seiyuu, bool preferred = false)
        => !string.IsNullOrEmpty(seiyuu.PicName)
            ? new Image_Base(DataSourceEnum.AniDB, ImageEntityType.Person, seiyuu.SeiyuuID, seiyuu.GetFullImagePath(), ResolveAnidbImageUrl(seiyuu.PicName))
            {
                IsEnabled = true,
                IsPreferred = preferred,
            }
            : null;

    public static string GetFullImagePath(this AniDB_Seiyuu seiyuu)
    {
        if (string.IsNullOrEmpty(seiyuu.PicName))
            return string.Empty;

        return Path.Combine(ImageUtils.GetAniDBCreatorImagePath(seiyuu.SeiyuuID), seiyuu.PicName);
    }

    public static IImageMetadata? GetImageMetadata(this AnimeCharacter character, bool preferred = false)
        => !string.IsNullOrEmpty(character.ImagePath)
            ? new Image_Base(DataSourceEnum.Shoko, ImageEntityType.Character, character.CharacterID, character.GetFullImagePath(), ResolveAnidbImageUrl(character.ImagePath))
            {
                IsEnabled = true,
                IsPreferred = preferred,
            }
            : null;

    public static string GetFullImagePath(this AnimeCharacter character)
    {
        if (string.IsNullOrEmpty(character.ImagePath))
            return string.Empty;

        return Path.Combine(ImageUtils.GetBaseAniDBCharacterImagesPath(), character.ImagePath);
    }

    public static IImageMetadata? GetImageMetadata(this AnimeStaff staff, bool preferred = false)
        => !string.IsNullOrEmpty(staff.ImagePath)
            ? new Image_Base(DataSourceEnum.Shoko, ImageEntityType.Person, staff.StaffID, staff.GetFullImagePath(), ResolveAnidbImageUrl(staff.ImagePath))
            {
                IsEnabled = true,
                IsPreferred = preferred,
            }
            : null;

    public static string GetFullImagePath(this AnimeStaff staff)
    {
        if (string.IsNullOrEmpty(staff.ImagePath))
            return string.Empty;

        return Path.Combine(ImageUtils.GetBaseAniDBCreatorImagesPath(), staff.ImagePath);
    }
}
