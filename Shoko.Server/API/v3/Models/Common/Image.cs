using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
// using ImageMagick;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// Image container
/// </summary>
public class Image
{
    /// <summary>
    /// AniDB, TvDB, MovieDB, etc
    /// </summary>
    [Required]
    public ImageSource Source { get; set; }

    /// <summary>
    /// text representation of type of image. fanart, poster, etc. Mainly so clients know what they are getting
    /// </summary>
    [Required]
    public ImageType Type { get; set; }

    /// <summary>
    /// The image's ID, usually an int, but in the case of Static resources, it is the resource name.
    /// </summary>
    [Required]
    public string ID { get; set; }

    /// <summary>
    /// The relative path from the base image directory. A client with access to the server's filesystem can map
    /// these for quick access and no need for caching
    /// </summary>
    public string RelativeFilepath { get; set; }

    /// <summary>
    /// Is it marked as default. Only one default is possible for a given <see cref="Image.Type"/>.
    /// </summary>
    public bool Preferred { get; set; }

    /// <summary>
    /// Width of the image.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Height of the image.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Is it marked as disabled. You must explicitly ask for these, for obvious reasons.
    /// </summary>
    public bool Disabled { get; set; }

    public Image(int id, ImageEntityType type, bool preferred = false, bool disabled = false) : this(id.ToString(),
        type, preferred, disabled)
    {
        if (type == ImageEntityType.Static)
        {
            throw new ArgumentException("Static Resources do not use an integer ID");
        }

        RelativeFilepath = GetImagePath(type, id)?.Replace(ImageUtils.GetBaseImagesPath(), "").Replace("\\", "/");
        /*
        var imagePath = GetImagePath(type, id);
        if (string.IsNullOrEmpty(imagePath)) {
            RelativeFilepath = null;
            Width = null;
            Height = null;
        }
        // This causes serious IO lag on some systems. Removing until we have better Image data structures
        else
        {
            var info = new MagickImageInfo(imagePath);
            RelativeFilepath = imagePath.Replace(ImageUtils.GetBaseImagesPath(), "").Replace("\\", "/");
            if (!RelativeFilepath.StartsWith("/"))
                RelativeFilepath = "/" + RelativeFilepath;
            Width = info.Width;
            Height = info.Height;
        }*/
    }

    public Image(string id, ImageEntityType type, bool preferred = false, bool disabled = false)
    {
        ID = id;
        Type = GetSimpleTypeFromImageType(type);
        Source = GetSourceFromType(type);

        Preferred = preferred;
        Disabled = disabled;
    }

    public static ImageType GetSimpleTypeFromImageType(ImageEntityType type)
    {
        switch (type)
        {
            case ImageEntityType.TvDB_Cover:
            case ImageEntityType.MovieDB_Poster:
            case ImageEntityType.AniDB_Cover:
                return ImageType.Poster;
            case ImageEntityType.TvDB_Banner:
                return ImageType.Banner;
            case ImageEntityType.TvDB_Episode:
                return ImageType.Thumb;
            case ImageEntityType.TvDB_FanArt:
            case ImageEntityType.MovieDB_FanArt:
                return ImageType.Fanart;
            case ImageEntityType.AniDB_Character:
            case ImageEntityType.Character:
                return ImageType.Character;
            case ImageEntityType.AniDB_Creator:
            case ImageEntityType.Staff:
                return ImageType.Staff;
            case ImageEntityType.Static:
                return ImageType.Static;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    public static ImageSource GetSourceFromType(ImageEntityType type)
    {
        switch (type)
        {
            case ImageEntityType.AniDB_Cover:
            case ImageEntityType.AniDB_Character:
            case ImageEntityType.AniDB_Creator:
                return ImageSource.AniDB;
            case ImageEntityType.TvDB_Banner:
            case ImageEntityType.TvDB_Cover:
            case ImageEntityType.TvDB_Episode:
            case ImageEntityType.TvDB_FanArt:
                return ImageSource.TvDB;
            case ImageEntityType.MovieDB_FanArt:
            case ImageEntityType.MovieDB_Poster:
                return ImageSource.TMDB;
            case ImageEntityType.Character:
            case ImageEntityType.Staff:
            case ImageEntityType.Static:
                return ImageSource.Shoko;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    /// <summary>
    /// Gets the <see cref="ImageEntityType"/> for the given <paramref name="imageSource"/> and <paramref name="imageType"/>.
    /// </summary>
    /// <param name="imageSource"></param>
    /// <param name="imageType"></param>
    /// <returns></returns>
    public static ImageEntityType? GetImageTypeFromSourceAndType(ImageSource imageSource, ImageType imageType)
    {
        return imageSource switch
        {
            ImageSource.AniDB => imageType switch
            {
                ImageType.Poster => ImageEntityType.AniDB_Cover,
                ImageType.Character => ImageEntityType.AniDB_Character,
                ImageType.Staff => ImageEntityType.AniDB_Creator,
                _ => null
            },
            ImageSource.TvDB => imageType switch
            {
                ImageType.Poster => ImageEntityType.TvDB_Cover,
                ImageType.Banner => ImageEntityType.TvDB_Banner,
                ImageType.Thumb => ImageEntityType.TvDB_Episode,
                ImageType.Fanart => ImageEntityType.TvDB_FanArt,
                _ => null
            },
            ImageSource.TMDB => imageType switch
            {
                ImageType.Poster => ImageEntityType.MovieDB_Poster,
                ImageType.Fanart => ImageEntityType.MovieDB_FanArt,
                _ => null
            },
            ImageSource.Shoko => imageType switch
            {
                ImageType.Static => ImageEntityType.Static,
                ImageType.Character => ImageEntityType.Character,
                ImageType.Staff => ImageEntityType.Staff,
                _ => null
            },
            _ => null
        };
    }

    /// <summary>
    /// Gets the <see cref="ImageSizeType"/> from the given <paramref name="imageEntityType"/>.
    /// </summary>
    /// <param name="imageEntityType">Image entity type.</param>
    /// <returns>The <see cref="ImageSizeType"/></returns>
    public static ImageSizeType? GetImageSizeTypeFromImageEntityType(ImageEntityType imageEntityType)
    {
        return imageEntityType switch
        {
            // Posters
            ImageEntityType.AniDB_Cover => ImageSizeType.Poster,
            ImageEntityType.TvDB_Cover => ImageSizeType.Poster,
            ImageEntityType.MovieDB_Poster => ImageSizeType.Poster,

            // Banners
            ImageEntityType.TvDB_Banner => ImageSizeType.WideBanner,

            // Fanart
            ImageEntityType.TvDB_FanArt => ImageSizeType.Fanart,
            ImageEntityType.MovieDB_FanArt => ImageSizeType.Fanart,
            _ => null
        };
    }

    /// <summary>
    /// Gets the <see cref="ImageSizeType"/> from the given <paramref name="imageType"/>.
    /// </summary>
    /// <param name="imageType"></param>
    /// <returns>The <see cref="ImageSizeType"/></returns>
    public static ImageSizeType GetImageSizeTypeFromType(ImageType imageType)
    {
        return imageType switch
        {
            // Posters
            ImageType.Poster => ImageSizeType.Poster,

            // Banners
            ImageType.Banner => ImageSizeType.WideBanner,

            // Fanart
            ImageType.Fanart => ImageSizeType.Fanart,
            _ => ImageSizeType.Poster
        };
    }

    public static string GetImagePath(ImageEntityType type, int id)
    {
        string path;

        switch (type)
        {
            // 1
            case ImageEntityType.AniDB_Cover:
                var anime = RepoFactory.AniDB_Anime.GetByAnimeID(id);
                if (anime == null)
                {
                    return null;
                }

                path = anime.PosterPath;
                if (File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                }

                break;
            // 4
            case ImageEntityType.TvDB_Banner:
                var wideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(id);
                if (wideBanner == null)
                {
                    return null;
                }

                path = wideBanner.GetFullImagePath();
                if (File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                }

                break;

            // 5
            case ImageEntityType.TvDB_Cover:
                var poster = RepoFactory.TvDB_ImagePoster.GetByID(id);
                if (poster == null)
                {
                    return null;
                }

                path = poster.GetFullImagePath();
                if (File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                }

                break;

            // 6
            case ImageEntityType.TvDB_Episode:
                var ep = RepoFactory.TvDB_Episode.GetByTvDBID(id);
                if (ep == null)
                {
                    return null;
                }

                path = ep.GetFullImagePath();
                if (File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                }

                break;

            // 7
            case ImageEntityType.TvDB_FanArt:
                var fanart = RepoFactory.TvDB_ImageFanart.GetByID(id);
                if (fanart == null)
                {
                    return null;
                }

                path = fanart.GetFullImagePath();
                if (File.Exists(path))
                {
                    return path;
                }

                path = string.Empty;
                break;

            // 8
            case ImageEntityType.MovieDB_FanArt:
                var mFanart = RepoFactory.MovieDB_Fanart.GetByID(id);
                if (mFanart == null)
                {
                    return null;
                }

                mFanart = RepoFactory.MovieDB_Fanart.GetByOnlineID(mFanart.URL);
                if (mFanart == null)
                {
                    return null;
                }

                path = mFanart.GetFullImagePath();
                if (File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                }

                break;

            // 9
            case ImageEntityType.MovieDB_Poster:
                var mPoster = RepoFactory.MovieDB_Poster.GetByID(id);
                if (mPoster == null)
                {
                    return null;
                }

                mPoster = RepoFactory.MovieDB_Poster.GetByOnlineID(mPoster.URL);
                if (mPoster == null)
                {
                    return null;
                }

                path = mPoster.GetFullImagePath();
                if (File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                }

                break;

            case ImageEntityType.Character:
                var character = RepoFactory.AnimeCharacter.GetByID(id);
                if (character == null)
                {
                    return null;
                }

                path = ImageUtils.GetBaseAniDBCharacterImagesPath() + Path.DirectorySeparatorChar + character.ImagePath;
                if (File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                }

                break;

            case ImageEntityType.Staff:
                var staff = RepoFactory.AnimeStaff.GetByID(id);
                if (staff == null)
                {
                    return null;
                }

                path = ImageUtils.GetBaseAniDBCreatorImagesPath() + Path.DirectorySeparatorChar + staff.ImagePath;
                if (File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                }

                break;

            default:
                path = string.Empty;
                break;
        }

        return path;
    }

    private static List<ImageSource> BannerImageSources = new() { ImageSource.TvDB };

    private static List<ImageSource> PosterImageSources = new()
    {
        ImageSource.AniDB,
        // ImageSource.TMDB,
        ImageSource.TvDB
    };

    // There is only one thumbnail provider atm.
    private static List<ImageSource> ThumbImageSources = new() { ImageSource.TvDB };

    // TMDB is too unreliable atm, so we will only use TvDB for now.
    private static List<ImageSource> FanartImageSources = new()
    {
        ImageSource.TvDB
        // ImageSource.TMDB,
    };

    private static List<ImageSource> CharacterImageSources = new()
    {
        // ImageSource.AniDB,
        ImageSource.Shoko
    };

    private static List<ImageSource> StaffImageSources = new()
    {
        // ImageSource.AniDB,
        ImageSource.Shoko
    };

    private static List<ImageSource> StaticImageSources = new() { ImageSource.Shoko };

    internal static ImageSource GetRandomImageSource(ImageType imageType)
    {
        var sourceList = imageType switch
        {
            ImageType.Poster => PosterImageSources,
            ImageType.Banner => BannerImageSources,
            ImageType.Thumb => ThumbImageSources,
            ImageType.Fanart => FanartImageSources,
            ImageType.Character => CharacterImageSources,
            ImageType.Staff => StaffImageSources,
            _ => StaticImageSources
        };

        return sourceList.GetRandomElement();
    }

    internal static int? GetRandomImageID(ImageEntityType imageType)
    {
        return imageType switch
        {
            ImageEntityType.AniDB_Cover => RepoFactory.AniDB_Anime.GetAll()
                .Where(a => a?.PosterPath != null && !a.GetAllTags().Contains("18 restricted"))
                .GetRandomElement()?.AnimeID,
            ImageEntityType.AniDB_Character => RepoFactory.AniDB_Anime.GetAll()
                .Where(a => a != null && !a.GetAllTags().Contains("18 restricted"))
                .SelectMany(a => a.GetAnimeCharacters()).Select(a => a.GetCharacter()).Where(a => a != null)
                .GetRandomElement()?.AniDB_CharacterID,
            // This will likely be slow
            ImageEntityType.AniDB_Creator => RepoFactory.AniDB_Anime.GetAll()
                .Where(a => a != null && !a.GetAllTags().Contains("18 restricted"))
                .SelectMany(a => a.GetAnimeCharacters())
                .SelectMany(a => RepoFactory.AniDB_Character_Seiyuu.GetByCharID(a.CharID))
                .Select(a => RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(a.SeiyuuID)).Where(a => a != null)
                .GetRandomElement()?.AniDB_SeiyuuID,
            // TvDB doesn't allow H content, so we get to skip the check!
            ImageEntityType.TvDB_Banner => RepoFactory.TvDB_ImageWideBanner.GetAll()
                .GetRandomElement()?.TvDB_ImageWideBannerID,
            // TvDB doesn't allow H content, so we get to skip the check!
            ImageEntityType.TvDB_Cover => RepoFactory.TvDB_ImagePoster.GetAll()
                .GetRandomElement()?.TvDB_ImagePosterID,
            // TvDB doesn't allow H content, so we get to skip the check!
            ImageEntityType.TvDB_Episode => RepoFactory.TvDB_Episode.GetAll()
                .GetRandomElement()?.Id,
            // TvDB doesn't allow H content, so we get to skip the check!
            ImageEntityType.TvDB_FanArt => RepoFactory.TvDB_ImageFanart.GetAll()
                .GetRandomElement()?.TvDB_ImageFanartID,
            ImageEntityType.MovieDB_FanArt => RepoFactory.MovieDB_Fanart.GetAll()
                .GetRandomElement()?.MovieDB_FanartID,
            ImageEntityType.MovieDB_Poster => RepoFactory.MovieDB_Poster.GetAll()
                .GetRandomElement()?.MovieDB_PosterID,
            ImageEntityType.Character => RepoFactory.AniDB_Anime.GetAll()
                .Where(a => a != null && !a.GetAllTags().Contains("18 restricted"))
                .SelectMany(a => RepoFactory.CrossRef_Anime_Staff.GetByAnimeID(a.AnimeID))
                .Where(a => a.RoleType == (int)StaffRoleType.Seiyuu && a.RoleID.HasValue)
                .Select(a => RepoFactory.AnimeCharacter.GetByID(a.RoleID.Value))
                .GetRandomElement()?.CharacterID,
            ImageEntityType.Staff => RepoFactory.AniDB_Anime.GetAll()
                .Where(a => a != null && !a.GetAllTags().Contains("18 restricted"))
                .SelectMany(a => RepoFactory.CrossRef_Anime_Staff.GetByAnimeID(a.AnimeID))
                .Select(a => RepoFactory.AnimeStaff.GetByID(a.StaffID))
                .GetRandomElement()?.StaffID,
            _ => null
        };
    }

    /// <summary>
    /// Image source.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ImageSource
    {
        /// <summary>
        ///
        /// </summary>
        AniDB = 1,

        /// <summary>
        ///
        /// </summary>
        TvDB = 2,

        /// <summary>
        ///
        /// </summary>
        TMDB = 3,

        /// <summary>
        ///
        /// </summary>
        Shoko = 100
    }

    /// <summary>
    /// Image type.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ImageType
    {
        /// <summary>
        ///
        /// </summary>
        Poster = 1,

        /// <summary>
        ///
        /// </summary>
        Banner = 2,

        /// <summary>
        ///
        /// </summary>
        Thumb = 3,

        /// <summary>
        ///
        /// </summary>
        Fanart = 4,

        /// <summary>
        ///
        /// </summary>
        Character = 5,

        /// <summary>
        ///
        /// </summary>
        Staff = 6,

        /// <summary>
        /// Static resources are only valid if the <see cref="Image.Source"/> is set to <see cref="ImageSource.Shoko"/>.
        /// </summary>
        Static = 100
    }

    /// <summary>
    /// Input models.
    /// </summary>
    public class Input
    {
        public class DefaultImageBody
        {
            /// <summary>
            /// The ID. A stringified int since we send the ID as a string
            /// from the API. Also see <seealso cref="Image.ID"/>.
            /// </summary>
            /// <value></value>
            [Required]
            public string ID { get; set; }

            /// <summary>
            /// The image source.
            /// </summary>
            /// <value></value>
            [Required]
            public ImageSource Source { get; set; }
        }
    }
}
