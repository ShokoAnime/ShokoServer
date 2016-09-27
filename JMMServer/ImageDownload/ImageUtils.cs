using System.Diagnostics;
using System.IO;

namespace JMMServer.ImageDownload
{
    public class ImageUtils
    {
        public static string GetBaseImagesPath()
        {
            if (!string.IsNullOrEmpty(ServerSettings.ImagesPath) && Directory.Exists(ServerSettings.ImagesPath)) 
                return ServerSettings.ImagesPath;
            string imagepath = ServerSettings.DefaultImagePath;
            if (!Directory.Exists(imagepath))
                Directory.CreateDirectory(imagepath);
            return imagepath;
        }

        public static string GetBaseAniDBImagesPath()
        {
            string filePath = Path.Combine(GetBaseImagesPath(), "AniDB");

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            return filePath;
        }

        public static string GetBaseAniDBCharacterImagesPath()
        {
            string filePath = Path.Combine(GetBaseImagesPath(), "AniDB_Char");

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            return filePath;
        }

        public static string GetBaseAniDBCreatorImagesPath()
        {
            string filePath = Path.Combine(GetBaseImagesPath(), "AniDB_Creator");

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            return filePath;
        }

        public static string GetBaseTvDBImagesPath()
        {
            string filePath = Path.Combine(GetBaseImagesPath(), "TvDB");

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            return filePath;
        }

        public static string GetBaseMovieDBImagesPath()
        {
            string filePath = Path.Combine(GetBaseImagesPath(), "MovieDB");

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            return filePath;
        }

        public static string GetBaseTraktImagesPath()
        {
            string filePath = Path.Combine(GetBaseImagesPath(), "Trakt");

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            return filePath;
        }

        public static string GetImagesTempFolder()
        {
            string filePath = Path.Combine(GetBaseImagesPath(), "_Temp_");

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            return filePath;
        }

        public static string GetAniDBCharacterImagePath(int charID)
        {
            string subFolder = "";
            string sid = charID.ToString();
            if (sid.Length == 1)
                subFolder = sid;
            else
                subFolder = sid.Substring(0, 2);

            string filePath = Path.Combine(GetBaseAniDBCharacterImagesPath(), subFolder);

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            return filePath;
        }

        public static string GetAniDBCreatorImagePath(int creatorID)
        {
            string subFolder = "";
            string sid = creatorID.ToString();
            if (sid.Length == 1)
                subFolder = sid;
            else
                subFolder = sid.Substring(0, 2);

            string filePath = Path.Combine(GetBaseAniDBCreatorImagesPath(), subFolder);

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            return filePath;
        }

        public static string GetAniDBImagePath(int animeID)
        {
            string subFolder = "";
            string sid = animeID.ToString();
            if (sid.Length == 1)
                subFolder = sid;
            else
                subFolder = sid.Substring(0, 2);

            string filePath = Path.Combine(GetBaseAniDBImagesPath(), subFolder);

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            return filePath;
        }

        public static string GetTvDBImagePath()
        {
            string filePath = GetBaseTvDBImagesPath();

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            return filePath;
        }

        public static string GetMovieDBImagePath()
        {
            string filePath = GetBaseMovieDBImagesPath();

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            return filePath;
        }

        public static string GetTraktImagePath()
        {
            string filePath = GetBaseTraktImagesPath();

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            return filePath;
        }

        public static string GetTraktImagePath_Avatars()
        {
            string filePath = Path.Combine(GetTraktImagePath(), "Avatars");

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            return filePath;
        }
    }
}