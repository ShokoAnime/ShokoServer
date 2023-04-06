using System.IO;
using Microsoft.AspNetCore.Mvc;
using Shoko.Models.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Properties;
using Shoko.Server.Settings;
using Mime = MimeMapping.MimeUtility;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
public class ImageController : BaseController
{
    /// <summary>
    /// Returns the image for the given <paramref name="source"/>, <paramref name="type"/> and <paramref name="value"/>.
    /// </summary>
    /// <param name="source">AniDB, TvDB, MovieDB, Shoko</param>
    /// <param name="type">Poster, Fanart, Banner, Thumb, Static</param>
    /// <param name="value">Usually the ID, but the resource name in the case of image/Shoko/Static/{value}</param>
    /// <returns>200 on found, 400/404 if the type or source are invalid, and 404 if the id is not found</returns>
    [HttpGet("{source}/{type}/{value}")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(404)]
    public ActionResult GetImage([FromRoute] Image.ImageSource source, [FromRoute] Image.ImageType type,
        [FromRoute] string value)
    {
        var sourceType = Image.GetImageTypeFromSourceAndType(source, type) ?? ImageEntityType.None;
        if (sourceType == ImageEntityType.None)
        {
            return NotFound("The requested resource does not exist.");
        }

        if (sourceType == ImageEntityType.Static)
        {
            return GetStaticImage(value);
        }

        if (!int.TryParse(value, out var id))
        {
            return NotFound("The requested resource does not exist.");
        }

        var path = Image.GetImagePath(sourceType, id);
        if (string.IsNullOrEmpty(path))
        {
            return NotFound("The requested resource does not exist.");
        }

        return File(System.IO.File.OpenRead(path), Mime.GetMimeMapping(path));
    }

    /// <summary>
    /// Gets a static server resource
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    [NonAction]
    public ActionResult GetStaticImage(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return NotFound("The requested resource does not exist.");
        }

        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var buffer = (byte[])Resources.ResourceManager.GetObject(fileName);
        if (buffer == null || buffer.Length == 0)
        {
            return NotFound("The requested resource does not exist.");
        }

        return File(buffer, Mime.GetMimeMapping(fileName) ?? "image/png");
    }

    /// <summary>
    /// Returns a random image for the <paramref name="imageType"/>.
    /// </summary>
    /// <param name="imageType">Poster, Fanart, Banner, Thumb, Static</param>
    /// <returns>200 on found, 400/404 if the type or source are invalid, and 404 if the id is not found</returns>
    [HttpGet("Random/{imageType}")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public ActionResult GetRandomImageForType([FromRoute] Image.ImageType imageType)
    {
        if (imageType == Image.ImageType.Static)
        {
            return BadRequest("Unsupported image type for random image.");
        }

        var source = Image.GetRandomImageSource(imageType);
        var sourceType = Image.GetImageTypeFromSourceAndType(source, imageType) ?? ImageEntityType.None;
        if (sourceType == ImageEntityType.None)
        {
            return InternalError("Could not generate a valid image type to fetch.");
        }

        var id = Image.GetRandomImageID(sourceType);
        if (!id.HasValue)
        {
            return InternalError("Unable to find a random image to send.");
        }

        var path = Image.GetImagePath(sourceType, id.Value);
        if (string.IsNullOrEmpty(path))
        {
            return InternalError("Unable to load image from disk.");
        }

        return File(System.IO.File.OpenRead(path), Mime.GetMimeMapping(path));
    }

    /// <summary>
    /// Returns the metadata for a random image for the <paramref name="imageType"/>.
    /// </summary>
    /// <param name="imageType">Poster, Fanart, Banner, Thumb</param>
    /// <returns>200 on found, 400 if the type or source are invalid</returns>
    [HttpGet("Random/{imageType}/Metadata")]
    [ProducesResponseType(typeof(Image), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public ActionResult<Image> GetRandomImageMetadataForType([FromRoute] Image.ImageType imageType)
    {
        if (imageType == Image.ImageType.Static)
            return BadRequest("Unsupported image type for random image.");

        var source = Image.GetRandomImageSource(imageType);
        var sourceType = Image.GetImageTypeFromSourceAndType(source, imageType) ?? ImageEntityType.None;
        if (sourceType == ImageEntityType.None)
            return InternalError("Could not generate a valid image type to fetch.");

        // Try 5 times to get a valid image.
        var tries = 0;
        do 
        {
            var id = Image.GetRandomImageID(sourceType);
            if (!id.HasValue)
                break;

            var path = Image.GetImagePath(sourceType, id.Value);
            if (string.IsNullOrEmpty(path))
                continue;

            var image = new Image(id.Value, sourceType, false, false);
            var series = Image.GetFirstSeriesForImage(sourceType, id.Value);
            if (series != null)
                image.Series = new(series.AnimeSeriesID, series.GetSeriesName());

            return image;
        } while (tries++ < 5);

        return InternalError("Unable to find a random image to send.");
    }

    public ImageController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
