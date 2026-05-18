using System;
using System.Globalization;
using System.IO;
using ImageMagick;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Web.Attributes;
using Shoko.Server.API.v1.Models;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Properties;
using Shoko.Server.Settings;

#pragma warning disable CS0618 // Type or member is obsolete
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
        var metadata = imageManager.GetImageByID(id);
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

        if (imageManager.GetImageByID(id)?.GetStream() is not { } stream)
            return APIStatus.NotFound();

        return File(ResizeImageToRatio(stream, newRatio), "image/png");
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
            var metadata = imageManager.GetRandomImageCrossReference(imageType.ToServerSource(), imageType.ToServerType())?.GetImage();
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

public static class APIv2ImageExtensions
{
    extension(CL_ImageEntityType type)
    {
        public ImageEntityType ToServerType()
        => type switch
        {
            CL_ImageEntityType.AniDB_Character => ImageEntityType.Primary,
            CL_ImageEntityType.AniDB_Cover => ImageEntityType.Primary,
            CL_ImageEntityType.AniDB_Creator => ImageEntityType.Primary,
            CL_ImageEntityType.MovieDB_FanArt => ImageEntityType.Backdrop,
            CL_ImageEntityType.MovieDB_Poster => ImageEntityType.Primary,
            _ => ImageEntityType.None,
        };

        public DataSource ToServerSource()
        => type switch
        {
            CL_ImageEntityType.AniDB_Character => DataSource.AniDB,
            CL_ImageEntityType.AniDB_Cover => DataSource.AniDB,
            CL_ImageEntityType.AniDB_Creator => DataSource.AniDB,
            CL_ImageEntityType.MovieDB_FanArt => DataSource.TMDB,
            CL_ImageEntityType.MovieDB_Poster => DataSource.TMDB,
            _ => DataSource.None,
        };
    }
}
