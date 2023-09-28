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
using Shoko.Server.ImageDownload;
using Shoko.Server.Properties;
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
        try
        {
            var it = (CL_ImageEntityType)imageType;
            return ImageUtils.GetLocalPath(it, imageId, true) ?? string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return string.Empty;
        }
    }
}
