using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
    private const string ImageNotFound = "The requested resource does not exist.";

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
    /// Returns the image for the given <paramref name="source"/>, <paramref name="type"/> and <paramref name="value"/>.
    /// </summary>
    /// <remarks>
    /// <b>Deprecated:</b> Legacy endpoint for backwards compatibility only. Clients are advised to switch to using
    /// <c>{imageID}</c> instead.
    /// </remarks>
    /// <param name="source">AniDB, TMDB, Shoko, etc.</param>
    /// <param name="type">Poster, Backdrop, Banner, Thumbnail, etc.</param>
    /// <param name="value">The image ID.</param>
    /// <returns>200 on found, 400/404 if the type or source are invalid, and 404 if the id is not found</returns>
    [HttpGet("{source}/{type}/{value}")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(404)]
    [Obsolete("Legacy endpoint for backwards compatibility only. Clients are advised to switch to using {imageID} instead.")]
    public ActionResult GetLegacyImage(
        [FromRoute] DataSource source,
        [FromRoute] Image.LegacyImageType type,
        [FromRoute, Range(1, int.MaxValue)] int value
    )
    {
        var metadata = imageManager.GetImageByID(value);
        if (metadata is null || metadata.GetStream() is not { } stream)
            return NotFound(ImageNotFound);

        Response.Headers["Cache-Control"] = "public, max-age=3600";
        return File(stream, metadata.ContentType);
    }

    /// <summary>
    /// Enable or disable an image. Disabled images are hidden unless explicitly
    /// asked for.
    /// </summary>
    /// <remarks>
    /// <b>Deprecated:</b> Use the management controller's enabled endpoint for
    /// the image or cross-reference, preferably the cross-reference.
    /// </remarks>
    /// <param name="source">AniDB, TMDB, Shoko, etc.</param>
    /// <param name="type">Poster, Backdrop, Banner, Thumbnail, etc.</param>
    /// <param name="value">The image ID.</param>
    /// <param name="body"></param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("{source}/{type}/{value}/Enabled")]
    [Obsolete("Use the management controller's enabled endpoint for the image or cross-reference, preferably the cross-reference.")]
    public async Task<ActionResult> EnableOrDisableLegacyImage(
        [FromRoute] DataSource source,
        [FromRoute] Image.LegacyImageType type,
        [FromRoute, Range(1, int.MaxValue)] int value,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Image.Input.EnableImageBody body
    )
    {
        var metadata = imageManager.GetImageByID(value);
        if (metadata is null)
            return NotFound(ImageNotFound);

        // Enabled state is now tied to cross-references.
        if (metadata.GetCrossReferences() is { Count: 0 })
            return ValidationProblem($"Unable to enable or disable {source} {type} with id {value}!");

        imageManager.EnableImage(metadata, body.Enabled);

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
    public ActionResult GetRandomImageForType([FromRoute] Image.LegacyImageType imageType)
    {
        if (imageType == Image.LegacyImageType.Avatar)
            return ValidationProblem("Unsupported image type for random image.", "imageType");

        var dataSource = Image.GetRandomImageSource(imageType);
        var imageEntityType = imageType.ToServer();
        if (imageEntityType == ImageEntityType.None)
            return InternalError("Could not generate a valid image type to fetch.");

        // Try 5 times to get a valid image.
        var tries = 0;
        do
        {
            var metadata = imageManager.GetRandomImageCrossReference(dataSource, imageEntityType, new() { IsAvailable = true })?.GetImage();
            if (metadata is null)
                continue;

            var series = imageManager.GetFirstSeriesForImage(metadata);
            if (series == null || series.AnidbAnime.Restricted)
                continue;

            if (metadata.GetStream() is not { } stream)
                continue;

            return File(stream, metadata.ContentType);
        } while (tries++ < 5);

        return InternalError("Unable to find a random image to send.");
    }

    /// <summary>
    /// Returns the metadata for a random image for the <paramref name="imageType"/>.
    /// </summary>
    /// <param name="imageType">Poster, Backdrop, Banner, Thumb</param>
    /// <param name="includeRestricted">Include or exclude restricted images</param>
    /// <param name="seriesType">Series types to include in the search</param>
    /// <param name="maxAttempts">Maximum number of attempts to find a valid image</param>
    /// <returns>200 on found, 400 if the type or source are invalid</returns>
    [HttpGet("Random/{imageType}/Metadata")]
    [ProducesResponseType(typeof(Image), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public ActionResult<Image> GetRandomImageMetadataForType(
        [FromRoute] Image.LegacyImageType imageType,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AnimeType>? seriesType = null,
        [FromQuery, Range(0, 100)] int maxAttempts = 5
    )
    {
        if (imageType == Image.LegacyImageType.Avatar)
            return ValidationProblem("Unsupported image type for random image.", "imageType");

        var dataSource = Image.GetRandomImageSource(imageType);
        var imageEntityType = imageType.ToServer();
        if (imageEntityType == ImageEntityType.None)
            return InternalError("Could not generate a valid image type to fetch.");

        // Try 5 times to get a valid image.
        var tries = 0;
        do
        {
            var metadata = imageManager.GetRandomImageCrossReference(dataSource, imageEntityType, new() { IsAvailable = true })?.GetImage();
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

        return InternalError("Unable to find a random image to send.");
    }
}
