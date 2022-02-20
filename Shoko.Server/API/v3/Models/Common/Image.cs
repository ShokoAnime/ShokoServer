using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using ImageMagick;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Models.Common
{
    /// <summary>
    /// Image container
    /// </summary>
    public class Image
    {
        /// <summary>
        /// AniDB, TvDB, MovieDB, etc
        /// </summary>
        [Required]
        public string Source { get; set; }
        
        /// <summary>
        /// text representation of type of image. fanart, poster, etc. Mainly so clients know what they are getting
        /// </summary>
        [Required]
        public string Type { get; set; }
        
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
        /// Is it marked as default. Multiple defaults are possible
        /// </summary>
        public bool Preferred { get; set; }
        
        /// <summary>
        /// Width of the image.
        /// </summary>
        public int? Width { get; set; }

        /// <summary>
        /// Height of the image.
        /// </summary>
        public int? Height { get; set; }
        
        /// <summary>
        /// Is it marked as disabled. You must explicitly ask for these, for obvious reasons.
        /// </summary>
        public bool Disabled { get; set; }

        public Image(int id, ImageEntityType type, bool preferred = false, bool disabled = false) : this(id.ToString(), type, preferred, disabled)
        {
            if (type == ImageEntityType.Static)
                throw new ArgumentException("Static Resources do not use an integer ID");
            
            var imagePath = GetImagePath(type, id);
            if (string.IsNullOrEmpty(imagePath)) {
                RelativeFilepath = null;
                Width = null;
                Height = null;
            }
            else
            {
                var info = new MagickImageInfo(imagePath);
                RelativeFilepath = imagePath.Replace(ImageUtils.GetBaseImagesPath(), "").Replace("\\", "/");
                if (!RelativeFilepath.StartsWith("/"))
                    RelativeFilepath = "/" + RelativeFilepath;
                Width = info.Width;
                Height = info.Height;
            }
        }

        public Image(string id, ImageEntityType type, bool preferred = false, bool disabled = false)
        {
            ID = id;
            Type = GetSimpleTypeFromImageType(type);
            Source = GetSourceFromType(type);

            Preferred = preferred;
            Disabled = disabled;
        }

        public static string GetSimpleTypeFromImageType(ImageEntityType type)
        {
            switch (type)
            {
                case ImageEntityType.TvDB_Cover:
                case ImageEntityType.MovieDB_Poster:
                case ImageEntityType.AniDB_Cover:
                    return "Poster";
                case ImageEntityType.TvDB_Banner:
                    return "Banner";
                case ImageEntityType.TvDB_Episode:
                    return "Thumb";
                case ImageEntityType.TvDB_FanArt:
                case ImageEntityType.MovieDB_FanArt:
                    return "Fanart";
                case ImageEntityType.AniDB_Character:
                case ImageEntityType.Character:
                    return "Character";
                case ImageEntityType.AniDB_Creator:
                case ImageEntityType.Staff:
                    return "Staff";
                case ImageEntityType.Static:
                    return "Static";
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public static string GetSourceFromType(ImageEntityType type)
        {
            switch (type)
            {
                case ImageEntityType.AniDB_Cover:
                case ImageEntityType.AniDB_Character:
                case ImageEntityType.AniDB_Creator:
                    return "AniDB";
                case ImageEntityType.TvDB_Banner:
                case ImageEntityType.TvDB_Cover:
                case ImageEntityType.TvDB_Episode:
                case ImageEntityType.TvDB_FanArt:
                    return "TvDB";
                case ImageEntityType.MovieDB_FanArt:
                case ImageEntityType.MovieDB_Poster:
                    return "TMDB";
                case ImageEntityType.Character:
                case ImageEntityType.Staff:
                case ImageEntityType.Static:
                    return "Shoko";
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
        
        /// <summary>
        /// Gets the <see cref="ImageEntityType"/> for the given <paramref name="source"/> and <paramref name="imageType"/>.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="imageType"></param>
        /// <returns></returns>
        public static ImageEntityType? GetImageTypeFromSourceAndType(string source, string imageType)
        {
            return (source.ToLower(CultureInfo.InvariantCulture)) switch {
                "anidb" => (imageType.ToLower(CultureInfo.InvariantCulture)) switch {
                    "poster" => ImageEntityType.AniDB_Cover,
                    "character" => ImageEntityType.AniDB_Character,
                    "staff" => ImageEntityType.AniDB_Creator,
                    _ => null,
                },
                "tvdb" => (imageType.ToLower(CultureInfo.InvariantCulture)) switch {
                    "poster" => ImageEntityType.TvDB_Cover,
                    "banner" => ImageEntityType.TvDB_Banner,
                    "thumb" => ImageEntityType.TvDB_Episode,
                    "fanart" => ImageEntityType.TvDB_FanArt,
                    _ => null,
                },
                "tmdb" => (imageType.ToLower(CultureInfo.InvariantCulture)) switch {
                    "poster" => ImageEntityType.MovieDB_Poster,
                    "fanart" => ImageEntityType.MovieDB_FanArt,
                    _ => null,
                },
                "shoko" => (imageType.ToLower(CultureInfo.InvariantCulture)) switch {
                    "static" => ImageEntityType.Static,
                    "character" => ImageEntityType.Character,
                    "staff" => ImageEntityType.Staff,
                    _ => null,
                },
                _ => null,
            };
        }

        /// <summary>
        /// Gets the <see cref="ImageSizeType"/> from the given <paramref name="imageEntityType"/>.
        /// </summary>
        /// <param name="imageEntityType">Image entity type.</param>
        /// <returns>The <see cref="ImageSizeType"/></returns>
        public static ImageSizeType? GetImageSizeTypeFromImageEntityType(ImageEntityType imageEntityType)
        {
            return imageEntityType switch {
                // Posters
                ImageEntityType.AniDB_Cover => ImageSizeType.Poster,
                ImageEntityType.TvDB_Cover => ImageSizeType.Poster,
                ImageEntityType.MovieDB_Poster => ImageSizeType.Poster,

                // Banners
                ImageEntityType.TvDB_Banner => ImageSizeType.WideBanner,

                // Fanart
                ImageEntityType.TvDB_FanArt => ImageSizeType.Fanart,
                ImageEntityType.MovieDB_FanArt => ImageSizeType.Fanart,
                _ => null,
            };
        }
        
        public static string GetImagePath(ImageEntityType type, int id)
        {
            string path;

            switch (type)
            {
                // 1
                case ImageEntityType.AniDB_Cover:
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(id);
                    if (anime == null)
                        return null;
                    path = anime.PosterPath;
                    if (System.IO.File.Exists(path))
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
                    TvDB_ImageWideBanner wideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(id);
                    if (wideBanner == null)
                        return null;
                    path = wideBanner.GetFullImagePath();
                    if (System.IO.File.Exists(path))
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
                    TvDB_ImagePoster poster = RepoFactory.TvDB_ImagePoster.GetByID(id);
                    if (poster == null)
                        return null;
                    path = poster.GetFullImagePath();
                    if (System.IO.File.Exists(path))
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
                    TvDB_Episode ep = RepoFactory.TvDB_Episode.GetByTvDBID(id);
                    if (ep == null)
                        return null;
                    path = ep.GetFullImagePath();
                    if (System.IO.File.Exists(path))
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
                    TvDB_ImageFanart fanart = RepoFactory.TvDB_ImageFanart.GetByID(id);
                    if (fanart == null)
                        return null;
                    path = fanart.GetFullImagePath();
                    if (System.IO.File.Exists(path))
                        return path;

                    path = string.Empty;
                    break;

                // 8
                case ImageEntityType.MovieDB_FanArt:
                    MovieDB_Fanart mFanart = RepoFactory.MovieDB_Fanart.GetByID(id);
                    if (mFanart == null)
                        return null;
                    mFanart = RepoFactory.MovieDB_Fanart.GetByOnlineID(mFanart.URL);
                    if (mFanart == null)
                        return null;
                    path = mFanart.GetFullImagePath();
                    if (System.IO.File.Exists(path))
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
                    MovieDB_Poster mPoster = RepoFactory.MovieDB_Poster.GetByID(id);
                    if (mPoster == null)
                        return null;
                    mPoster = RepoFactory.MovieDB_Poster.GetByOnlineID(mPoster.URL);
                    if (mPoster == null)
                        return null;
                    path = mPoster.GetFullImagePath();
                    if (System.IO.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = string.Empty;
                    }
                    break;

                case ImageEntityType.Character:
                    AnimeCharacter character = RepoFactory.AnimeCharacter.GetByID(id);
                    if (character == null)
                        return null;
                    path = ImageUtils.GetBaseAniDBCharacterImagesPath() + System.IO.Path.DirectorySeparatorChar + character.ImagePath;
                    if (System.IO.File.Exists(path))
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
                        return null;
                    path = ImageUtils.GetBaseAniDBCreatorImagesPath() + System.IO.Path.DirectorySeparatorChar + staff.ImagePath;
                    if (System.IO.File.Exists(path))
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
        
        /// <summary>
        /// Input models.
        /// </summary>
        public class Input
        {
            public class DefaultImageBody
            {
                [Required]
                public string ID { get; set; }
                
                [Required]
                public string Source { get; set; }
            }
        }
    }
}