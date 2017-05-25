using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using Nancy;
using NLog;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Resources = Shoko.Server.Properties.Resources;

namespace Shoko.Server.API.v2.Modules
{
    public class Image : Nancy.NancyModule
    {
        public static int version = 1;

        private static Logger logger = LogManager.GetCurrentClassLogger();

        public Image() : base("/api")
        {
            Get["/image/{type}/{id}", true] = async (x,ct) => await Task.Factory.StartNew(() => GetImage((int) x.type, (int) x.id), ct);
            Get["/image/thumb/{type}/{id}/{ratio}", true] = async (x,ct) => await Task.Factory.StartNew(() => GetThumb((int) x.type, (int) x.id, x.ratio), ct);
            Get["/image/thumb/{type}/{id}", true] = async (x,ct) => await Task.Factory.StartNew(() => GetThumb((int) x.type, (int) x.id, "0"), ct);
            Get["/image/support/{name}", true] = async (x,ct) => await Task.Factory.StartNew(() => GetSupportImage(x.name), ct);
            Get["/image/support/{name}/{ratio}", true] = async (x,ct) => await Task.Factory.StartNew(() => GetSupportImage(x.name, x.ratio), ct);
        }

        /// <summary>
        /// Return image with given id, type
        /// </summary>
        /// <param name="id">image id</param>
        /// <param name="type">image type</param>
        /// <returns>image body inside stream</returns>
        private object GetImage(int type, int id)
        {
            Nancy.Response response = new Nancy.Response();
            string contentType = "";
            string path = ReturnImagePath(type, id, false);

            if (path == "")
            {
                Stream image = MissingImage();
                contentType = "image/png";
                response = Response.FromStream(image, contentType);
            }
            else
            {
                FileStream fs = Pri.LongPath.File.OpenRead(path);
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
            Nancy.Response response = new Nancy.Response();
            string contentType = "";
            ratio = ratio.Replace(',', '.');
            float.TryParse(ratio, System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.CreateSpecificCulture("en-EN"), out float newratio);

            string path = ReturnImagePath(id, type, false);

            if (path == "")
            {
                Stream image = MissingImage();
                contentType = "image/png";
                response = Response.FromStream(image, contentType);
            }
            else
            {
                FileStream fs = Pri.LongPath.File.OpenRead(path);
                contentType = MimeTypes.GetMimeType(path);
                System.Drawing.Image im = System.Drawing.Image.FromStream(fs);
                response = Response.FromStream(ResizeToRatio(im, newratio), contentType);
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
            Nancy.Response response = new Nancy.Response();

            if (string.IsNullOrEmpty(name))
            {
                return APIStatus.notFound404();
            }
            name = Path.GetFileNameWithoutExtension(name);
            System.Resources.ResourceManager man = Resources.ResourceManager;
            byte[] dta = (byte[]) man.GetObject(name);
            if ((dta == null) || (dta.Length == 0))
            {
                return APIStatus.notFound404();
            }
            MemoryStream ms = new MemoryStream(dta);
            ms.Seek(0, SeekOrigin.Begin);

            response = Response.FromStream(ms, "image/png");
            return response;
        }

        private object GetSupportImage(string name, string ratio)
        {
            Nancy.Response response = new Nancy.Response();
            if (string.IsNullOrEmpty(name))
            {
                return APIStatus.notFound404();
            }

            ratio = ratio.Replace(',', '.');
            float.TryParse(ratio, System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.CreateSpecificCulture("en-EN"), out float newratio);

            name = Path.GetFileNameWithoutExtension(name);
            System.Resources.ResourceManager man = Resources.ResourceManager;
            byte[] dta = (byte[]) man.GetObject(name);
            if ((dta == null) || (dta.Length == 0))
            {
                return APIStatus.notFound404();
            }
            MemoryStream ms = new MemoryStream(dta);
            ms.Seek(0, SeekOrigin.Begin);
            System.Drawing.Image im = System.Drawing.Image.FromStream(ms);

            response = Response.FromStream(ResizeToRatio(im, newratio), "image/png");
            return response;
        }

        /// <summary>
        /// Internal function that return valid image file path on server that exist
        /// </summary>
        /// <param name="id">image id</param>
        /// <param name="type">image type</param>
        /// <param name="thumb">thumb mode</param>
        /// <returns>string</returns>
        internal string ReturnImagePath(int type, int id, bool thumb)
        {
            JMMImageType imageType = (JMMImageType) type;
            string path = "";

            switch (imageType)
            {
                // 1
                case JMMImageType.AniDB_Cover:
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(id);
                    if (anime == null)
                    {
                        return null;
                    }
                    path = anime.PosterPath;
                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = "";
                        logger.Trace("Could not find AniDB_Cover image: {0}", anime.PosterPath);
                    }
                    break;

                // 2
                case JMMImageType.AniDB_Character:
                    AniDB_Character chr = RepoFactory.AniDB_Character.GetByCharID(id);
                    if (chr == null)
                    {
                        return null;
                    }
                    path = chr.GetPosterPath();
                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = "";
                        logger.Trace("Could not find AniDB_Character image: {0}", chr.GetPosterPath());
                    }
                    break;

                // 3
                case JMMImageType.AniDB_Creator:
                    AniDB_Seiyuu creator = RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(id);
                    if (creator == null)
                    {
                        return null;
                    }
                    path = creator.GetPosterPath();
                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = "";
                        logger.Trace("Could not find AniDB_Creator image: {0}", creator.GetPosterPath());
                    }
                    break;

                // 4
                case JMMImageType.TvDB_Banner:
                    TvDB_ImageWideBanner wideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(id);
                    if (wideBanner == null)
                    {
                        return null;
                    }
                    path = wideBanner.GetFullImagePath();
                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = "";
                        logger.Trace("Could not find TvDB_Banner image: {0}", wideBanner.GetFullImagePath());
                    }
                    break;

                // 5
                case JMMImageType.TvDB_Cover:
                    TvDB_ImagePoster poster = RepoFactory.TvDB_ImagePoster.GetByID(id);
                    if (poster == null)
                    {
                        return null;
                    }
                    path = poster.GetFullImagePath();
                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = "";
                        logger.Trace("Could not find TvDB_Cover image: {0}", poster.GetFullImagePath());
                    }
                    break;

                // 6
                case JMMImageType.TvDB_Episode:
                    TvDB_Episode ep = RepoFactory.TvDB_Episode.GetByID(id);
                    if (ep == null)
                    {
                        return null;
                    }
                    path = ep.GetFullImagePath();
                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = "";
                        logger.Trace("Could not find TvDB_Episode image: {0}", ep.GetFullImagePath());
                    }
                    break;

                // 7
                case JMMImageType.TvDB_FanArt:
                    TvDB_ImageFanart fanart = RepoFactory.TvDB_ImageFanart.GetByID(id);
                    if (fanart == null)
                    {
                        return null;
                    }
                    if (thumb)
                    {
                        //ratio
                        path = fanart.GetFullThumbnailPath();
                        if (Pri.LongPath.File.Exists(path))
                        {
                            return path;
                        }
                        else
                        {
                            path = "";
                            logger.Trace("Could not find TvDB_FanArt image: {0}", fanart.GetFullThumbnailPath());
                        }
                    }
                    else
                    {
                        path = fanart.GetFullImagePath();
                        if (Pri.LongPath.File.Exists(path))
                        {
                            return path;
                        }
                        else
                        {
                            path = "";
                            logger.Trace("Could not find TvDB_FanArt image: {0}", fanart.GetFullImagePath());
                        }
                    }
                    break;

                // 8
                case JMMImageType.MovieDB_FanArt:
                    MovieDB_Fanart mFanart = RepoFactory.MovieDB_Fanart.GetByID(id);
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
                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = "";
                        logger.Trace("Could not find MovieDB_FanArt image: {0}", mFanart.GetFullImagePath());
                    }
                    break;

                // 9
                case JMMImageType.MovieDB_Poster:
                    MovieDB_Poster mPoster = RepoFactory.MovieDB_Poster.GetByID(id);
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
                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = "";
                        logger.Trace("Could not find MovieDB_Poster image: {0}", mPoster.GetFullImagePath());
                    }
                    break;

                // 10
                case JMMImageType.Trakt_Poster:
                    Trakt_ImagePoster tPoster = RepoFactory.Trakt_ImagePoster.GetByID(id);
                    if (tPoster == null)
                    {
                        return null;
                    }
                    path = tPoster.GetFullImagePath();
                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = "";
                        logger.Trace("Could not find Trakt_Poster image: {0}", tPoster.GetFullImagePath());
                    }
                    break;

                // 11
                case JMMImageType.Trakt_Fanart:
                    Trakt_ImageFanart tFanart = RepoFactory.Trakt_ImageFanart.GetByID(id);
                    if (tFanart == null)
                    {
                        return null;
                    }
                    path = tFanart.GetFullImagePath();
                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = "";
                        logger.Trace("Could not find Trakt_Fanart image: {0}", tFanart.GetFullImagePath());
                    }
                    break;


                // 12 + 16
                case JMMImageType.Trakt_Episode:
                case JMMImageType.Trakt_WatchedEpisode:
                    Trakt_Episode tEpisode = RepoFactory.Trakt_Episode.GetByID(id);
                    if (tEpisode == null)
                    {
                        return null;
                    }
                    path = tEpisode.GetFullImagePath();
                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = "";
                        logger.Trace("Could not find Trakt_Episode image: {0}", tEpisode.GetFullImagePath());
                    }
                    break;

                // 0, 13-15, 17+
                default:
                    path = "";
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

        internal static System.Drawing.Image ReSize(System.Drawing.Image im, int width, int height)
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

        internal System.IO.Stream ResizeToRatio(System.Drawing.Image im, float newratio)
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

            float nheight = 0;
            do
            {
                nheight = calcwidth / newratio;
                if (nheight > im.Height + 0.5F)
                {
                    calcwidth = calcwidth * (im.Height / nheight);
                }
                else
                {
                    calcheight = nheight;
                }
            } while (nheight > im.Height + 0.5F);

            int newwidth = (int) Math.Round(calcwidth);
            int newheight = (int) Math.Round(calcheight);
            int x = 0;
            int y = 0;
            if (newwidth < im.Width)
                x = (im.Width - newwidth) / 2;
            if (newheight < im.Height)
                y = (im.Height - newheight) / 2;

            System.Drawing.Image im2 = ReSize(im, newwidth, newheight);
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