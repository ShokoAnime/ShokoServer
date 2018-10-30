using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Microsoft.AspNetCore.Http;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3
{
    /// <summary>
    /// Image container
    /// </summary>
    public class Image
    {
        /// <summary>
        /// text representation of type of image. fanart, poster, etc. Mainly so clients know what they are getting
        /// </summary>
        [Required]
        public string type { get; set; }
        
        /// <summary>
        /// normally a client won't need this, but if the client plans to set it as default, disabled, deleted, etc, then it'll be needed
        /// </summary>
        [Required]
        public int id { get; set; }
        
        /// <summary>
        /// AniDB, TvDB, MovieDB, etc
        /// </summary>
        [Required]
        public string source { get; set; }
        
        /// <summary>
        /// The URL to get the image from the server
        /// </summary>
        [Required]
        public string url { get; set; }
        
        /// <summary>
        /// The relative path from the base image directory. A client with access to the server's filesystem can map
        /// these for quick access and no need for caching
        /// </summary>
        public string relative_filepath { get; set; }

        public Image(HttpContext ctx, int id, ImageEntityType type)
        {
            this.id = id;
            this.type = type.ToString();
            source = GetSourceFromType(type);
            url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, (int) type, id);
            relative_filepath = GetImagePath(type, id).Replace(ImageUtils.GetBaseImagesPath(), "").Replace("\\", "/");
            if (!relative_filepath.StartsWith("/")) relative_filepath = "/" + relative_filepath;
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
                    return "AniDB";
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
        public static ImageEntityType? GetImageTypeFromSourceAndType(string source, string type)
        {
            switch (source)
            {
                case "anidb":
                    switch (type)
                    {
                        case "poster": return ImageEntityType.AniDB_Cover;
                        case "character": return ImageEntityType.Character;
                        case "staff": return ImageEntityType.Staff;
                    }
                    break;
                case "tvdb":
                    switch (type)
                    {
                        case "poster": return ImageEntityType.TvDB_Cover;
                        case "fanart": return ImageEntityType.TvDB_FanArt;
                        case "banner": return ImageEntityType.TvDB_Banner;
                        case "thumb": return ImageEntityType.TvDB_Episode;
                    }
                    break;
                case "moviedb":
                    switch (type)
                    {
                        case "poster": return ImageEntityType.MovieDB_Poster;
                        case "fanart": return ImageEntityType.MovieDB_FanArt;
                    }
                    break;
            }

            return null;
        }
        
        /// <summary>
        /// Gets the source and type from the ImageEntityTypeEnum
        /// </summary>
        /// <param name="source"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string GetSourceAndTypeFromImageType(ImageEntityType type)
        {
            switch (type)
            {
                case ImageEntityType.AniDB_Cover:
                    return "anidb/poster/";
                case ImageEntityType.Character:
                    return "anidb/character/";
                case ImageEntityType.Staff:
                    return "anidb/staff/";
                case ImageEntityType.TvDB_Cover:
                    return "tvdb/poster";
                case ImageEntityType.TvDB_FanArt:
                    return "tvdb/fanart/";
                case ImageEntityType.TvDB_Banner:
                    return "tvdb/banner/";
                case ImageEntityType.TvDB_Episode:
                    return "tvdb/thumb/";
                case ImageEntityType.MovieDB_Poster:
                    return "moviedb/poster/";
                case ImageEntityType.MovieDB_FanArt:
                    return "moviedb/fanart/";
            }

            return null;
        }
        
        public static string GetImagePath(ImageEntityType type, int id)
        {
            string path;

            switch (type)
            {
                // 1
                case ImageEntityType.AniDB_Cover:
                    SVR_AniDB_Anime anime = Repo.Instance.AniDB_Anime.GetByID(id);
                    if (anime == null)
                        return null;
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
                    TvDB_ImageWideBanner wideBanner = Repo.Instance.TvDB_ImageWideBanner.GetByID(id);
                    if (wideBanner == null)
                        return null;
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
                    TvDB_ImagePoster poster = Repo.Instance.TvDB_ImagePoster.GetByID(id);
                    if (poster == null)
                        return null;
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
                    TvDB_Episode ep = Repo.Instance.TvDB_Episode.GetByID(id);
                    if (ep == null)
                        return null;
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
                    TvDB_ImageFanart fanart = Repo.Instance.TvDB_ImageFanart.GetByID(id);
                    if (fanart == null)
                        return null;
                    path = fanart.GetFullImagePath();
                    if (File.Exists(path))
                        return path;

                    path = string.Empty;
                    break;

                // 8
                case ImageEntityType.MovieDB_FanArt:
                    MovieDB_Fanart mFanart = Repo.Instance.MovieDB_Fanart.GetByID(id);
                    if (mFanart == null)
                        return null;
                    mFanart = Repo.Instance.MovieDB_Fanart.GetByOnlineID(mFanart.URL);
                    if (mFanart == null)
                        return null;
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
                    MovieDB_Poster mPoster = Repo.Instance.MovieDB_Poster.GetByID(id);
                    if (mPoster == null)
                        return null;
                    mPoster = Repo.Instance.MovieDB_Poster.GetByOnlineID(mPoster.URL);
                    if (mPoster == null)
                        return null;
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
                    AnimeCharacter character = Repo.Instance.AnimeCharacter.GetByID(id);
                    if (character == null)
                        return null;
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
                    var staff = Repo.Instance.AnimeStaff.GetByID(id);
                    if (staff == null)
                        return null;
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
    }
}