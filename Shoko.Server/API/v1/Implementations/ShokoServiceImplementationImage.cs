using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Shoko.Models.Server;
using NLog;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Server.Models;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;
using Resources = Shoko.Server.Properties.Resources;
using Shoko.Server.API.v1;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using HttpContext = Microsoft.AspNetCore.Http.HttpContext;
using Mime = MimeMapping.MimeUtility;

namespace Shoko.Server
{
    [ApiController, Route("/api/Image"), ApiVersionNeutral, ApiExplorerSettings(IgnoreApi = true)]
    public class ShokoServiceImplementationImage : Controller, IShokoServerImage, IHttpContextAccessor
    {
        public HttpContext HttpContext { get; set; }

        private static Logger logger = LogManager.GetCurrentClassLogger();

        [HttpGet("{imageid}/{imageType}/{thumnbnailOnly?}")]
        public object GetImage(int imageid, int imageType, bool? thumnbnailOnly = false)
        {
            string path = GetImagePath(imageid, imageType, thumnbnailOnly);
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                return NotFound();
            string mime = Mime.GetMimeMapping(path);
            Response.ContentType = mime;
            return System.IO.File.OpenRead(path);
        }

        [HttpGet("WithPath/{serverImagePath}")]
        public object GetImageUsingPath(string serverImagePath)
        {
            if (!System.IO.File.Exists(serverImagePath))
            {
                logger.Trace("Could not find AniDB_Cover image: {0}", serverImagePath);
                return NotFound();
            }

            Response.ContentType = Mime.GetMimeMapping(serverImagePath);
            return System.IO.File.OpenRead(serverImagePath);
        }

        [HttpGet("Blank")]
        public object BlankImage()
        {
            byte[] dta = Resources.blank;
            MemoryStream ms = new MemoryStream(dta);
            ms.Seek(0, SeekOrigin.Begin);
            Response.ContentType = "image/jpeg";
            return ms;
        }

        [NonAction]
        internal static Bitmap ReSize(Bitmap im, int width, int height)
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

        [NonAction]
        public Stream ResizeToRatio(Image im, double newratio)
        {
            double calcwidth = im.Width;
            double calcheight = im.Height;

            if (Math.Abs(newratio) < 0.001D)
            {
                MemoryStream stream = new MemoryStream();
                im.Save(stream, ImageFormat.Jpeg);
                stream.Seek(0, SeekOrigin.Begin);
                Response.ContentType = "image/jpeg";
                return stream;
            }

            double nheight = 0;
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

            Image im2 = ReSize(new Bitmap(im), newwidth, newheight);
            Graphics g = Graphics.FromImage(im2);
            g.DrawImage(im, new Rectangle(0, 0, im2.Width, im2.Height),
                new Rectangle(x, y, im2.Width, im2.Height), GraphicsUnit.Pixel);
            MemoryStream ms = new MemoryStream();
            im2.Save(ms, ImageFormat.Jpeg);
            ms.Seek(0, SeekOrigin.Begin);
            Response.ContentType = "image/jpeg";
            return ms;
        }

        [HttpGet("Support/{name}/{ratio}")]
        public object GetSupportImage(string name, float? ratio)
        {
            if (string.IsNullOrEmpty(name))
                return NotFound();
            
            name = Path.GetFileNameWithoutExtension(name);
            System.Resources.ResourceManager man = Resources.ResourceManager;
            byte[] dta = (byte[]) man.GetObject(name);
            if ((dta == null) || (dta.Length == 0))
                return NotFound();
            
            //Little hack
            MemoryStream ms = new MemoryStream(dta);
            ms.Seek(0, SeekOrigin.Begin);
            if (!name.Contains("404") || (ratio == null || Math.Abs(ratio.Value) < 0.001D))
            {
                Response.ContentType = "image/png";
                return ms;
            }
            
            Image im = Image.FromStream(ms);
            float w = im.Width;
            float h = im.Height;
            float nw;
            float nh;

            if (w <= h)
            {
                nw = h * ratio.Value;
                if (nw < w)
                {
                    nw = w;
                    nh = w / ratio.Value;
                }
                else
                    nh = h;
            }
            else
            {
                nh = w / ratio.Value;
                if (nh < h)
                {
                    nh = h;
                    nw = w * ratio.Value;
                }
                else
                    nw = w;
            }
            nw = (float) Math.Round(nw);
            nh = (float) Math.Round(nh);
            Image im2 = new Bitmap((int) nw, (int) nh, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(im2))
            {
                g.InterpolationMode = nw >= im.Width
                    ? InterpolationMode.HighQualityBilinear
                    : InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.Clear(Color.Transparent);
                Rectangle src = new Rectangle(0, 0, im.Width, im.Height);
                Rectangle dst = new Rectangle((int) ((nw - w) / 2), (int) ((nh - h) / 2), im.Width, im.Height);
                g.DrawImage(im, dst, src, GraphicsUnit.Pixel);
            }
            MemoryStream ms2 = new MemoryStream();
            im2.Save(ms2, ImageFormat.Png);
            ms2.Seek(0, SeekOrigin.Begin);
            ms.Dispose();
            Response.ContentType = "image/png";
            return ms2;
        }

        [HttpGet("Thumb/{imageId}/{imageType}/{ratio}")]
        public object GetThumb(int imageId, int imageType, float ratio)
        {
            object m = GetImage(imageId, imageType, false);
            if (m == NotFound()) return m;

            if (!(m is Stream image)) return NotFound();
            using (Image im = Image.FromStream(image))
            {
                return ResizeToRatio(im, ratio);
            }
        }

        [HttpGet("Path/{imageId}/{imageType}/{thumnbnailOnly?}")]
        public string GetImagePath(int imageId, int imageType, bool? thumnbnailOnly)
        {
            ImageEntityType it = (ImageEntityType) imageType;

            switch (it)
            {
                case ImageEntityType.AniDB_Cover:
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(imageId);
                    if (anime == null) return null;
                    if (System.IO.File.Exists(anime.PosterPath))
                    {
                        return anime.PosterPath;
                    }
                    else
                    {
                        logger.Trace("Could not find AniDB_Cover image: {0}", anime.PosterPath);
                        return string.Empty;
                    }

                case ImageEntityType.AniDB_Character:
                    AniDB_Character chr = RepoFactory.AniDB_Character.GetByID(imageId);
                    if (chr == null) return null;
                    if (System.IO.File.Exists(chr.GetPosterPath()))
                    {
                        return chr.GetPosterPath();
                    }
                    else
                    {
                        logger.Trace("Could not find AniDB_Character image: {0}", chr.GetPosterPath());
                        return string.Empty;
                    }

                case ImageEntityType.AniDB_Creator:
                    AniDB_Seiyuu creator = RepoFactory.AniDB_Seiyuu.GetByID(imageId);
                    if (creator == null) return string.Empty;
                    if (System.IO.File.Exists(creator.GetPosterPath()))
                    {
                        return creator.GetPosterPath();
                    }
                    else
                    {
                        logger.Trace("Could not find AniDB_Creator image: {0}", creator.GetPosterPath());
                        return string.Empty;
                    }

                case ImageEntityType.TvDB_Cover:
                    TvDB_ImagePoster poster = RepoFactory.TvDB_ImagePoster.GetByID(imageId);
                    if (poster == null) return null;
                    if (System.IO.File.Exists(poster.GetFullImagePath()))
                    {
                        return poster.GetFullImagePath();
                    }
                    else
                    {
                        logger.Trace("Could not find TvDB_Cover image: {0}", poster.GetFullImagePath());
                        return string.Empty;
                    }

                case ImageEntityType.TvDB_Banner:
                    TvDB_ImageWideBanner wideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(imageId);
                    if (wideBanner == null) return null;
                    if (System.IO.File.Exists(wideBanner.GetFullImagePath()))
                    {
                        return wideBanner.GetFullImagePath();
                    }
                    else
                    {
                        logger.Trace("Could not find TvDB_Banner image: {0}", wideBanner.GetFullImagePath());
                        return string.Empty;
                    }

                case ImageEntityType.TvDB_Episode:
                    TvDB_Episode ep = RepoFactory.TvDB_Episode.GetByID(imageId);
                    if (ep == null) return null;
                    if (System.IO.File.Exists(ep.GetFullImagePath()))
                    {
                        return ep.GetFullImagePath();
                    }
                    else
                    {
                        logger.Trace("Could not find TvDB_Episode image: {0}", ep.GetFullImagePath());
                        return string.Empty;
                    }

                case ImageEntityType.TvDB_FanArt:
                    TvDB_ImageFanart fanart = RepoFactory.TvDB_ImageFanart.GetByID(imageId);
                    if (fanart == null) return null;
                    if (System.IO.File.Exists(fanart.GetFullImagePath()))
                    {
                        return fanart.GetFullImagePath();
                    }
                        
                    logger.Trace("Could not find TvDB_FanArt image: {0}", fanart.GetFullImagePath());
                    return string.Empty;

                case ImageEntityType.MovieDB_Poster:
                    MovieDB_Poster mPoster = RepoFactory.MovieDB_Poster.GetByID(imageId);
                    if (mPoster == null) return null;

                    // now find only the original size
                    mPoster = RepoFactory.MovieDB_Poster.GetByOnlineID(mPoster.URL);
                    if (mPoster == null) return null;
                    if (System.IO.File.Exists(mPoster.GetFullImagePath()))
                    {
                        return mPoster.GetFullImagePath();
                    }
                    else
                    {
                        logger.Trace("Could not find MovieDB_Poster image: {0}", mPoster.GetFullImagePath());
                        return string.Empty;
                    }

                case ImageEntityType.MovieDB_FanArt:
                    MovieDB_Fanart mFanart = RepoFactory.MovieDB_Fanart.GetByID(imageId);
                    if (mFanart == null) return null;
                    mFanart = RepoFactory.MovieDB_Fanart.GetByOnlineID(mFanart.URL);
                    if (mFanart == null) return null;
                    if (System.IO.File.Exists(mFanart.GetFullImagePath()))
                    {
                        return mFanart.GetFullImagePath();
                    }
                    else
                    {
                        logger.Trace("Could not find MovieDB_FanArt image: {0}", mFanart.GetFullImagePath());
                        return string.Empty;
                    }

                default:
                    return string.Empty;
            }
        }
    }
}