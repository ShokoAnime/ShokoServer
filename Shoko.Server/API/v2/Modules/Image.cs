using System;
using System.Globalization;
using System.IO;
using ImageMagick;
using Microsoft.AspNetCore.Mvc;
using Shoko.Models.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.ImageDownload;
using Shoko.Server.Properties;
using Shoko.Server.Settings;
using Mime = MimeMapping.MimeUtility;

namespace Shoko.Server.API.v2.Modules;

[ApiController]
[Route("/api/image")]
[Route("/api/v2/image")]
[ApiVersion("2.0")]
public class Image : BaseController
{
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
        var path = ImageUtils.GetLocalPath((CL_ImageEntityType)type, id, true);

        if (string.IsNullOrEmpty(path))
        {
            Response.StatusCode = 404;
            return File(MissingImage(), "image/png");
        }

        return File(System.IO.File.OpenRead(path), Mime.GetMimeMapping(path));
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
        if (!float.TryParse(ratio, NumberStyles.AllowDecimalPoint, CultureInfo.CreateSpecificCulture("en-EN"),
                out var newratio))
        {
            newratio = 0.6667f;
        }

        var path = ImageUtils.GetLocalPath((CL_ImageEntityType)type, id, true);

        if (string.IsNullOrEmpty(path))
        {
            Response.StatusCode = 404;
            return File(MissingImage(), "image/png");
        }

        var fs = System.IO.File.OpenRead(path);
        contentType = Mime.GetMimeMapping(path);
        return File(ResizeImageToRatio(fs, newratio), contentType);
    }

    /// <summary>
    /// Return SupportImage (build-in server)
    /// </summary>
    /// <param name="name">image file name</param>
    /// <returns></returns>
    [HttpGet("support/{name}")]
    [InitFriendly]
    [DatabaseBlockedExempt]
    public ActionResult GetSupportImage(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return APIStatus.NotFound();
        }

        name = Path.GetFileNameWithoutExtension(name);
        var man = Resources.ResourceManager;
        var dta = (byte[])man.GetObject(name);
        if (dta == null || dta.Length == 0)
        {
            return APIStatus.NotFound();
        }

        var ms = new MemoryStream(dta);
        ms.Seek(0, SeekOrigin.Begin);

        return File(ms, "image/png");
    }

    [HttpGet("support/{name}/{ratio}")]
    [InitFriendly]
    [DatabaseBlockedExempt]
    public ActionResult GetSupportImage(string name, string ratio)
    {
        if (string.IsNullOrEmpty(name))
        {
            return APIStatus.NotFound();
        }

        ratio = ratio.Replace(',', '.');
        float.TryParse(ratio, NumberStyles.AllowDecimalPoint,
            CultureInfo.CreateSpecificCulture("en-EN"), out var newratio);

        name = Path.GetFileNameWithoutExtension(name);
        var man = Resources.ResourceManager;
        var dta = (byte[])man.GetObject(name);
        if (dta == null || dta.Length == 0)
        {
            return APIStatus.NotFound();
        }

        var ms = new MemoryStream(dta);
        ms.Seek(0, SeekOrigin.Begin);
        return File(ResizeImageToRatio(ms, newratio), "image/png");
    }

    /// <summary>
    /// Return random image with given type and not from restricted content
    /// </summary>
    /// <param name="type">image type</param>
    /// <returns>image body inside stream</returns>
    [HttpGet("{type}/random")]
    public FileResult GetRandomImage(int type)
    {
        // Try 5 times to find a **valid** random image.
        var tries = 0;
        var imageType = (CL_ImageEntityType)type;
        do
        {
            var randomID = ImageUtils.GetRandomImageID(imageType);
            var path = randomID.HasValue ? ImageUtils.GetLocalPath(imageType, randomID.Value) : null;
            if (!string.IsNullOrEmpty(path))
                return File(System.IO.File.OpenRead(path), Mime.GetMimeMapping(path));
        } while (tries++ < 5);

        Response.StatusCode = 404;
        return File(MissingImage(), "image/png");
    }

    /// <summary>
    /// Internal function that return image for missing image
    /// </summary>
    /// <returns>Stream</returns>
    internal static Stream MissingImage()
    {
        var dta = Resources.blank;
        var ms = new MemoryStream(dta);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    internal static Stream ResizeImageToRatio(Stream imageStream, float newRatio)
    {
        if (Math.Abs(newRatio) < 0.1F)
            return imageStream;

        var image = new MagickImage(imageStream);
        float originalWidth = image.Width;
        float originalHeight = image.Height;
        int newWidth, newHeight;

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

        newWidth = (int)Math.Round(calculatedWidth);
        newHeight = (int)Math.Round(calculatedHeight);
        image.Resize(new MagickGeometry(newWidth, newHeight));

        var outStream = new MemoryStream();
        image.Write(outStream, MagickFormat.Png);
        outStream.Seek(0, SeekOrigin.Begin);

        return outStream;
    }

    public Image(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
