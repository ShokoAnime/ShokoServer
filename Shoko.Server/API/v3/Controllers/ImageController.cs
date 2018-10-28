using System.IO;
using Microsoft.AspNetCore.Mvc;
using NLog;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3
{
    [ApiController]
    [Route("/apiv3/image")]
    public class ImageController : Controller
    {
        private Logger logger = LogManager.GetCurrentClassLogger(); 
        
        /// <summary>
        /// /apiv3/image/tvdb/fanart/12
        /// returns an image
        /// </summary>
        /// <param name="source"></param>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <returns>200 on found, 400 if the type or source are invalid, and 404 if the id is not found</returns>
        [HttpGet("{source}/{type}/{id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public ActionResult GetImage(string source, string type, int id)
        {
            source = source.ToLower();
            type = type.ToLower();
            ImageEntityType? sourceType = GetImageTypeFromSourceAndType(source, type);

            if (sourceType == null) return BadRequest();
            string path = GetImagePath(sourceType.Value, id);
            if (string.IsNullOrEmpty(path)) return NotFound();
            return File(System.IO.File.OpenRead(path), MimeTypes.GetMimeType(path));
        }

        /// <summary>
        /// Gets the enum ImageEntityType from the text url segments
        /// </summary>
        /// <param name="source"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        [NonAction]
        private ImageEntityType? GetImageTypeFromSourceAndType(string source, string type)
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
        /// Get image path on disk from the type and relevant ID
        /// </summary>
        /// <param name="id">image id</param>
        /// <param name="type">image type</param>
        /// <returns>string</returns>
        [NonAction]
        private string GetImagePath(ImageEntityType type, int id)
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
                    if (System.IO.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = string.Empty;
                        logger.Trace("Could not find AniDB_Cover image: {0}", anime.PosterPath);
                    }
                    break;
                // 4
                case ImageEntityType.TvDB_Banner:
                    TvDB_ImageWideBanner wideBanner = Repo.Instance.TvDB_ImageWideBanner.GetByID(id);
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
                        logger.Trace("Could not find TvDB_Banner image: {0}", wideBanner.GetFullImagePath());
                    }
                    break;

                // 5
                case ImageEntityType.TvDB_Cover:
                    TvDB_ImagePoster poster = Repo.Instance.TvDB_ImagePoster.GetByID(id);
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
                        logger.Trace("Could not find TvDB_Cover image: {0}", poster.GetFullImagePath());
                    }
                    break;

                // 6
                case ImageEntityType.TvDB_Episode:
                    TvDB_Episode ep = Repo.Instance.TvDB_Episode.GetByID(id);
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
                        logger.Trace("Could not find TvDB_Episode image: {0}", ep.GetFullImagePath());
                    }
                    break;

                // 7
                case ImageEntityType.TvDB_FanArt:
                    TvDB_ImageFanart fanart = Repo.Instance.TvDB_ImageFanart.GetByID(id);
                    if (fanart == null)
                        return null;
                    path = fanart.GetFullImagePath();
                    if (System.IO.File.Exists(path))
                        return path;
                    
                    logger.Trace("Could not find TvDB_FanArt image: {0}", path);
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
                    if (System.IO.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = string.Empty;
                        logger.Trace("Could not find MovieDB_FanArt image: {0}", mFanart.GetFullImagePath());
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
                    if (System.IO.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = string.Empty;
                        logger.Trace("Could not find MovieDB_Poster image: {0}", mPoster.GetFullImagePath());
                    }
                    break;

                case ImageEntityType.Character:
                    AnimeCharacter character = Repo.Instance.AnimeCharacter.GetByID(id);
                    if (character == null)
                        return null;
                    path = ImageUtils.GetBaseAniDBCharacterImagesPath() + Path.DirectorySeparatorChar + character.ImagePath;
                    if (System.IO.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = string.Empty;
                        logger.Trace("Could not find Character image: {0}",
                            ImageUtils.GetBaseAniDBCharacterImagesPath() + Path.DirectorySeparatorChar + character.ImagePath);
                    }
                    break;

                case ImageEntityType.Staff:
                    var staff = Repo.Instance.AnimeStaff.GetByID(id);
                    if (staff == null)
                        return null;
                    path = ImageUtils.GetBaseAniDBCreatorImagesPath() + Path.DirectorySeparatorChar + staff.ImagePath;
                    if (System.IO.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = string.Empty;
                        logger.Trace("Could not find Staff image: {0}",
                            ImageUtils.GetBaseAniDBCreatorImagesPath() + Path.DirectorySeparatorChar + staff.ImagePath);
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