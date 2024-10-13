using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

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
    /// <param name="source">AniDB, TMDB, Shoko, etc.</param>
    /// <param name="type">Poster, Backdrop, Banner, Thumbnail, etc.</param>
    /// <param name="value">The image ID.</param>
    /// <returns>200 on found, 400/404 if the type or source are invalid, and 404 if the id is not found</returns>
    [HttpGet("{source}/{type}/{value}")]
    [ResponseCache(Duration = 3600 /* 1 hour in seconds */)]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(404)]
    public ActionResult GetImage([FromRoute] Image.ImageSource source, [FromRoute] Image.ImageType type,
        [FromRoute, Range(1, int.MaxValue)] int value)
    {
        // Unrecognized combination of source, type and/or value.
        var dataSource = source.ToServer();
        var imageEntityType = type.ToServer();
        if (imageEntityType == ImageEntityType.None || dataSource == DataSourceType.None)
            return NotFound(ImageNotFound);

        // User avatars are stored in the database.
        if (imageEntityType == ImageEntityType.Art && dataSource == DataSourceType.User)
        {
            var user = RepoFactory.JMMUser.GetByID(value);
            if (!user.HasAvatarImage)
                return NotFound(ImageNotFound);

            return File(user.AvatarImageBlob, user.AvatarImageMetadata.ContentType);
        }

        var metadata = ImageUtils.GetImageMetadata(dataSource, imageEntityType, value);
        if (metadata is null || metadata.GetStream() is not { } stream)
            return NotFound(ImageNotFound);

        return File(stream, metadata.ContentType);
    }

    /// <summary>
    /// Enable or disable an image. Disabled images are hidden unless explicitly
    /// asked for.
    /// </summary>
    /// <param name="source">AniDB, TMDB, Shoko, etc.</param>
    /// <param name="type">Poster, Backdrop, Banner, Thumbnail, etc.</param>
    /// <param name="value">The image ID.</param>
    /// <param name="body"></param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("{source}/{type}/{value}/Enabled")]
    public ActionResult EnableOrDisableImage([FromRoute] Image.ImageSource source, [FromRoute] Image.ImageType type, [FromRoute, Range(1, int.MaxValue)] int value, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Image.Input.EnableImageBody body)
    {
        // Unrecognized combination of source, type and/or value.
        var dataSource = source.ToServer();
        var imageEntityType = type.ToServer();
        if (imageEntityType == ImageEntityType.None || dataSource == DataSourceType.None)
            return NotFound(ImageNotFound);

        // User avatars are stored in the database.
        if (imageEntityType == ImageEntityType.Art && dataSource == DataSourceType.User)
        {
            var user = RepoFactory.JMMUser.GetByID(value);
            if (!user.HasAvatarImage)
                return NotFound(ImageNotFound);

            return ValidationProblem($"Unable to enable or disable user avatar with id {value}!");
        }

        var metadata = ImageUtils.GetImageMetadata(dataSource, imageEntityType, value);
        if (metadata is null)
            return NotFound(ImageNotFound);

        if (!ImageUtils.SetEnabled(dataSource, imageEntityType, value, body.Enabled))
            return ValidationProblem($"Unable to enable or disable {source} {type} with id {value}!");

        return NoContent();
    }

    /// <summary>
    /// Returns a random image for the <paramref name="imageType"/>.
    /// </summary>
    /// <param name="imageType">Poster, Backdrop, Banner, Thumb, Static</param>
    /// <returns>200 on found, 400/404 if the type or source are invalid, and 404 if the id is not found</returns>
    [HttpGet("Random/{imageType}")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public ActionResult GetRandomImageForType([FromRoute] Image.ImageType imageType)
    {
        if (imageType == Image.ImageType.Avatar)
            return ValidationProblem("Unsupported image type for random image.", "imageType");

        var dataSource = Image.GetRandomImageSource(imageType);
        var imageEntityType = imageType.ToServer();
        if (imageEntityType == ImageEntityType.None)
            return InternalError("Could not generate a valid image type to fetch.");

        // Try 5 times to get a valid image.
        var tries = 0;
        do
        {
            var metadata = ImageUtils.GetRandomImageID(dataSource, imageEntityType);
            if (metadata is null)
                break;

            if (!metadata.IsLocalAvailable)
                continue;

            var series = ImageUtils.GetFirstSeriesForImage(metadata);
            if (series == null || (series.AniDB_Anime?.IsRestricted ?? false))
                continue;

            if (metadata.GetStream(allowRemote: false) is not { } stream)
                continue;

            return File(stream, metadata.ContentType);
        } while (tries++ < 5);

        return InternalError("Unable to find a random image to send.");
    }

    /// <summary>
    /// Returns the metadata for a random image for the <paramref name="imageType"/>.
    /// </summary>
    /// <param name="imageType">Poster, Backdrop, Banner, Thumb</param>
    /// <returns>200 on found, 400 if the type or source are invalid</returns>
    [HttpGet("Random/{imageType}/Metadata")]
    [ProducesResponseType(typeof(Image), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public ActionResult<Image> GetRandomImageMetadataForType([FromRoute] Image.ImageType imageType)
    {
        if (imageType == Image.ImageType.Avatar)
            return ValidationProblem("Unsupported image type for random image.", "imageType");

        var dataSource = Image.GetRandomImageSource(imageType);
        var imageEntityType = imageType.ToServer();
        if (imageEntityType == ImageEntityType.None)
            return InternalError("Could not generate a valid image type to fetch.");

        // Try 5 times to get a valid image.
        var tries = 0;
        do
        {
            var metadata = ImageUtils.GetRandomImageID(dataSource, imageEntityType);
            if (metadata is null)
                break;

            if (!metadata.IsLocalAvailable)
                continue;

            var image = new Image(metadata);
            var series = ImageUtils.GetFirstSeriesForImage(metadata);
            if (series == null || (series.AniDB_Anime?.IsRestricted ?? false))
                continue;

            image.Series = new(series.AnimeSeriesID, series.PreferredTitle);

            return image;
        } while (tries++ < 5);

        return InternalError("Unable to find a random image to send.");
    }

    public ImageController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
