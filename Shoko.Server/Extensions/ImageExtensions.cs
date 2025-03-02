using System.IO;
using System.Linq;
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

public static class ImageExtensions
{
    private static string ResolveAnidbImageUrl(string relativePath)
        => string.Format(string.Format(Constants.URLS.AniDB_Images, Constants.URLS.AniDB_Images_Domain), relativePath.Split(Path.DirectorySeparatorChar).LastOrDefault());

    public static IImageMetadata? GetImageMetadata(this AniDB_Character character, bool preferred = false)
        => !string.IsNullOrEmpty(character.ImagePath)
            ? new Image_Base
            {
                ID = character.CharacterID,
                Source = DataSourceEnum.AniDB,
                ImageType = ImageEntityType.Character,
                IsPreferred = preferred,
                LocalPath = character.GetFullImagePath(),
                RemoteURL = ResolveAnidbImageUrl(character.ImagePath),
            }
            : null;

    public static IImageMetadata GetImageMetadata(this AniDB_Anime anime, bool? preferred = null) =>
        !string.IsNullOrEmpty(anime.Picname)
            ? new Image_Base
            {
                Source = DataSourceEnum.AniDB,
                ImageType = ImageEntityType.Poster,
                ID = anime.AnimeID,
                IsEnabled = anime.ImageEnabled == 1,
                IsPreferred = preferred ??
                              RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(anime.AnimeID, ImageEntityType.Poster) is
                                  { ImageSource: Shoko.Models.Enums.DataSourceType.AniDB },
                RemoteURL = ResolveAnidbImageUrl(anime.Picname),
                LocalPath = GetFullImagePath(anime)
            }
            : new Image_Base
            {
                Source = DataSourceEnum.AniDB, ImageType = ImageEntityType.Poster, ID = anime.AnimeID
            };

    public static string GetFullImagePath(this AniDB_Anime anime)
    {
        if (string.IsNullOrEmpty(anime.Picname))
            return string.Empty;

        return Path.Combine(ImageUtils.GetAniDBImagePath(anime.AnimeID), anime.Picname);
    }

    public static string GetFullImagePath(this AniDB_Character character)
    {
        if (string.IsNullOrEmpty(character.ImagePath))
            return string.Empty;

        return Path.Combine(ImageUtils.GetAniDBCharacterImagePath(character.CharacterID), character.ImagePath);
    }

    public static IImageMetadata? GetImageMetadata(this AniDB_Creator creator, bool preferred = false)
        => !string.IsNullOrEmpty(creator.ImagePath)
            ? new Image_Base
            {
                Source = DataSourceEnum.AniDB,
                ImageType = ImageEntityType.Person,
                ID = creator.CreatorID,
                IsPreferred = preferred,
                RemoteURL = ResolveAnidbImageUrl(creator.ImagePath),
                LocalPath = creator.GetFullImagePath()
            }
            : null;

    public static string GetFullImagePath(this AniDB_Creator creator)
    {
        if (string.IsNullOrEmpty(creator.ImagePath))
            return string.Empty;

        return Path.Combine(ImageUtils.GetAniDBCreatorImagePath(creator.CreatorID), creator.ImagePath);
    }
    
    public static bool IsImageValid(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            var bytes = new byte[12];
            if (fs.Length < 12) return false;
            fs.Read(bytes, 0, 12);
            return GetImageFormat(bytes) != null;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsImageValid(byte[] bytes)
    {
        try
        {
            if (bytes.Length < 12) return false;
            return GetImageFormat(bytes) != null;
        }
        catch
        {
            return false;
        }
    }

    public static string? GetImageFormat(byte[] bytes)
    {
        // https://en.wikipedia.org/wiki/BMP_file_format#File_structure
        var bmp = "BM"u8.ToArray();
        // https://en.wikipedia.org/wiki/GIF#File_format
        var gif = "GIF"u8.ToArray();
        // https://en.wikipedia.org/wiki/JPEG#Syntax_and_structure
        var jpeg = new byte[] { 255, 216 };
        // https://en.wikipedia.org/wiki/Portable_Network_Graphics#File_header
        var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        // https://en.wikipedia.org/wiki/TIFF#Byte_order
        var tiff1 = "II*\0"u8.ToArray();
        var tiff2 = "MM*\0"u8.ToArray();
        // https://developers.google.com/speed/webp/docs/riff_container#webp_file_header
        var webp1 = "RIFF"u8.ToArray();
        var webp2 = "WEBP"u8.ToArray();

        if (png.SequenceEqual(bytes.Take(png.Length)))
            return "png";

        if (jpeg.SequenceEqual(bytes.Take(jpeg.Length)))
            return "jpeg";

        if (webp1.SequenceEqual(bytes.Take(webp1.Length)) &&
            webp2.SequenceEqual(bytes.Skip(8).Take(webp2.Length)))
            return "webp";

        if (gif.SequenceEqual(bytes.Take(gif.Length)))
            return "gif";

        if (bmp.SequenceEqual(bytes.Take(bmp.Length)))
            return "bmp";

        if (tiff1.SequenceEqual(bytes.Take(tiff1.Length)) ||
            tiff2.SequenceEqual(bytes.Take(tiff2.Length)))
            return "tiff";

        return null;
    }
}
