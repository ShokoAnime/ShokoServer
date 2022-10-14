using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NLog;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Server.Extensions;
using Shoko.Server.Properties;
using Shoko.Server.Repositories;
using Mime = MimeMapping.MimeUtility;

namespace Shoko.Server;

[ApiController]
[Route("/api/Image")]
[ApiVersionNeutral]
[ApiExplorerSettings(IgnoreApi = true)]
public class ShokoServiceImplementationImage : Controller, IShokoServerImage, IHttpContextAccessor
{
    public HttpContext HttpContext { get; set; }

    private static Logger logger = LogManager.GetCurrentClassLogger();

    [HttpGet("{imageid}/{imageType}/{thumnbnailOnly?}")]
    public object GetImage(int imageid, int imageType, bool? thumnbnailOnly = false)
    {
        var path = GetImagePath(imageid, imageType, thumnbnailOnly);
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            return NotFound();
        }

        var mime = Mime.GetMimeMapping(path);
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
        var dta = Resources.blank;
        var ms = new MemoryStream(dta);
        ms.Seek(0, SeekOrigin.Begin);
        Response.ContentType = "image/jpeg";
        return ms;
    }

    [NonAction]
    internal static Bitmap ReSize(Bitmap im, int width, int height)
    {
        var dest = new Bitmap(width, height);
        using (var g = Graphics.FromImage(dest))
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
            var stream = new MemoryStream();
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

        var newwidth = (int)Math.Round(calcwidth);
        var newheight = (int)Math.Round(calcheight);
        var x = 0;
        var y = 0;
        if (newwidth < im.Width)
        {
            x = (im.Width - newwidth) / 2;
        }

        if (newheight < im.Height)
        {
            y = (im.Height - newheight) / 2;
        }

        Image im2 = ReSize(new Bitmap(im), newwidth, newheight);
        var g = Graphics.FromImage(im2);
        g.DrawImage(im, new Rectangle(0, 0, im2.Width, im2.Height),
            new Rectangle(x, y, im2.Width, im2.Height), GraphicsUnit.Pixel);
        var ms = new MemoryStream();
        im2.Save(ms, ImageFormat.Jpeg);
        ms.Seek(0, SeekOrigin.Begin);
        Response.ContentType = "image/jpeg";
        return ms;
    }

    [HttpGet("Support/{name}/{ratio}")]
    public object GetSupportImage(string name, float? ratio)
    {
        if (string.IsNullOrEmpty(name))
        {
            return NotFound();
        }

        name = Path.GetFileNameWithoutExtension(name);
        var man = Resources.ResourceManager;
        var dta = (byte[])man.GetObject(name);
        if (dta == null || dta.Length == 0)
        {
            return NotFound();
        }

        //Little hack
        var ms = new MemoryStream(dta);
        ms.Seek(0, SeekOrigin.Begin);
        if (!name.Contains("404") || ratio == null || Math.Abs(ratio.Value) < 0.001D)
        {
            Response.ContentType = "image/png";
            return ms;
        }

        var im = Image.FromStream(ms);
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
            {
                nh = h;
            }
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
            {
                nw = w;
            }
        }

        nw = (float)Math.Round(nw);
        nh = (float)Math.Round(nh);
        Image im2 = new Bitmap((int)nw, (int)nh, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(im2))
        {
            g.InterpolationMode = nw >= im.Width
                ? InterpolationMode.HighQualityBilinear
                : InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.Clear(Color.Transparent);
            var src = new Rectangle(0, 0, im.Width, im.Height);
            var dst = new Rectangle((int)((nw - w) / 2), (int)((nh - h) / 2), im.Width, im.Height);
            g.DrawImage(im, dst, src, GraphicsUnit.Pixel);
        }

        var ms2 = new MemoryStream();
        im2.Save(ms2, ImageFormat.Png);
        ms2.Seek(0, SeekOrigin.Begin);
        ms.Dispose();
        Response.ContentType = "image/png";
        return ms2;
    }

    [HttpGet("Thumb/{imageId}/{imageType}/{ratio}")]
    public object GetThumb(int imageId, int imageType, float ratio)
    {
        var m = GetImage(imageId, imageType);
        if (m == NotFound())
        {
            return m;
        }

        if (!(m is Stream image))
        {
            return NotFound();
        }

        using (var im = Image.FromStream(image))
        {
            return ResizeToRatio(im, ratio);
        }
    }

    [HttpGet("Path/{imageId}/{imageType}/{thumnbnailOnly?}")]
    public string GetImagePath(int imageId, int imageType, bool? thumnbnailOnly)
    {
        var it = (ImageEntityType)imageType;

        switch (it)
        {
            case ImageEntityType.AniDB_Cover:
                var anime = RepoFactory.AniDB_Anime.GetByAnimeID(imageId);
                if (anime == null)
                {
                    return null;
                }

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
                var chr = RepoFactory.AniDB_Character.GetByID(imageId);
                if (chr == null)
                {
                    return null;
                }

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
                var creator = RepoFactory.AniDB_Seiyuu.GetByID(imageId);
                if (creator == null)
                {
                    return string.Empty;
                }

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
                var poster = RepoFactory.TvDB_ImagePoster.GetByID(imageId);
                if (poster == null)
                {
                    return null;
                }

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
                var wideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(imageId);
                if (wideBanner == null)
                {
                    return null;
                }

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
                var ep = RepoFactory.TvDB_Episode.GetByID(imageId);
                if (ep == null)
                {
                    return null;
                }

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
                var fanart = RepoFactory.TvDB_ImageFanart.GetByID(imageId);
                if (fanart == null)
                {
                    return null;
                }

                if (System.IO.File.Exists(fanart.GetFullImagePath()))
                {
                    return fanart.GetFullImagePath();
                }

                logger.Trace("Could not find TvDB_FanArt image: {0}", fanart.GetFullImagePath());
                return string.Empty;

            case ImageEntityType.MovieDB_Poster:
                var mPoster = RepoFactory.MovieDB_Poster.GetByID(imageId);
                if (mPoster == null)
                {
                    return null;
                }

                // now find only the original size
                mPoster = RepoFactory.MovieDB_Poster.GetByOnlineID(mPoster.URL);
                if (mPoster == null)
                {
                    return null;
                }

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
                var mFanart = RepoFactory.MovieDB_Fanart.GetByID(imageId);
                if (mFanart == null)
                {
                    return null;
                }

                mFanart = RepoFactory.MovieDB_Fanart.GetByOnlineID(mFanart.URL);
                if (mFanart == null)
                {
                    return null;
                }

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
