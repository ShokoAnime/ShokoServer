using System;
using System.Globalization;
using System.IO;
using ImageMagick;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.Web.Attributes;
using Shoko.Server.API.v1.Models;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Extensions;
using Shoko.Server.Properties;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.API.v2.Modules;

[ApiController]
[Route("/api/image")]
[Route("/api/v2/image")]
[ApiVersion("2.0")]
public class Image(IImageManager imageManager, ISettingsProvider settingsProvider) : BaseController(settingsProvider)
{
    [HttpGet("validateall")]
    public ActionResult ValidateAll()
        => APIStatus.BadRequest("Use APIv3.");

    /// <summary>
    /// Return image with given id, type
    /// </summary>
    /// <param name="id">image id</param>
    /// <param name="type">image type</param>
    /// <returns>image body inside stream</returns>
    [HttpGet("{type}/{id}")]
    public ActionResult GetImage(int type, int id)
    {
        var imageType = (CL_ImageEntityType)type;
        var metadata = imageManager.GetImage(imageType.ToServerSource(), imageType.ToServerType(), id);
        if (metadata is null || metadata.GetStream() is not { } stream)
            return APIStatus.NotFound();

        return File(stream, metadata.ContentType);
    }

    /// <summary>
    /// Return thumb with given id, type
    /// </summary>
    /// <param name="id">image id</param>
    /// <param name="type">image type</param>
    /// <param name="ratio">new image ratio</param>
    /// <returns>resize image body inside stream</returns>
    [HttpGet("thumb/{type}/{id}/{ratio?}")]
    public ActionResult GetThumb(int type, int id, string ratio = "0")
    {
        if (!float.TryParse(ratio.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.CreateSpecificCulture("en-EN"), out var newRatio))
            newRatio = 0.6667f;

        var imageType = (CL_ImageEntityType)type;
        var metadata = imageManager.GetImage(imageType.ToServerSource(), imageType.ToServerType(), id);
        if (metadata is null || metadata.GetStream() is not { } stream)
            return APIStatus.NotFound();

        return File(ResizeImageToRatio(stream, newRatio), metadata.ContentType);
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
            return APIStatus.NotFound();

        name = Path.GetFileNameWithoutExtension(name);
        if (string.IsNullOrEmpty(name) || Resources.ResourceManager.GetObject(name) is not byte[] dta || dta is { Length: 0 })
            return APIStatus.NotFound();

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
            return APIStatus.NotFound();

        name = Path.GetFileNameWithoutExtension(name);
        if (string.IsNullOrEmpty(name) || Resources.ResourceManager.GetObject(name) is not byte[] dta || dta is { Length: 0 })
            return APIStatus.NotFound();

        var ms = new MemoryStream(dta);
        ms.Seek(0, SeekOrigin.Begin);
        float.TryParse(ratio.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.CreateSpecificCulture("en-EN"), out var newRatio);
        return File(ResizeImageToRatio(ms, newRatio), "image/png");
    }

    /// <summary>
    /// Return random image with given type and not from restricted content
    /// </summary>
    /// <param name="type">image type</param>
    /// <returns>image body inside stream</returns>
    [HttpGet("{type}/random")]
    public ActionResult GetRandomImage(int type)
    {
        // Try 5 times to find a **valid** random image.
        var tries = 0;
        var imageType = (CL_ImageEntityType)type;
        while (tries++ < 5)
        {
            var metadata = imageManager.GetRandomImage(imageType.ToServerSource(), imageType.ToServerType());
            if (metadata is not null && metadata.GetStream() is { } stream)
                return File(stream, metadata.ContentType);
        }

        return APIStatus.NotFound();
    }

    private static Stream ResizeImageToRatio(Stream imageStream, float newRatio)
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
}
