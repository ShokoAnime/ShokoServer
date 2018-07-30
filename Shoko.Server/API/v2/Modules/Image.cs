using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Models;
using Shoko.Server.Properties;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v2.Modules
{
    [ApiController]
    [Route("/api/image")]
    public class Image : Controller
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        [HttpGet("validateall")]
        public ActionResult ValidateAll()
        {
            Importer.ValidateAllImages();
            return APIStatus.OK();
        }

        /// <summary>
        /// Return image with given id, type
        /// </summary>
        /// <param name="id">image id</param>
        /// <param name="type">image type</param>
        /// <returns>image body inside stream</returns>
        [HttpGet("{type}/{id}")]
        public FileResult GetImage(int type, int id)
        {
            string path = GetImagePath(type, id, false);

            if (string.IsNullOrEmpty(path))
            {
                Response.StatusCode = 404;
                return File(MissingImage(), "image/png");
            }
            else
            {
                return File(System.IO.File.OpenRead(path), MimeTypes.GetMimeType(path));
            }
        }

        /// <summary>
        /// Return thumb with given id, type
        /// </summary>
        /// <param name="id">image id</param>
        /// <param name="type">image type</param>
        /// <param name="ratio">new image ratio</param>
        /// <returns>resize image body inside stream</returns>
        [HttpGet("thumb/{type}/{id}/{ratio?}")]
        public FileResult GetThumb(int type, int id, string ratio = "0")
        {
            string contentType;
            ratio = ratio.Replace(',', '.');
            if (!float.TryParse(ratio, NumberStyles.AllowDecimalPoint, CultureInfo.CreateSpecificCulture("en-EN"), out float newratio))
                newratio = 0.6667f;

            string path = GetImagePath(type, id, false);

            if (string.IsNullOrEmpty(path))
            {
                Response.StatusCode = 404;
                return File(MissingImage(), "image/png");
            }
            else
            {
                FileStream fs = System.IO.File.OpenRead(path);
                contentType = MimeTypes.GetMimeType(path);
                System.Drawing.Image im = System.Drawing.Image.FromStream(fs);
                return File(ResizeImageToRatio(im, newratio), contentType);
            }
        }

        /// <summary>
        /// Return SupportImage (build-in server)
        /// </summary>
        /// <param name="name">image file name</param>
        /// <returns></returns>
        [HttpGet("support/{name}")]
        public ActionResult GetSupportImage(string name)
        {
            if (string.IsNullOrEmpty(name))
                return APIStatus.NotFound();
            name = Path.GetFileNameWithoutExtension(name);
            ResourceManager man = Resources.ResourceManager;
            byte[] dta = (byte[]) man.GetObject(name);
            if ((dta == null) || (dta.Length == 0))
                return APIStatus.NotFound();
            MemoryStream ms = new MemoryStream(dta);
            ms.Seek(0, SeekOrigin.Begin);

            return File(ms, "image/png");
        }

        [HttpGet("support/{name}/{ratio}")]
        public ActionResult GetSupportImage(string name, string ratio)
        {
            if (string.IsNullOrEmpty(name))
                return APIStatus.NotFound();

            ratio = ratio.Replace(',', '.');
            float.TryParse(ratio, NumberStyles.AllowDecimalPoint,
                CultureInfo.CreateSpecificCulture("en-EN"), out float newratio);

            name = Path.GetFileNameWithoutExtension(name);
            ResourceManager man = Resources.ResourceManager;
            byte[] dta = (byte[]) man.GetObject(name);
            if ((dta == null) || (dta.Length == 0))
                return APIStatus.NotFound();
            MemoryStream ms = new MemoryStream(dta);
            ms.Seek(0, SeekOrigin.Begin);
            System.Drawing.Image im = System.Drawing.Image.FromStream(ms);

            return File(ResizeImageToRatio(im, newratio), "image/png");
        }

        /// <summary>
        /// Internal function that return valid image file path on server that exist
        /// </summary>
        /// <param name="id">image id</param>
        /// <param name="type">image type</param>
        /// <param name="thumb">thumb mode</param>
        /// <returns>string</returns>
        internal string GetImagePath(int type, int id, bool thumb)
        {
            ImageEntityType imageType = (ImageEntityType) type;
            string path;

            switch (imageType)
            {
                // 1
                case ImageEntityType.AniDB_Cover:
                    SVR_AniDB_Anime anime = Repo.AniDB_Anime.GetByID(id);
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

                // 2
                case ImageEntityType.AniDB_Character:
                    AniDB_Character chr = Repo.AniDB_Character.GetByID(id);
                    if (chr == null)
                        return null;
                    path = chr.GetPosterPath();
                    if (System.IO.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = string.Empty;
                        logger.Trace("Could not find AniDB_Character image: {0}", chr.GetPosterPath());
                    }
                    break;

                // 3
                case ImageEntityType.AniDB_Creator:
                    AniDB_Seiyuu creator = Repo.AniDB_Seiyuu.GetByID(id);
                    if (creator == null)
                        return null;
                    path = creator.GetPosterPath();
                    if (System.IO.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = string.Empty;
                        logger.Trace("Could not find AniDB_Creator image: {0}", creator.GetPosterPath());
                    }
                    break;

                // 4
                case ImageEntityType.TvDB_Banner:
                    TvDB_ImageWideBanner wideBanner = Repo.TvDB_ImageWideBanner.GetByID(id);
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
                    TvDB_ImagePoster poster = Repo.TvDB_ImagePoster.GetByID(id);
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
                    TvDB_Episode ep = Repo.TvDB_Episode.GetByID(id);
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
                    TvDB_ImageFanart fanart = Repo.TvDB_ImageFanart.GetByID(id);
                    if (fanart == null)
                        return null;
                    if (thumb)
                    {
                        //ratio
                        path = fanart.GetFullThumbnailPath();
                        if (System.IO.File.Exists(path))
                            return path;
                        path = string.Empty;
                        logger.Trace("Could not find TvDB_FanArt image: {0}", fanart.GetFullThumbnailPath());
                    }
                    else
                    {
                        path = fanart.GetFullImagePath();
                        if (System.IO.File.Exists(path))
                            return path;
                        path = string.Empty;
                        logger.Trace("Could not find TvDB_FanArt image: {0}", fanart.GetFullImagePath());
                    }
                    break;

                // 8
                case ImageEntityType.MovieDB_FanArt:
                    MovieDB_Fanart mFanart = Repo.MovieDB_Fanart.GetByID(id);
                    if (mFanart == null)
                        return null;
                    mFanart = Repo.MovieDB_Fanart.GetByOnlineID(mFanart.URL);
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
                    MovieDB_Poster mPoster = Repo.MovieDB_Poster.GetByID(id);
                    if (mPoster == null)
                        return null;
                    mPoster = Repo.MovieDB_Poster.GetByOnlineID(mPoster.URL);
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
                    AnimeCharacter character = Repo.AnimeCharacter.GetByID(id);
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
                    var staff = Repo.AnimeStaff.GetByID(id);
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

        /// <summary>
        /// Return random image with given type and not from restricted content
        /// </summary>
        /// <param name="type">image type</param>
        /// <returns>image body inside stream</returns>
        [HttpGet("random/{type}")]
        public FileResult GetRandomImage(int type)
        {
            string path = GetRandomImagePath(type);

            if (string.IsNullOrEmpty(path))
            {
                Response.StatusCode = 404;
                return File(MissingImage(), "image/png");
            }
            else
            {
                return File(System.IO.File.OpenRead(path), MimeTypes.GetMimeType(path));
            }
        }

        private string GetRandomImagePath(int type)
        {
            ImageEntityType imageType = (ImageEntityType) type;
            string path;

            switch (imageType)
            {
                // 1
                case ImageEntityType.AniDB_Cover:
                    SVR_AniDB_Anime anime = Repo.AniDB_Anime.GetAll()
                        .Where(a => a?.PosterPath != null && !a.GetAllTags().Contains("18 restricted"))
                        .GetRandomElement();
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

                // 2
                case ImageEntityType.AniDB_Character:
                    var chr = Repo.AniDB_Anime.GetAll()
                        .Where(a => a != null && !a.GetAllTags().Contains("18 restricted"))
                        .SelectMany(a => a.GetAnimeCharacters()).Select(a => a.GetCharacter()).Where(a => a != null)
                        .GetRandomElement();
                    if (chr == null)
                        return null;
                    path = chr.GetPosterPath();
                    if (System.IO.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = string.Empty;
                        logger.Trace("Could not find AniDB_Character image: {0}", chr.GetPosterPath());
                    }
                    break;

                // 3 -- this will likely be slow
                case ImageEntityType.AniDB_Creator:
                    var creator = Repo.AniDB_Anime.GetAll()
                        .Where(a => a != null && !a.GetAllTags().Contains("18 restricted"))
                        .SelectMany(a => a.GetAnimeCharacters())
                        .SelectMany(a => Repo.AniDB_Character_Seiyuu.GetByCharID(a.CharID))
                        .Select(a => Repo.AniDB_Seiyuu.GetByID(a.SeiyuuID)).Where(a => a != null)
                        .GetRandomElement();
                    if (creator == null)
                        return null;
                    path = creator.GetPosterPath();
                    if (System.IO.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = string.Empty;
                        logger.Trace("Could not find AniDB_Creator image: {0}", creator.GetPosterPath());
                    }
                    break;

                // 4
                case ImageEntityType.TvDB_Banner:
                    // TvDB doesn't allow H content, so we get to skip the check!
                    TvDB_ImageWideBanner wideBanner = Repo.TvDB_ImageWideBanner.GetAll().GetRandomElement();
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
                    // TvDB doesn't allow H content, so we get to skip the check!
                    TvDB_ImagePoster poster = Repo.TvDB_ImagePoster.GetAll().GetRandomElement();
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
                    // TvDB doesn't allow H content, so we get to skip the check!
                    TvDB_Episode ep = Repo.TvDB_Episode.GetAll().GetRandomElement();
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
                    // TvDB doesn't allow H content, so we get to skip the check!
                    TvDB_ImageFanart fanart = Repo.TvDB_ImageFanart.GetAll().GetRandomElement();
                    if (fanart == null)
                        return null;
                    path = fanart.GetFullImagePath();
                    if (System.IO.File.Exists(path))
                        return path;
                    path = string.Empty;
                    logger.Trace("Could not find TvDB_FanArt image: {0}", fanart.GetFullImagePath());
                    break;

                // 8
                case ImageEntityType.MovieDB_FanArt:
                    MovieDB_Fanart mFanart = Repo.MovieDB_Fanart.GetAll().GetRandomElement();
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
                    MovieDB_Poster mPoster = Repo.MovieDB_Poster.GetAll().GetRandomElement();
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
                    var character = Repo.AniDB_Anime.GetAll()
                        .Where(a => a != null && !a.GetAllTags().Contains("18 restricted"))
                        .SelectMany(a => Repo.CrossRef_Anime_Staff.GetByAnimeID(a.AnimeID))
                        .Where(a => a.RoleType == (int) StaffRoleType.Seiyuu && a.RoleID.HasValue)
                        .Select(a => Repo.AnimeCharacter.GetByID(a.RoleID.Value)).GetRandomElement();
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
                    var staff = Repo.AniDB_Anime.GetAll()
                        .Where(a => a != null && !a.GetAllTags().Contains("18 restricted"))
                        .SelectMany(a => Repo.CrossRef_Anime_Staff.GetByAnimeID(a.AnimeID))
                        .Select(a => Repo.AnimeStaff.GetByID(a.StaffID)).GetRandomElement();
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

        /// <summary>
        /// Internal function that return image for missing image
        /// </summary>
        /// <returns>Stream</returns>
        internal Stream MissingImage()
        {
            byte[] dta = Resources.blank;
            MemoryStream ms = new MemoryStream(dta);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        internal static System.Drawing.Image ResizeImage(System.Drawing.Image im, int width, int height)
        {
            Bitmap dest = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(dest))
            {
                g.InterpolationMode = width >= im.Width
                    ? InterpolationMode.HighQualityBilinear
                    : InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(im, 0, 0, width, height);
            }
            return dest;
        }

        internal Stream ResizeImageToRatio(System.Drawing.Image im, float newratio)
        {
            float calcwidth = im.Width;
            float calcheight = im.Height;

            if (Math.Abs(newratio) < 0.1F)
            {
                MemoryStream stream = new MemoryStream();
                im.Save(stream, ImageFormat.Png);
                stream.Seek(0, SeekOrigin.Begin);
                return stream;
            }

            float nheight;
            do
            {
                nheight = calcwidth / newratio;
                if (nheight > im.Height + 0.5F)
                    calcwidth = calcwidth * (im.Height / nheight);
                else
                    calcheight = nheight;
            } while (nheight > im.Height + 0.5F);

            int newwidth = (int) Math.Round(calcwidth);
            int newheight = (int) Math.Round(calcheight);
            int x = 0;
            int y = 0;
            if (newwidth < im.Width)
                x = (im.Width - newwidth) / 2;
            if (newheight < im.Height)
                y = (im.Height - newheight) / 2;

            System.Drawing.Image im2 = ResizeImage(im, newwidth, newheight);
            Graphics g = Graphics.FromImage(im2);
            g.DrawImage(im, new Rectangle(0, 0, im2.Width, im2.Height), new Rectangle(x, y, im2.Width, im2.Height),
                GraphicsUnit.Pixel);
            MemoryStream ms = new MemoryStream();
            im2.Save(ms, ImageFormat.Png);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }
    }
}