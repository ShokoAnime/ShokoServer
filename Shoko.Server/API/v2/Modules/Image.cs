using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Threading.Tasks;
using Nancy;
using NLog;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Properties;
using Shoko.Server.Repositories;


namespace Shoko.Server.API.v2.Modules
{
    public class Image : NancyModule
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public Image() : base("/api")
        {
            Get("/image/{type}/{id}",  async (x,ct) => await Task.Factory.StartNew(() => GetImage((int) x.type, (int) x.id), ct));
            Get("/image/thumb/{type}/{id}/{ratio}",  async (x,ct) => await Task.Factory.StartNew(() => GetThumb((int) x.type, (int) x.id, x.ratio), ct));
            Get("/image/thumb/{type}/{id}",  async (x,ct) => await Task.Factory.StartNew(() => GetThumb((int) x.type, (int) x.id, "0"), ct));
            Get("/image/support/{name}",  async (x,ct) => await Task.Factory.StartNew(() => GetSupportImage(x.name), ct));
            Get("/image/support/{name}/{ratio}",  async (x,ct) => await Task.Factory.StartNew(() => GetSupportImage(x.name, x.ratio), ct));
            Get("/image/validateall",  async (x,ct) => await Task.Factory.StartNew(() =>
            {
                Importer.ValidateAllImages();
                return APIStatus.OK();
            }, ct));
        }

        /// <summary>
        /// Return image with given id, type
        /// </summary>
        /// <param name="id">image id</param>
        /// <param name="type">image type</param>
        /// <returns>image body inside stream</returns>
        private object GetImage(int type, int id)
        {
            Response response;
            string contentType;
            string path = GetImagePath(type, id, false);

            if (string.IsNullOrEmpty(path))
            {
                Stream image = MissingImage();
                contentType = "image/png";
                response = Response.FromStream(image, contentType);
            }
            else
            {
                FileStream fs = File.OpenRead(path);
                contentType = MimeTypes.GetMimeType(path);
                response = Response.FromStream(fs, contentType);
            }

            return response;
        }

        /// <summary>
        /// Return thumb with given id, type
        /// </summary>
        /// <param name="id">image id</param>
        /// <param name="type">image type</param>
        /// <param name="ratio">new image ratio</param>
        /// <returns>resize image body inside stream</returns>
        private object GetThumb(int type, int id, string ratio)
        {
            Response response;
            string contentType;
            ratio = ratio.Replace(',', '.');
            if (!float.TryParse(ratio, NumberStyles.AllowDecimalPoint, CultureInfo.CreateSpecificCulture("en-EN"),
                out float newratio))
                newratio = 0.6667f;

            string path = GetImagePath(type, id, false);

            if (string.IsNullOrEmpty(path))
            {
                Stream image = MissingImage();
                contentType = "image/png";
                response = Response.FromStream(image, contentType);
            }
            else
            {
                FileStream fs = File.OpenRead(path);
                contentType = MimeTypes.GetMimeType(path);
                System.Drawing.Image im = System.Drawing.Image.FromStream(fs);
                response = Response.FromStream(ResizeImageToRatio(im, newratio), contentType);
            }

            return response;
        }

        /// <summary>
        /// Return SupportImage (build-in server)
        /// </summary>
        /// <param name="name">image file name</param>
        /// <returns></returns>
        private object GetSupportImage(string name)
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

            return Response.FromStream(ms, "image/png");
        }

        private object GetSupportImage(string name, string ratio)
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

            return Response.FromStream(ResizeImageToRatio(im, newratio), "image/png");
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
                    if (File.Exists(path))
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
                    if (File.Exists(path))
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
                    if (File.Exists(path))
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
                    if (File.Exists(path))
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
                    if (File.Exists(path))
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
                    if (File.Exists(path))
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
                        if (File.Exists(path))
                            return path;
                        path = string.Empty;
                        logger.Trace("Could not find TvDB_FanArt image: {0}", fanart.GetFullThumbnailPath());
                    }
                    else
                    {
                        path = fanart.GetFullImagePath();
                        if (File.Exists(path))
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
                    if (File.Exists(path))
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
                    if (File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = string.Empty;
                        logger.Trace("Could not find MovieDB_Poster image: {0}", mPoster.GetFullImagePath());
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