using System.IO;
using Shoko.Server.Utilities;

namespace Shoko.Server.ImageDownload;

public class ImageUtils
{
    public static string GetBaseImagesPath()
    {
        var settings = Utils.SettingsProvider?.GetSettings();
        if (!string.IsNullOrEmpty(settings?.ImagesPath) &&
            Directory.Exists(settings.ImagesPath))
        {
            return settings.ImagesPath;
        }

        var imagepath = Utils.DefaultImagePath;
        if (!Directory.Exists(imagepath))
        {
            Directory.CreateDirectory(imagepath);
        }

        return imagepath;
    }

    public static string GetBaseAniDBImagesPath()
    {
        var filePath = Path.Combine(GetBaseImagesPath(), "AniDB");

        if (!Directory.Exists(filePath))
        {
            Directory.CreateDirectory(filePath);
        }

        return filePath;
    }

    public static string GetBaseAniDBCharacterImagesPath()
    {
        var filePath = Path.Combine(GetBaseImagesPath(), "AniDB_Char");

        if (!Directory.Exists(filePath))
        {
            Directory.CreateDirectory(filePath);
        }

        return filePath;
    }

    public static string GetBaseAniDBCreatorImagesPath()
    {
        var filePath = Path.Combine(GetBaseImagesPath(), "AniDB_Creator");

        if (!Directory.Exists(filePath))
        {
            Directory.CreateDirectory(filePath);
        }

        return filePath;
    }

    public static string GetBaseTvDBImagesPath()
    {
        var filePath = Path.Combine(GetBaseImagesPath(), "TvDB");

        if (!Directory.Exists(filePath))
        {
            Directory.CreateDirectory(filePath);
        }

        return filePath;
    }

    public static string GetBaseMovieDBImagesPath()
    {
        var filePath = Path.Combine(GetBaseImagesPath(), "MovieDB");

        if (!Directory.Exists(filePath))
        {
            Directory.CreateDirectory(filePath);
        }

        return filePath;
    }

    public static string GetBaseTraktImagesPath()
    {
        var filePath = Path.Combine(GetBaseImagesPath(), "Trakt");

        if (!Directory.Exists(filePath))
        {
            Directory.CreateDirectory(filePath);
        }

        return filePath;
    }

    public static string GetImagesTempFolder()
    {
        var filePath = Path.Combine(GetBaseImagesPath(), "_Temp_");

        if (!Directory.Exists(filePath))
        {
            Directory.CreateDirectory(filePath);
        }

        return filePath;
    }

    public static string GetAniDBCharacterImagePath(int charID)
    {
        var subFolder = string.Empty;
        var sid = charID.ToString();
        if (sid.Length == 1)
        {
            subFolder = sid;
        }
        else
        {
            subFolder = sid.Substring(0, 2);
        }

        var filePath = Path.Combine(GetBaseAniDBCharacterImagesPath(), subFolder);

        if (!Directory.Exists(filePath))
        {
            Directory.CreateDirectory(filePath);
        }

        return filePath;
    }

    public static string GetAniDBCreatorImagePath(int creatorID)
    {
        var subFolder = string.Empty;
        var sid = creatorID.ToString();
        if (sid.Length == 1)
        {
            subFolder = sid;
        }
        else
        {
            subFolder = sid.Substring(0, 2);
        }

        var filePath = Path.Combine(GetBaseAniDBCreatorImagesPath(), subFolder);

        if (!Directory.Exists(filePath))
        {
            Directory.CreateDirectory(filePath);
        }

        return filePath;
    }

    public static string GetAniDBImagePath(int animeID)
    {
        var subFolder = string.Empty;
        var sid = animeID.ToString();
        if (sid.Length == 1)
        {
            subFolder = sid;
        }
        else
        {
            subFolder = sid.Substring(0, 2);
        }

        var filePath = Path.Combine(GetBaseAniDBImagesPath(), subFolder);

        if (!Directory.Exists(filePath))
        {
            Directory.CreateDirectory(filePath);
        }

        return filePath;
    }

    public static string GetTvDBImagePath()
    {
        var filePath = GetBaseTvDBImagesPath();

        if (!Directory.Exists(filePath))
        {
            Directory.CreateDirectory(filePath);
        }

        return filePath;
    }

    public static string GetMovieDBImagePath()
    {
        var filePath = GetBaseMovieDBImagesPath();

        if (!Directory.Exists(filePath))
        {
            Directory.CreateDirectory(filePath);
        }

        return filePath;
    }

    public static string GetTraktImagePath()
    {
        var filePath = GetBaseTraktImagesPath();

        if (!Directory.Exists(filePath))
        {
            Directory.CreateDirectory(filePath);
        }

        return filePath;
    }

    public static string GetTraktImagePath_Avatars()
    {
        var filePath = Path.Combine(GetTraktImagePath(), "Avatars");

        if (!Directory.Exists(filePath))
        {
            Directory.CreateDirectory(filePath);
        }

        return filePath;
    }
}
