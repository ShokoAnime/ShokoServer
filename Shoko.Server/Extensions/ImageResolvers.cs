using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Extensions;

public static class ImageResolvers
{
    private static string ResolveAnidbImageUrl(string relativePath)
        => string.Format(string.Format(Constants.URLS.AniDB_Images, Constants.URLS.AniDB_Images_Domain), relativePath.Split(Path.DirectorySeparatorChar).LastOrDefault());

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
            : new Image_Base(DataSourceEnum.AniDB, ImageEntityType.Poster, anime.AnimeID);

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

    public static IImageMetadata? GetImageMetadata(this AniDB_Creator seiyuu, bool preferred = false)
        => !string.IsNullOrEmpty(seiyuu.ImagePath)
            ? new Image_Base(DataSourceEnum.AniDB, ImageEntityType.Person, seiyuu.CreatorID, seiyuu.GetFullImagePath(), ResolveAnidbImageUrl(seiyuu.ImagePath))
            {
                IsEnabled = true,
                IsPreferred = preferred,
            }
            : null;

    public static string GetFullImagePath(this AniDB_Creator seiyuu)
    {
        if (string.IsNullOrEmpty(seiyuu.ImagePath))
            return string.Empty;

        return Path.Combine(ImageUtils.GetAniDBCreatorImagePath(seiyuu.CreatorID), seiyuu.ImagePath);
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
