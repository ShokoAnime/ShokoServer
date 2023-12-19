using System.IO;
using Microsoft.AspNetCore.Mvc;
using Shoko.Models.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Properties;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Mime = MimeMapping.MimeUtility;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
public class ImageController : BaseController
{
    private const string ImageNotFound = "The requested resource does not exist.";

    /// <summary>
    /// Returns the image for the given <paramref name="source"/>, <paramref name="type"/> and <paramref name="value"/>.
    /// </summary>
    /// <param name="source">AniDB, TvDB, MovieDB, Shoko</param>
    /// <param name="type">Poster, Fanart, Banner, Thumb, Static</param>
    /// <param name="value">Usually the ID, but the resource name in the case of image/Shoko/Static/{value}</param>
    /// <returns>200 on found, 400/404 if the type or source are invalid, and 404 if the id is not found</returns>
    [HttpGet("{source}/{type}/{value}")]
    [ResponseCache(Duration = 3600 /* 1 hour in seconds */)]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(404)]
    public ActionResult GetImage([FromRoute] Image.ImageSource source, [FromRoute] Image.ImageType type,
        [FromRoute] string value)
    {
        // No value no image.
        if (string.IsNullOrEmpty(value))
            return NotFound(ImageNotFound);

        var sourceType = Image.GetImageTypeFromSourceAndType(source, type) ?? ImageEntityType.None;
        switch (sourceType)
        {
            // Unrecognised combination of source and type.
            case ImageEntityType.None:
                return NotFound(ImageNotFound);

            // Static resources are stored in the resource manager.
            case ImageEntityType.Static:
            {
                var fileName = Path.GetFileNameWithoutExtension(value);
                var buffer = (byte[])Resources.ResourceManager.GetObject(fileName);
                if (buffer == null || buffer.Length == 0)
                    return NotFound(ImageNotFound);

                return File(buffer, Mime.GetMimeMapping(fileName) ?? "image/png");
            }

            // User avatars are stored in the database.
            case ImageEntityType.UserAvatar:
            {
                if (!int.TryParse(value, out var id))
                    return NotFound(ImageNotFound);

                var user = RepoFactory.JMMUser.GetByID(id);
                if (!user.HasAvatarImage)
                    return NotFound(ImageNotFound);

                return File(user.AvatarImageBlob, user.AvatarImageMetadata.ContentType);
            }

            // All other valid types.
            default:
            {
                if (!int.TryParse(value, out var id))
                    return NotFound(ImageNotFound);

                var path = Image.GetImagePath(sourceType, id);
                if (string.IsNullOrEmpty(path))
                    return NotFound(ImageNotFound);

                return File(System.IO.File.OpenRead(path), Mime.GetMimeMapping(path));
            }
        }
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
        if (imageType == Image.ImageType.Static || imageType == Image.ImageType.Avatar)
            return ValidationProblem("Unsupported image type for random image.", "imageType");

        var source = Image.GetRandomImageSource(imageType);
        var sourceType = Image.GetImageTypeFromSourceAndType(source, imageType) ?? ImageEntityType.None;
        if (sourceType == ImageEntityType.None)
            return InternalError("Could not generate a valid image type to fetch.");

        var id = Image.GetRandomImageID(sourceType);
        if (!id.HasValue)
            return InternalError("Unable to find a random image to send.");

        var path = Image.GetImagePath(sourceType, id.Value);
        if (string.IsNullOrEmpty(path))
            return InternalError("Unable to load image from disk.");

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
            return ValidationProblem("Unsupported image type for random image.", "imageType");

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
