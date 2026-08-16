using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
public class ImageController(IImageManager imageManager, ISettingsProvider settingsProvider) : BaseController(settingsProvider)
{
    private const string ImageNotFound = "The requested image does not exist.";

    /// <summary>
    /// Returns the image for the given <paramref name="imageID"/>.
    /// </summary>
    /// <param name="imageID">The image ID.</param>
    /// <returns>200 on found, 400/404 if the type or source are invalid, and 404 if the id is not found</returns>
    [HttpGet("{imageID}")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(404)]
    public ActionResult GetImage(
        [FromRoute] Guid imageID
    )
    {
        var metadata = imageManager.GetImageByID(imageID);
        if (metadata is null || metadata.GetStream() is not { } stream)
            return NotFound(ImageNotFound);

        Response.Headers["Cache-Control"] = "public, max-age=3600";
        return File(stream, metadata.ContentType);
    }

    /// <summary>
    /// Returns the image for the given <paramref name="source"/> and <paramref name="resourceID"/>.
    /// </summary>
    [HttpGet("Remote/{source}/{*resourceID}")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(404)]
    public ActionResult GetRemoteImage(
        [FromRoute] DataSource source,
        [FromRoute] string resourceID
    )
        => source.IsLocal ? NotFound(ImageNotFound) : GetImage(IImageManager.GetIDForImageSourceAndResourceID(source, resourceID));

    /// <summary>
    /// Returns a random image for the <paramref name="imageType"/>.
    /// </summary>
    /// <param name="imageType">Primary, Backdrop, Banner</param>
    /// <returns>200 on found, 400 if the type is unsupported, and 404 if no image could be found</returns>
    [HttpGet("Random/{imageType}")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public ActionResult GetRandomImageForType([FromRoute] ImageEntityType imageType)
    {
        if (imageType is ImageEntityType.None or ImageEntityType.Logo or ImageEntityType.Disc)
            return ValidationProblem("Unsupported image type for random image.", nameof(imageType));

        var dataSource = Image.GetRandomImageSource(imageType);

        // Try 5 times to get a valid image.
        var tries = 0;
        do
        {
            var metadata = imageManager.GetRandomImageCrossReference(dataSource, imageType, new() { IsAvailable = true })?.GetImage();
            if (metadata is null)
                continue;

            var series = imageManager.GetFirstSeriesForImage(metadata);
            if (series == null || series.AnidbAnime.Restricted)
                continue;

            if (metadata.GetStream() is not { } stream)
                continue;

            return File(stream, metadata.ContentType);
        } while (tries++ < 5);

        return NotFound("Unable to find a random image to send.");
    }

    /// <summary>
    /// Returns the metadata for a random image for the <paramref name="imageType"/>.
    /// </summary>
    /// <param name="imageType">Primary, Backdrop, Banner</param>
    /// <param name="includeRestricted">Include or exclude restricted images</param>
    /// <param name="seriesType">Series types to include in the search</param>
    /// <param name="maxAttempts">Maximum number of attempts to find a valid image</param>
    /// <returns>200 on found, 400 if the type is unsupported, and 404 if no image could be found</returns>
    [HttpGet("Random/{imageType}/Metadata")]
    [ProducesResponseType(typeof(Image), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public ActionResult<Image> GetRandomImageMetadataForType(
        [FromRoute] ImageEntityType imageType,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AnimeType>? seriesType = null,
        [FromQuery, Range(0, 100)] int maxAttempts = 5
    )
    {
        if (imageType is ImageEntityType.None or ImageEntityType.Logo or ImageEntityType.Disc)
            return ValidationProblem("Unsupported image type for random image.", nameof(imageType));

        var dataSource = Image.GetRandomImageSource(imageType);

        // Try 5 times to get a valid image.
        var tries = 0;
        do
        {
            var metadata = imageManager.GetRandomImageCrossReference(dataSource, imageType, new() { IsAvailable = true })?.GetImage();
            if (metadata is null)
                continue;

            var image = new Image(metadata);
            var series = imageManager.GetFirstSeriesForImage(metadata);
            if (series?.AnidbAnime is not { } anime)
                continue;

            if (includeRestricted != IncludeOnlyFilter.True)
            {
                var onlyRestricted = includeRestricted is IncludeOnlyFilter.Only;
                if (onlyRestricted != anime.Restricted)
                    continue;
            }

            if (seriesType is not null && !seriesType.Contains(anime.Type))
                continue;

            image.Series = new(series.ID, series.Title);

            return image;
        } while (tries++ < maxAttempts);

        return NotFound("Unable to find a random image to send.");
    }
}
