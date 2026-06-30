using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.Exceptions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.ImageManagement;
using Shoko.Server.API.v3.Models.ImageManagement.Input;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
///   Controller for managing images and image cross-references. Exposes
///   <see cref="IImageManager"/> capabilities including template URL
///   management, image CRUD with upload, download/purge/validate operations,
///   and full cross-reference management with entity-scoped lookups.
/// </summary>
/// <param name="imageManager">The image manager service.</param>
/// <param name="settingsProvider">The settings provider.</param>
[ApiController]
[Route("/api/v{version:apiVersion}/Image/Management")]
[ApiV3]
[Authorize]
public class ImageManagementController(IImageManager imageManager, ISettingsProvider settingsProvider) : BaseController(settingsProvider)
{
    private const string ImageNotFound = "The requested image does not exist.";
    private const string CrossReferenceNotFound = "The requested image cross-reference does not exist.";
    private const string EntityNotFound = "The requested entity does not exist.";

    #region Image Sources

    /// <summary>
    ///   Get all template URLs for image sources.
    /// </summary>
    /// <returns>A dictionary mapping data sources to their template URLs.</returns>
    [HttpGet("Source")]
    public ActionResult<Dictionary<DataSource, string?>> GetTemplateUrls()
        => imageManager.GetTemplateUrls().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    /// <summary>
    ///   Get the template URL for a specific image source.
    /// </summary>
    /// <param name="source">The data source (e.g., AniDB, TMDB).</param>
    /// <returns>The template URL for the source, or null if not set.</returns>
    [HttpGet("Source/{source}")]
    public ActionResult<string?> GetTemplateUrlForSource([FromRoute] DataSource source)
        => imageManager.GetTemplateUrlForSource(source);

    /// <summary>
    ///   Set the template URL for a specific image source.
    /// </summary>
    /// <param name="source">The data source (e.g., AniDB, TMDB).</param>
    /// <param name="body">The template URL to set.</param>
    /// <returns>No content.</returns>
    [Authorize("admin")]
    [HttpPut("Source/{source}")]
    public ActionResult SetTemplateUrlForSource(
        [FromRoute] DataSource source,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] SetTemplateUrlBody body
    )
    {
        imageManager.SetTemplateUrlForSource(source, body.TemplateUrl);
        return NoContent();
    }

    /// <summary>
    ///   Reset the template URL for a specific image source to its default value.
    /// </summary>
    /// <param name="source">The data source (e.g., AniDB, TMDB).</param>
    /// <returns>No content.</returns>
    [Authorize("admin")]
    [HttpDelete("Source/{source}")]
    public ActionResult ResetTemplateUrlForSource([FromRoute] DataSource source)
    {
        imageManager.SetTemplateUrlForSource(source, null);
        return NoContent();
    }

    #endregion

    #region Images | Query

    /// <summary>
    ///   Get all images with optional filtering and pagination.
    /// </summary>
    /// <param name="imageSource">Filter by image source (e.g., AniDB, TMDB).</param>
    /// <param name="imageType">Filter by image type (e.g., Primary, Backdrop).</param>
    /// <param name="xrefSource">Filter by cross-reference source.</param>
    /// <param name="isEnabled">Filter by enabled state.</param>
    /// <param name="isDesired">Filter by desired state.</param>
    /// <param name="isPreferred">Filter by preferred state.</param>
    /// <param name="isAvailable">Filter by available state.</param>
    /// <param name="isPrimaryAvailable">Filter by primary image availability.</param>
    /// <param name="isPrimaryImage">Filter to only primary images.</param>
    /// <param name="asPrimaryImage">Return the primary image when the image is part of a linked group.</param>
    /// <param name="pageSize">Number of results per page (0-100, default 50).</param>
    /// <param name="page">Page number (default 1).</param>
    /// <returns>A paginated list of images.</returns>
    [HttpGet]
    public ActionResult<ListResult<ImageSlim>> GetAllImages(
        [FromQuery] DataSource? imageSource = null,
        [FromQuery] ImageEntityType? imageType = null,
        [FromQuery] DataSource? xrefSource = null,
        [FromQuery] bool? isEnabled = null,
        [FromQuery] bool? isDesired = null,
        [FromQuery] bool? isPreferred = null,
        [FromQuery] bool? isAvailable = null,
        [FromQuery] bool? isPrimaryAvailable = null,
        [FromQuery] bool? isPrimaryImage = null,
        [FromQuery] bool asPrimaryImage = false,
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
        => imageManager.GetAllImages(new()
        {
            ImageSource = imageSource,
            ImageType = imageType,
            XrefSource = xrefSource,
            IsEnabled = isEnabled,
            IsDesired = isDesired,
            IsPreferred = isPreferred,
            IsAvailable = isAvailable,
            IsPrimaryImage = isPrimaryImage,
            IsPrimaryAvailable = isPrimaryAvailable,
            AsPrimaryImage = asPrimaryImage,
        })
            .ToListResult(image => new ImageSlim(image), page, pageSize);

    /// <summary>
    ///   Get an image by its unique identifier.
    /// </summary>
    /// <param name="imageID">The unique identifier of the image.</param>
    /// <returns>The image if found, otherwise 404.</returns>
    [HttpGet("{imageID:guid}")]
    public ActionResult<ImageSlim> GetImageByID([FromRoute] Guid imageID)
    {
        var image = imageManager.GetImageByID(imageID);
        if (image is null)
            return NotFound(ImageNotFound);
        return new ImageSlim(image, showLinkedIDs: true);
    }

    /// <summary>
    ///   Get an image by its source and remote resource identifier.
    /// </summary>
    /// <param name="source">The data source (e.g., AniDB, TMDB).</param>
    /// <param name="resourceID">The remote resource identifier.</param>
    /// <returns>The image if found, otherwise 404.</returns>
    [HttpGet("Remote/{source}/{*resourceID}")]
    public ActionResult<ImageSlim> GetImageBySourceAndRemoteResourceID(
        [FromRoute] DataSource source,
        [FromRoute] string resourceID
    )
    {
        if (source.IsLocal)
            return ValidationProblem("Cannot look up images for local sources.", nameof(source));
        var image = imageManager.GetImageBySourceAndRemoteResourceID(source, resourceID);
        if (image is null)
            return NotFound(ImageNotFound);
        return new ImageSlim(image, showLinkedIDs: true);
    }

    /// <summary>
    ///   Get the first series associated with an image.
    /// </summary>
    /// <param name="imageID">The unique identifier of the image.</param>
    /// <returns>Series information if found, otherwise 404.</returns>
    [HttpGet("{imageID:guid}/Series")]
    public ActionResult<Image.ImageSeriesInfo> GetFirstSeriesForImage([FromRoute] Guid imageID)
    {
        var image = imageManager.GetImageByID(imageID);
        if (image is null)
            return NotFound(ImageNotFound);
        var series = imageManager.GetFirstSeriesForImage(image);
        if (series is null)
            return NotFound("No series found for the image.");
        return new Image.ImageSeriesInfo(series.ID, series.Title);
    }

    /// <summary>
    ///   Get orphaned images (images with no cross-references) with optional filtering and pagination.
    /// </summary>
    /// <param name="daysOld">Minimum age in days (default 7).</param>
    /// <param name="imageSource">Filter by image source.</param>
    /// <param name="pageSize">Number of results per page (0-100, default 50).</param>
    /// <param name="page">Page number (default 1).</param>
    /// <returns>A paginated list of orphaned images.</returns>
    [HttpGet("Orphaned")]
    public ActionResult<ListResult<ImageSlim>> GetOrphanedImages(
        [FromQuery, Range(0, int.MaxValue)] int daysOld = 7,
        [FromQuery] DataSource? imageSource = null,
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
        => imageManager.GetOrphanedImages(daysOld, imageSource)
            .ToListResult(image => new ImageSlim(image), page, pageSize);

    #endregion

    #region Images | Add/Upload

    /// <summary>
    ///   Add a new image from a remote source.
    /// </summary>
    /// <param name="body">The image data containing source and resource information.</param>
    /// <returns>The created image.</returns>
    [Authorize("admin")]
    [HttpPost("Add")]
    public ActionResult<ImageSlim> AddImage(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] AddImageBody body
    )
    {
        try
        {
            var image = imageManager.AddImage(body.ToImageData());
            return Created($"/api/v3/Image/Management/{image.ID}", new ImageSlim(image));
        }
        catch (MissingImageSourceTemplateUrlException ex)
        {
            return ValidationProblem(ex.Message, nameof(body.Source));
        }
        catch (ImageDataExistsException ex)
        {
            return ValidationProblem(ex.Message, nameof(body.ResourceID));
        }
    }

    /// <summary>
    ///   Upload an image via multipart form data.
    /// </summary>
    /// <param name="file">The image file to upload.</param>
    /// <param name="userSubmitted">Whether the image was submitted by a user (default true).</param>
    /// <returns>The created image.</returns>
    [Authorize("admin")]
    [HttpPost("Upload")]
    [Consumes("multipart/form-data")]
    public ActionResult<ImageSlim> UploadImage(
        IFormFile file,
        [FromForm] bool userSubmitted = true
    )
    {
        if (file is null || file.Length == 0)
            return ValidationProblem("File cannot be empty.", nameof(file));
        try
        {
            using var stream = file.OpenReadStream();
            var image = imageManager.UploadImage(stream, file.ContentType, userSubmitted);
            return Created($"/api/v3/Image/Management/{image.ID}", new ImageSlim(image));
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message);
        }
    }

    /// <summary>
    ///   Upload an image via raw binary stream.
    /// </summary>
    /// <param name="contentType">Optional content type (e.g., image/jpeg, image/png).</param>
    /// <param name="userSubmitted">Whether the image was submitted by a user (default true).</param>
    /// <returns>The created image.</returns>
    [Authorize("admin")]
    [HttpPost("Upload/Raw")]
    [Consumes(ContentTypeHelper.UnknownMimeType, "image/jpeg", "image/png", "image/bmp", "image/gif", "image/tiff", "image/webp")]
    public async Task<ActionResult<ImageSlim>> UploadImageRaw(
        [FromQuery] string? contentType = null,
        [FromQuery] bool userSubmitted = true
    )
    {
        if (HttpContext.Request.ContentLength is null or 0)
            return ValidationProblem("Request body cannot be empty.");
        try
        {
            var image = imageManager.UploadImage(HttpContext.Request.Body, contentType, userSubmitted);
            return Created($"/api/v3/Image/Management/{image.ID}", new ImageSlim(image));
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message);
        }
    }

    #endregion

    #region Images | Update

    /// <summary>
    ///   Update an image with partial data. Only explicitly set properties are updated.
    /// </summary>
    /// <param name="imageID">The unique identifier of the image to update.</param>
    /// <param name="body">The update data containing properties to modify.</param>
    /// <returns>The updated image.</returns>
    [Authorize("admin")]
    [HttpPatch("{imageID:guid}")]
    public ActionResult<ImageSlim> UpdateImage(
        [FromRoute] Guid imageID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] UpdateImageBody body
    )
    {
        var image = imageManager.GetImageByID(imageID);
        if (image is null)
            return NotFound(ImageNotFound);
        var updated = imageManager.UpdateImage(image, body.ToImageUpdateData());
        return new ImageSlim(updated, showLinkedIDs: true);
    }

    /// <summary>
    ///   Enable or disable an image.
    /// </summary>
    /// <param name="imageID">The unique identifier of the image.</param>
    /// <param name="body">The enabled state to set.</param>
    /// <returns>The updated image.</returns>
    [Authorize("admin")]
    [HttpPost("{imageID:guid}/Enabled")]
    public ActionResult<ImageSlim> EnableOrDisableImage(
        [FromRoute] Guid imageID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Image.Input.EnableImageBody body
    )
    {
        var image = imageManager.GetImageByID(imageID);
        if (image is null)
            return NotFound(ImageNotFound);
        var updated = imageManager.EnableImage(image, body.Enabled);
        return new ImageSlim(updated, showLinkedIDs: true);
    }

    /// <summary>
    ///   Set the primary image for a linked image group.
    /// </summary>
    /// <param name="imageID">The unique identifier of the image.</param>
    /// <param name="body">The primary image identifier to set.</param>
    /// <returns>The updated image.</returns>
    [Authorize("admin")]
    [HttpPut("{imageID:guid}/Primary")]
    public ActionResult<ImageSlim> SetPrimaryImage(
        [FromRoute] Guid imageID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] SetPrimaryImageBody body
    )
    {
        var image = imageManager.GetImageByID(imageID);
        if (image is null)
            return NotFound(ImageNotFound);
        IImage? primaryImage = null;
        if (body.PrimaryImageID != imageID)
        {
            primaryImage = imageManager.GetImageByID(body.PrimaryImageID);
            if (primaryImage is null)
                return NotFound("The primary image does not exist.");
        }
        var updated = imageManager.SetPrimaryImage(image, primaryImage);
        return new ImageSlim(updated, showLinkedIDs: true);
    }

    /// <summary>
    ///   Batch update multiple images with the same update data.
    /// </summary>
    /// <param name="body">The batch update request containing image IDs and update data.</param>
    /// <returns>A list of updated images.</returns>
    [Authorize("admin")]
    [HttpPost("Batch/Update")]
    public ActionResult<ListResult<ImageSlim>> BatchUpdateImages(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] BatchUpdateImageBody body
    )
    {
        var updateData = body.Update.ToImageUpdateData();
        var results = new List<ImageSlim>();
        var errors = new List<string>();
        foreach (var imageID in body.ImageIDs)
        {
            var image = imageManager.GetImageByID(imageID);
            if (image is null)
            {
                errors.Add($"Image {imageID} not found.");
                continue;
            }
            var updated = imageManager.UpdateImage(image, updateData);
            results.Add(new ImageSlim(updated, showLinkedIDs: true));
        }
        if (errors.Count > 0 && results.Count == 0)
            return ValidationProblem(string.Join(" ", errors));
        return results.ToListResult();
    }

    #endregion

    #region Images | Download

    /// <summary>
    ///   Check if an image is available at its remote source.
    /// </summary>
    /// <param name="imageID">The unique identifier of the image.</param>
    /// <returns>True if available, false otherwise.</returns>
    [Authorize("admin")]
    [HttpGet("{imageID:guid}/Available")]
    public async Task<ActionResult<bool>> CheckIfAvailableAtRemote([FromRoute] Guid imageID)
    {
        var image = imageManager.GetImageByID(imageID);
        if (image is null)
            return NotFound(ImageNotFound);
        try
        {
            return await imageManager.CheckIfAvailableAtRemote(image);
        }
        catch (Exception ex)
        {
            return InternalError(ex.Message);
        }
    }

    /// <summary>
    ///   Download an image immediately from its remote source.
    /// </summary>
    /// <param name="imageID">The unique identifier of the image.</param>
    /// <param name="force">Force re-download even if already cached (default false).</param>
    /// <returns>The downloaded image.</returns>
    [Authorize("admin")]
    [HttpPost("{imageID:guid}/Download")]
    public async Task<ActionResult<ImageSlim>> DownloadImage(
        [FromRoute] Guid imageID,
        [FromQuery] bool force = false
    )
    {
        var image = imageManager.GetImageByID(imageID);
        if (image is null)
            return NotFound(ImageNotFound);
        try
        {
            await imageManager.DownloadImage(image, force);
            var refreshed = imageManager.GetImageByID(imageID);
            return new ImageSlim(refreshed ?? image, showLinkedIDs: true);
        }
        catch (Exception ex)
        {
            return InternalError(ex.Message);
        }
    }

    /// <summary>
    ///   Schedule an image for background download.
    /// </summary>
    /// <param name="imageID">The unique identifier of the image.</param>
    /// <param name="force">Force re-download even if already cached (default false).</param>
    /// <returns>Success message.</returns>
    [Authorize("admin")]
    [HttpPost("{imageID:guid}/Download/Schedule")]
    public async Task<ActionResult> ScheduleDownloadOfImage(
        [FromRoute] Guid imageID,
        [FromQuery] bool force = false
    )
    {
        var image = imageManager.GetImageByID(imageID);
        if (image is null)
            return NotFound(ImageNotFound);
        await imageManager.ScheduleDownloadOfImage(image, force);
        return Ok("Image download scheduled.");
    }

    #endregion

    #region Images | Purge/Validate

    /// <summary>
    ///   Purge an image from the system, removing all cross-references and local files.
    /// </summary>
    /// <param name="imageID">The unique identifier of the image to purge.</param>
    /// <returns>No content.</returns>
    [Authorize("admin")]
    [HttpDelete("{imageID:guid}")]
    public async Task<ActionResult> PurgeImage([FromRoute] Guid imageID)
    {
        var image = imageManager.GetImageByID(imageID);
        if (image is null)
            return NotFound(ImageNotFound);
        await imageManager.PurgeImage(image);
        return NoContent();
    }

    /// <summary>
    ///   Purge orphaned images (images with no cross-references) older than the specified threshold.
    /// </summary>
    /// <param name="daysOld">Minimum age in days (default 7).</param>
    /// <param name="imageSource">Filter by image source.</param>
    /// <returns>The number of images purged.</returns>
    [Authorize("admin")]
    [HttpDelete("Orphaned")]
    public async Task<ActionResult<int>> PurgeOrphanedImages(
        [FromQuery, Range(0, int.MaxValue)] int daysOld = 7,
        [FromQuery] DataSource? imageSource = null
    )
    {
        var count = await imageManager.PurgeOrphanedImages(daysOld, imageSource);
        return Ok(count);
    }

    /// <summary>
    ///   Validate all images in the system, checking for corruption and missing files.
    /// </summary>
    /// <returns>The number of images validated.</returns>
    [Authorize("admin")]
    [HttpPost("Validate")]
    public async Task<ActionResult<int>> ValidateAllImages()
    {
        var count = await imageManager.ValidateAllImages();
        return Ok(count);
    }

    #endregion

    #region Cross-References | Query

    /// <summary>
    ///   Get all image cross-references with optional filtering and pagination.
    /// </summary>
    /// <param name="imageSource">Filter by image source (e.g., AniDB, TMDB).</param>
    /// <param name="imageType">Filter by image type (e.g., Primary, Backdrop).</param>
    /// <param name="xrefSource">Filter by cross-reference source.</param>
    /// <param name="entitySource">Filter by entity source.</param>
    /// <param name="entityType">Filter by entity type.</param>
    /// <param name="isEnabled">Filter by enabled state.</param>
    /// <param name="isDesired">Filter by desired state.</param>
    /// <param name="isPreferred">Filter by preferred state.</param>
    /// <param name="isAvailable">Filter by available state.</param>
    /// <param name="isPrimaryImage">Filter to only primary images.</param>
    /// <param name="isPrimaryAvailable">Filter by primary image availability.</param>
    /// <param name="includeImage">Include the associated image in the response (default false).</param>
    /// <param name="pageSize">Number of results per page (0-100, default 50).</param>
    /// <param name="page">Page number (default 1).</param>
    /// <returns>A paginated list of cross-references.</returns>
    [HttpGet("CrossReference")]
    public ActionResult<ListResult<ImageCrossReference>> GetAllImageCrossReferences(
        [FromQuery] DataSource? imageSource = null,
        [FromQuery] ImageEntityType? imageType = null,
        [FromQuery] DataSource? xrefSource = null,
        [FromQuery] DataSource? entitySource = null,
        [FromQuery] DataEntityType? entityType = null,
        [FromQuery] bool? isEnabled = null,
        [FromQuery] bool? isDesired = null,
        [FromQuery] bool? isPreferred = null,
        [FromQuery] bool? isAvailable = null,
        [FromQuery] bool? isPrimaryImage = null,
        [FromQuery] bool? isPrimaryAvailable = null,
        [FromQuery] bool includeImage = false,
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
        => imageManager.GetAllImageCrossReferences(new()
        {
            ImageSource = imageSource,
            ImageType = imageType,
            XrefSource = xrefSource,
            EntitySource = entitySource,
            EntityType = entityType,
            IsEnabled = isEnabled,
            IsDesired = isDesired,
            IsPreferred = isPreferred,
            IsAvailable = isAvailable,
            IsPrimaryImage = isPrimaryImage,
            IsPrimaryAvailable = isPrimaryAvailable,
        })
            .ToListResult(xref => new ImageCrossReference(xref, includeImage), page, pageSize);

    /// <summary>
    ///   Get an image cross-reference by its ID.
    /// </summary>
    /// <param name="id">The cross-reference ID.</param>
    /// <param name="includeImage">Include the associated image in the response (default false).</param>
    /// <returns>The cross-reference if found, otherwise 404.</returns>
    [HttpGet("CrossReference/{id:int}")]
    public ActionResult<ImageCrossReference> GetImageCrossReferenceByID(
        [FromRoute, Range(1, int.MaxValue)] int id,
        [FromQuery] bool includeImage = false
    )
    {
        var xref = imageManager.GetImageCrossReferenceByID(id);
        if (xref is null)
            return NotFound(CrossReferenceNotFound);
        return new ImageCrossReference(xref, includeImage);
    }

    /// <summary>
    ///   Get a random image cross-reference matching the specified filters.
    /// </summary>
    /// <param name="imageSource">Filter by image source (required).</param>
    /// <param name="imageType">Filter by image type (required).</param>
    /// <param name="xrefSource">Filter by cross-reference source.</param>
    /// <param name="entitySource">Filter by entity source.</param>
    /// <param name="entityType">Filter by entity type.</param>
    /// <param name="isEnabled">Filter by enabled state.</param>
    /// <param name="isDesired">Filter by desired state.</param>
    /// <param name="isPreferred">Filter by preferred state.</param>
    /// <param name="isAvailable">Filter by available state.</param>
    /// <param name="isPrimaryImage">Filter to only primary images.</param>
    /// <param name="isPrimaryAvailable">Filter by primary image availability.</param>
    /// <param name="includeImage">Include the associated image in the response (default false).</param>
    /// <returns>A random cross-reference if found, otherwise 404.</returns>
    [HttpGet("CrossReference/Random")]
    public ActionResult<ImageCrossReference> GetRandomImageCrossReference(
        [FromQuery, Required] DataSource imageSource,
        [FromQuery, Required] ImageEntityType imageType,
        [FromQuery] DataSource? xrefSource = null,
        [FromQuery] DataSource? entitySource = null,
        [FromQuery] DataEntityType? entityType = null,
        [FromQuery] bool? isEnabled = null,
        [FromQuery] bool? isDesired = null,
        [FromQuery] bool? isPreferred = null,
        [FromQuery] bool? isAvailable = null,
        [FromQuery] bool? isPrimaryImage = null,
        [FromQuery] bool? isPrimaryAvailable = null,
        [FromQuery] bool includeImage = false
    )
    {
        var xref = imageManager.GetRandomImageCrossReference(imageSource, imageType, new()
        {
            XrefSource = xrefSource,
            EntitySource = entitySource,
            EntityType = entityType,
            IsEnabled = isEnabled,
            IsDesired = isDesired,
            IsPreferred = isPreferred,
            IsAvailable = isAvailable,
            IsPrimaryImage = isPrimaryImage,
            IsPrimaryAvailable = isPrimaryAvailable,
        });
        if (xref is null)
            return NotFound("No cross-reference found matching the criteria.");
        return new ImageCrossReference(xref, includeImage);
    }

    /// <summary>
    ///   Get all image cross-references for a specific entity with optional filtering and pagination.
    /// </summary>
    /// <param name="entitySource">The entity source (e.g., Shoko, AniDB, TMDB).</param>
    /// <param name="entityType">The entity type (e.g., Series, Episode).</param>
    /// <param name="entityID">The entity ID (string representation).</param>
    /// <param name="imageSource">Filter by image source.</param>
    /// <param name="imageType">Filter by image type.</param>
    /// <param name="xrefSource">Filter by cross-reference source.</param>
    /// <param name="isEnabled">Filter by enabled state.</param>
    /// <param name="isDesired">Filter by desired state.</param>
    /// <param name="isPreferred">Filter by preferred state.</param>
    /// <param name="isAvailable">Filter by available state.</param>
    /// <param name="isPrimaryImage">Filter to only primary images.</param>
    /// <param name="isPrimaryAvailable">Filter by primary image availability.</param>
    /// <param name="linkedEntityImages">Also include cross-references for images from other entities linked to the entity. Defaults to null (let the service decide based on the entity).</param>
    /// <param name="includeImage">Include the associated image in the response (default false).</param>
    /// <param name="pageSize">Number of results per page (0-100, default 50).</param>
    /// <param name="page">Page number (default 1).</param>
    /// <returns>A paginated list of cross-references for the entity.</returns>
    [HttpGet("CrossReference/Entity/{entitySource}/{entityType}/{*entityID}")]
    public ActionResult<ListResult<ImageCrossReference>> GetImageCrossReferencesForEntity(
        [FromRoute] DataSource entitySource,
        [FromRoute] DataEntityType entityType,
        [FromRoute] string entityID,
        [FromQuery] DataSource? imageSource = null,
        [FromQuery] ImageEntityType? imageType = null,
        [FromQuery] DataSource? xrefSource = null,
        [FromQuery] bool? isEnabled = null,
        [FromQuery] bool? isDesired = null,
        [FromQuery] bool? isPreferred = null,
        [FromQuery] bool? isAvailable = null,
        [FromQuery] bool? isPrimaryImage = null,
        [FromQuery] bool? isPrimaryAvailable = null,
        [FromQuery] bool? linkedEntityImages = null,
        [FromQuery] bool includeImage = false,
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var entity = imageManager.GetEntityForImage(entitySource, entityType, entityID);
        if (entity is null)
            return NotFound(EntityNotFound);
        return imageManager.GetImageCrossReferencesForEntity(entity, new()
        {
            ImageSource = imageSource,
            ImageType = imageType,
            XrefSource = xrefSource,
            IsEnabled = isEnabled,
            IsDesired = isDesired,
            IsPreferred = isPreferred,
            IsAvailable = isAvailable,
            IsPrimaryImage = isPrimaryImage,
            IsPrimaryAvailable = isPrimaryAvailable,
            LinkedEntityImages = linkedEntityImages,
        })
            .ToListResult(xref => new ImageCrossReference(xref, includeImage), page, pageSize);
    }

    #endregion

    #region Cross-References | Mutations

    /// <summary>
    ///   Add a new image cross-reference for a specific entity.
    /// </summary>
    /// <param name="entitySource">The entity source (e.g., Shoko, AniDB, TMDB).</param>
    /// <param name="entityType">The entity type (e.g., Series, Episode).</param>
    /// <param name="entityID">The entity ID (string representation).</param>
    /// <param name="body">The cross-reference data to add.</param>
    /// <param name="includeImage">Include the associated image in the response (default false).</param>
    /// <returns>The created cross-reference.</returns>
    [Authorize("admin")]
    [HttpPost("CrossReference/Entity/{entitySource}/{entityType}/{*entityID}")]
    public ActionResult<ImageCrossReference> AddImageCrossReference(
        [FromRoute] DataSource entitySource,
        [FromRoute] DataEntityType entityType,
        [FromRoute] string entityID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] AddImageCrossReferenceBody body,
        [FromQuery] bool includeImage = false
    )
    {
        var entity = imageManager.GetEntityForImage(entitySource, entityType, entityID);
        if (entity is null)
            return NotFound(EntityNotFound);
        var image = imageManager.GetImageByID(body.ImageID);
        if (image is null)
            return NotFound(ImageNotFound);
        try
        {
            var xref = imageManager.AddImageCrossReference(entity, image, body.ToImageCrossReferenceData());
            return Created($"/api/v3/Image/Management/CrossReference/{xref.ID}", new ImageCrossReference(xref, includeImage));
        }
        catch (ImageCrossReferenceExistsException ex)
        {
            return ValidationProblem(ex.Message);
        }
    }

    /// <summary>
    ///   Set an image cross-reference as the preferred image for its entity and type.
    /// </summary>
    /// <param name="id">The cross-reference ID.</param>
    /// <param name="includeImage">Include the associated image in the response (default false).</param>
    /// <returns>The updated cross-reference.</returns>
    [Authorize("admin")]
    [HttpPut("CrossReference/{id:int}/Preferred")]
    public ActionResult<ImageCrossReference> SetPreferredImageForEntity(
        [FromRoute, Range(1, int.MaxValue)] int id,
        [FromQuery] bool includeImage = false
    )
    {
        var xref = imageManager.GetImageCrossReferenceByID(id);
        if (xref is null)
            return NotFound(CrossReferenceNotFound);
        var updated = imageManager.SetPreferredImageForEntity(xref);
        return new ImageCrossReference(updated, includeImage);
    }

    /// <summary>
    ///   Unset an image cross-reference as the preferred image for its entity and type.
    /// </summary>
    /// <param name="id">The cross-reference ID.</param>
    /// <returns>No content.</returns>
    [Authorize("admin")]
    [HttpDelete("CrossReference/{id:int}/Preferred")]
    public ActionResult UnsetPreferredImageForEntity([FromRoute, Range(1, int.MaxValue)] int id)
    {
        var xref = imageManager.GetImageCrossReferenceByID(id);
        if (xref is null)
            return NotFound(CrossReferenceNotFound);
        if (!imageManager.UnsetPreferredImageForEntity(xref))
            return InternalError("Failed to unset preferred image.");
        return NoContent();
    }

    /// <summary>
    ///   Unset all preferred images for a specific entity across all image types.
    /// </summary>
    /// <param name="entitySource">The entity source (e.g., Shoko, AniDB, TMDB).</param>
    /// <param name="entityType">The entity type (e.g., Series, Episode).</param>
    /// <param name="entityID">The entity ID (string representation).</param>
    /// <returns>No content.</returns>
    [Authorize("admin")]
    [HttpDelete("CrossReference/Preferred/Entity/{entitySource}/{entityType}/{*entityID}")]
    public ActionResult UnsetAllPreferredImagesForEntity(
        [FromRoute] DataSource entitySource,
        [FromRoute] DataEntityType entityType,
        [FromRoute] string entityID
    )
    {
        var entity = imageManager.GetEntityForImage(entitySource, entityType, entityID);
        if (entity is null)
            return NotFound(EntityNotFound);
        if (!imageManager.UnsetAllPreferredImagesForEntity(entity))
            return InternalError("Failed to unset all preferred images.");
        return NoContent();
    }

    /// <summary>
    ///   Update an image cross-reference with partial data. Only explicitly set properties are updated.
    /// </summary>
    /// <param name="id">The cross-reference ID.</param>
    /// <param name="body">The update data containing properties to modify.</param>
    /// <param name="includeImage">Include the associated image in the response (default false).</param>
    /// <returns>The updated cross-reference.</returns>
    [Authorize("admin")]
    [HttpPatch("CrossReference/{id:int}")]
    public ActionResult<ImageCrossReference> UpdateImageCrossReference(
        [FromRoute, Range(1, int.MaxValue)] int id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] UpdateImageCrossReferenceBody body,
        [FromQuery] bool includeImage = false
    )
    {
        var xref = imageManager.GetImageCrossReferenceByID(id);
        if (xref is null)
            return NotFound(CrossReferenceNotFound);
        var updated = imageManager.UpdateImageCrossReference(xref, body.ToImageCrossReferenceUpdateData());
        return new ImageCrossReference(updated, includeImage);
    }

    /// <summary>
    ///   Remove an image cross-reference.
    /// </summary>
    /// <param name="id">The cross-reference ID.</param>
    /// <returns>No content.</returns>
    [Authorize("admin")]
    [HttpDelete("CrossReference/{id:int}")]
    public ActionResult RemoveImageCrossReference([FromRoute, Range(1, int.MaxValue)] int id)
    {
        var xref = imageManager.GetImageCrossReferenceByID(id);
        if (xref is null)
            return NotFound(CrossReferenceNotFound);
        if (!imageManager.RemoveImageCrossReference(xref))
            return InternalError("Failed to remove cross-reference.");
        return NoContent();
    }

    /// <summary>
    ///   Batch update multiple image cross-references with the same update data.
    /// </summary>
    /// <param name="body">The batch update request containing cross-reference IDs and update data.</param>
    /// <param name="includeImage">Include the associated image in the response (default false).</param>
    /// <returns>A list of updated cross-references.</returns>
    [Authorize("admin")]
    [HttpPost("CrossReference/Batch/Update")]
    public ActionResult<ListResult<ImageCrossReference>> BatchUpdateImageCrossReferences(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] BatchUpdateImageCrossReferenceBody body,
        [FromQuery] bool includeImage = false
    )
    {
        var updateData = body.Update.ToImageCrossReferenceUpdateData();
        var results = new List<ImageCrossReference>();
        var errors = new List<string>();
        foreach (var xrefID in body.CrossReferenceIDs)
        {
            var xref = imageManager.GetImageCrossReferenceByID(xrefID);
            if (xref is null)
            {
                errors.Add($"Cross-reference {xrefID} not found.");
                continue;
            }
            var updated = imageManager.UpdateImageCrossReference(xref, updateData);
            results.Add(new ImageCrossReference(updated, includeImage));
        }
        if (errors.Count > 0 && results.Count == 0)
            return ValidationProblem(string.Join(" ", errors));
        return results.ToListResult();
    }

    #endregion
}
