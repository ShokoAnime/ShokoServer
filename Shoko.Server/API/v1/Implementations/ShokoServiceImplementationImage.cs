using System;
using System.IO;
using ImageMagick;
using Microsoft.AspNetCore.Mvc;
using NLog;
using Shoko.Abstractions.Services;
using Shoko.Server.API.v1.Models;
using Shoko.Server.Extensions;
using Shoko.Server.Properties;

using Mime = MimeMapping.MimeUtility;

namespace Shoko.Server.API.v1.Implementations;

[ApiController]
[Route("/api/Image")]
[ApiVersionNeutral]
[ApiExplorerSettings(IgnoreApi = true)]
public class ShokoServiceImplementationImage(IImageManager imageManager) : Controller
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    [HttpGet("{imageId}/{imageType}/{thumbnailOnly?}")]
    public object GetImage(int imageId, int imageType, bool? thumbnailOnly = false)
    {
        var path = GetImagePath(imageId, imageType, thumbnailOnly);
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
    internal static Stream ResizeImageToRatio(Stream imageStream, float newRatio)
    {
        if (Math.Abs(newRatio) < 0.1F)
            return imageStream;

        var image = new MagickImage(imageStream);
        float originalWidth = image.Width;
        float originalHeight = image.Height;
        uint newWidth, newHeight;

        var calculatedWidth = originalWidth;
        var calculatedHeight = originalHeight;

        do
        {
            var newHeightFloat = calculatedWidth / newRatio;
            if (newHeightFloat > originalHeight + 0.5F)
            {
                calculatedWidth *= originalHeight / newHeightFloat;
            }
            else
            {
                calculatedHeight = newHeightFloat;
            }
        } while (calculatedHeight > originalHeight + 0.5F);

        newWidth = (uint)Math.Round(calculatedWidth);
        newHeight = (uint)Math.Round(calculatedHeight);
        image.Resize(new MagickGeometry(newWidth, newHeight));

        var outStream = new MemoryStream();
        image.Write(outStream, MagickFormat.Png);
        outStream.Seek(0, SeekOrigin.Begin);

        return outStream;
    }

    [HttpGet("Support/{name}/{ratio}")]
    public object GetSupportImage(string name, float? ratio)
    {
        if (string.IsNullOrEmpty(name))
        {
            return NotFound();
        }

        name = Path.GetFileNameWithoutExtension(name);
        if (string.IsNullOrEmpty(name) || Resources.ResourceManager.GetObject(name) is not byte[] dta || dta is { Length: 0 })
            return NotFound();

        var ms = new MemoryStream(dta);
        ms.Seek(0, SeekOrigin.Begin);
        if (!name.Contains("404") || ratio is null || Math.Abs(ratio.Value) < 0.001D)
            return File(ms, "image/png");

        return File(ResizeImageToRatio(ms, ratio.Value), "image/png");
    }

    [HttpGet("Thumb/{imageId}/{imageType}/{ratio}")]
    public object GetThumb(int imageId, int imageType, float ratio)
    {
        var m = GetImage(imageId, imageType);
        if (m == NotFound())
        {
            return m;
        }

        if (m is not Stream image)
        {
            return NotFound();
        }

        return ResizeImageToRatio(image, ratio);
    }

    [HttpGet("Path/{imageId}/{imageType}/{thumbnailOnly?}")]
    public string GetImagePath(int imageId, int imageType, bool? thumbnailOnly)
    {
        try
        {
            var it = (CL_ImageEntityType)imageType;
            return imageManager.GetImage(it.ToServerSource(), it.ToServerType(), imageId) is { } metadata && metadata.IsLocalAvailable ? metadata.LocalPath! : string.Empty;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.ToString());
            return string.Empty;
        }
    }
}
