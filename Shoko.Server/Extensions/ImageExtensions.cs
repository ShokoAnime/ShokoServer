using System.IO;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Enums;
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

    public static IImage? GetImageMetadata(this AniDB_Character character, bool preferred = false)
        => !string.IsNullOrEmpty(character.ImagePath)
            ? new Image_Base(DataSource.AniDB, ImageEntityType.Character, character.CharacterID, character.GetFullImagePath(), ResolveAnidbImageUrl(character.ImagePath))
            {
                IsEnabled = true,
                IsPreferred = preferred,
            }
            : null;

    public static IImage GetImageMetadata(this AniDB_Anime anime, bool? preferred = null) =>
        !string.IsNullOrEmpty(anime.Picname)
            ? new Image_Base(DataSource.AniDB, ImageEntityType.Poster, anime.AnimeID, GetFullImagePath(anime), ResolveAnidbImageUrl(anime.Picname))
            {
                IsEnabled = anime.ImageEnabled == 1,
                IsPreferred = preferred ??
                    (RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(anime.AnimeID, ImageEntityType.Poster) is { } preferredImage &&
                    preferredImage.ImageSource is DataSource.AniDB),
            }
            : new Image_Base(DataSource.AniDB, ImageEntityType.Poster, anime.AnimeID);

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

    public static IImage? GetImageMetadata(this AniDB_Creator creator, bool preferred = false)
        => !string.IsNullOrEmpty(creator.ImagePath)
            ? new Image_Base(DataSource.AniDB, ImageEntityType.Creator, creator.CreatorID, creator.GetFullImagePath(), ResolveAnidbImageUrl(creator.ImagePath))
            {
                IsEnabled = true,
                IsPreferred = preferred,
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
            fs.ReadExactly(bytes);
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
        var bmp = new byte[] { 66, 77 };
        // https://en.wikipedia.org/wiki/GIF#File_format
        var gif = new byte[] { 71, 73, 70 };
        // https://en.wikipedia.org/wiki/JPEG#Syntax_and_structure
        var jpeg = new byte[] { 255, 216 };
        // https://en.wikipedia.org/wiki/Portable_Network_Graphics#File_header
        var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        // https://en.wikipedia.org/wiki/TIFF#Byte_order
        var tiff1 = new byte[] { 73, 73, 42, 0 };
        var tiff2 = new byte[] { 77, 77, 42, 0 };
        // https://developers.google.com/speed/webp/docs/riff_container#webp_file_header
        var webp1 = new byte[] { 82, 73, 70, 70 };
        var webp2 = new byte[] { 87, 69, 66, 80 };

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
