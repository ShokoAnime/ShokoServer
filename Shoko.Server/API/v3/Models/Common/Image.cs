using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
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
        /// Is it marked as disabled. You must explicitly ask for these, for obvious reasons.
        /// </summary>
        public bool Disabled { get; set; }

        public Image(int id, ImageEntityType type, bool preferred = false, bool disabled = false) : this(id.ToString(), type, preferred, disabled)
        {
            if (type == ImageEntityType.Static)
                throw new ArgumentException("Static Resources do not use an integer ID");
            
            RelativeFilepath = GetImagePath(type, id)?.Replace(ImageUtils.GetBaseImagesPath(), "")
                .Replace("\\", "/");
            if (RelativeFilepath != null && !RelativeFilepath.StartsWith("/"))
                RelativeFilepath = "/" + RelativeFilepath;
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
                    return "Shoko";
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
                    return "MovieDB";
                case ImageEntityType.Character:
                case ImageEntityType.Staff:
                case ImageEntityType.Static:
                    return "Shoko";
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
        
        /// <summary>
        /// Gets the enum ImageEntityType from the text url segments
        /// </summary>
        /// <param name="source"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static ImageEntityType GetImageTypeFromSourceAndType(string source, string type)
        {
            source = source.ToUpper(CultureInfo.InvariantCulture);
            type = type.ToUpper(CultureInfo.InvariantCulture);
            string msg = @"The type is not valid for the selected provider.";
            switch (source)
            {
                case "ANIDB":
                    switch (type)
                    {
                        case "POSTER": return ImageEntityType.AniDB_Cover;
                        default: throw new ArgumentOutOfRangeException(nameof(type), type, msg);
                    }
                case "TVDB":
                    switch (type)
                    {
                        case "POSTER": return ImageEntityType.TvDB_Cover;
                        case "FANART": return ImageEntityType.TvDB_FanArt;
                        case "BANNER": return ImageEntityType.TvDB_Banner;
                        case "THUMB": return ImageEntityType.TvDB_Episode;
                        default: throw new ArgumentOutOfRangeException(nameof(type), type, msg);
                    }
                case "MOVIEDB":
                    switch (type)
                    {
                        case "POSTER": return ImageEntityType.MovieDB_Poster;
                        case "FANART": return ImageEntityType.MovieDB_FanArt;
                        default: throw new ArgumentOutOfRangeException(nameof(type), type, msg);
                    }
                    case "SHOKO":
                        switch (type)
                        {
                            case "STATIC": return ImageEntityType.Static;
                            case "CHARACTER": return ImageEntityType.Character;
                            case "STAFF": return ImageEntityType.Staff;
                            default: throw new ArgumentOutOfRangeException(nameof(type), type, msg);
                        }
                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, @"The provider was not valid.");
            }
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
                    TvDB_Episode ep = RepoFactory.TvDB_Episode.GetByID(id);
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
    }
}