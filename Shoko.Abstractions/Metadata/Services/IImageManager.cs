using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Image.Exceptions;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Utilities;

namespace Shoko.Abstractions.Metadata.Services;

/// <summary>
///   Responsible for managing images across all providers and sources, and the
///   single point of entry for all image-related operations such as image
///   retrieval, creation, updates, downloading, and cross-reference management.
/// </summary>
public interface IImageManager
{
    #region Image Sources

    /// <summary>
    ///   Gets the currently registered template URLs across all the available
    ///   image sources.
    /// </summary>
    /// <returns>
    ///   A dictionary mapping image source to template URL.
    /// </returns>
    IReadOnlyDictionary<DataSource, string?> GetTemplateUrls();

    /// <summary>
    ///   Gets the template URL for the image source, if set and available.
    ///   Replace <c>{0}</c> with the <see cref="IImage.ResourceID"/> before
    ///   use.
    /// </summary>
    /// <param name="imageSource">
    ///   The image source.
    /// </param>
    /// <returns>
    ///   The template url if set and available, otherwise <c>null</c>.
    /// </returns>
    string? GetTemplateUrlForSource(DataSource imageSource);

    /// <summary>
    ///   Sets the template URL for an image source. The template must be a
    ///   valid <c>http://</c> or <c>https://</c> URL by itself and contain
    ///   <c>{0}</c>.
    /// </summary>
    /// <param name="imageSource">
    ///   The image source.
    /// </param>
    /// <param name="templateUrl">
    ///   The template URL to set. If this is <c>null</c>, the template URL will
    ///   be removed. Must be a valid URL starting with <c>http://</c> or
    ///   <c>https://</c> and contain <c>{0}</c> to be replaced.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   Thrown when the URL does not use the <c>http://</c> or <c>https://</c>
    ///   protocol, or it does not include the <c>{0}</c> template
    ///   substitution target.
    /// </exception> 
    /// <exception cref="InvalidOperationException">
    ///   Thrown when attempting to set or unset a URL for a data source which
    ///   is unsupported, such as <see cref="DataSource.User"/>,
    ///   <see cref="DataSource.None"/> or <see cref="DataSource.Shoko"/>.
    /// </exception>
    void SetTemplateUrlForSource(DataSource imageSource, string? templateUrl);

    #endregion

    #region Image Cross Reference Resolvers

    /// <summary>
    ///   Adds the image cross-reference resolvers to the service.
    /// </summary>
    /// <remarks>
    ///   This should be called once per instance of the service, and will be
    ///   called during start-up. Calling it multiple times will have no effect.
    /// </remarks>
    /// <param name="resolvers">
    ///   The resolvers to add.
    /// </param>
    void AddParts(IEnumerable<IImageCrossReferenceResolver> resolvers);

    /// <summary>
    ///   Gets a read-only list of the image cross-reference resolvers
    ///   registered with the service.
    /// </summary>
    IReadOnlyList<IImageCrossReferenceResolver> ImageCrossReferenceResolvers { get; }

    #endregion

    #region Images

    /// <summary>
    ///   Dispatched when metadata about a new image is added to the database.
    ///   The image may not necessarily be available yet.
    /// </summary>
    event EventHandler<ImageEventArgs>? ImageAdded;

    /// <summary>
    ///   Dispatched when metadata for an image is updated in the database.
    /// </summary>
    event EventHandler<ImageEventArgs>? ImageUpdated;

    /// <summary>
    ///   Dispatched when an image is successfully downloaded to local storage.
    /// </summary>
    event EventHandler<ImageEventArgs>? ImageDownloaded;

    /// <summary>
    ///   Dispatched when an image is removed from the database.
    /// </summary>
    event EventHandler<ImageEventArgs>? ImageRemoved;

    /// <summary>
    ///   Get all images, optionally filtered by source and enabled state.
    /// </summary>
    /// <param name="imageSource">
    ///   Optional. Filter by image source (e.g. AniDB, TMDB, AniList, User,
    ///   etc.).
    /// </param>
    /// <param name="imageType">
    ///   Optional. Filter by image type (e.g. Primary, Backdrop, Banner, etc.).
    /// </param>
    /// <param name="xrefSource">
    ///   Optional. Filter by cross-reference source (e.g. AniDB, TMDB, AniList,
    ///   User, etc.).
    /// </param>
    /// <param name="isEnabled">
    ///   Optional. Filter by enabled state. Pass <c>true</c> to get only
    ///   enabled, <c>false</c> to get only disabled, or <c>null</c> to get
    ///   both. Defaults to <c>null</c>.
    /// </param>
    /// <param name="isDesired">
    ///   Optional. Filter by desired state. Pass <c>true</c> to get only
    ///   desired, <c>false</c> to get only undesired, or <c>null</c> to get
    ///   both. Defaults to <c>null</c>.
    /// </param>
    /// <param name="primaryImage">
    ///   Optional. Set to <c>true</c> to retrieve the primary image if the
    ///   image is part of a linked image list.
    /// </param>
    /// <returns>
    ///   An enumerable of all images matching the filter criteria.
    /// </returns>
    IEnumerable<IImage> GetAllImages(
        DataSource? imageSource = null,
        ImageEntityType? imageType = null,
        DataSource? xrefSource = null,
        bool? isEnabled = null,
        bool? isDesired = null,
        bool? primaryImage = null
    );

    /// <summary>
    ///   Get all images associated with the specified entity. If you need
    ///   additional metadata about the relationship between the entity and the
    ///   images then you should use <seealso cref="GetImagesForEntity"/>
    ///   instead.
    /// </summary>
    /// <param name="entity">
    ///   The entity to get images for.
    /// </param>
    /// <param name="imageSource">
    ///   Optional. Filter by image source (e.g. AniDB, TMDB, AniList, User,
    ///   etc.).
    /// </param>
    /// <param name="imageType">
    ///   Optional. Filter by image type (e.g. Primary, Backdrop, Banner, etc.).
    /// </param>
    /// <param name="xrefSource">
    ///   Optional. Filter by cross-reference source (e.g. AniDB, TMDB, AniList,
    ///   User, etc.).
    /// </param>
    /// <param name="isEnabled">
    ///   Optional. Filter by enabled state. Pass <c>true</c> to get only
    ///   enabled, <c>false</c> to get only disabled, or <c>null</c> to get
    ///   both. Defaults to <c>null</c>.
    /// </param>
    /// <param name="isDesired">
    ///   Optional. Filter by desired state. Pass <c>true</c> to get only
    ///   desired, <c>false</c> to get only undesired, or <c>null</c> to get
    ///   both. Defaults to <c>null</c>.
    /// </param>
    /// <param name="primaryImage">
    ///   Optional. Set to <c>true</c> to retrieve the primary image if the
    ///   image is part of a linked image list.
    /// </param>
    /// <param name="linkedEntityImages">
    ///   Optional. Set to <c>false</c> to only retrieve the entity's own
    ///   images. Set to <c>true</c> to also retrieve images from other entities
    ///   linked to the entity. Set to <c>null</c> to let the service decide
    ///   based on the entity. Defaults to <c>null</c>.
    /// </param>
    /// <returns>
    ///   A readonly list of images associated with the entity, filtered by the
    ///   provided criteria.
    /// </returns>
    IReadOnlyList<IImage> GetImagesForEntity(
        IWithImages entity,
        DataSource? imageSource = null,
        ImageEntityType? imageType = null,
        DataSource? xrefSource = null,
        bool? isEnabled = null,
        bool? isDesired = null,
        bool primaryImage = false,
        bool? linkedEntityImages = null
    );

    /// <summary>
    ///   Get an image by its universally/globally unique identifier
    ///   (UUID/GUID).
    /// </summary>
    /// <param name="imageID">
    ///   The universally/globally unique identifier (UUID/GUID) of the image.
    /// </param>
    /// <param name="primaryImage">
    ///   Optional. Gets the primary image 
    /// </param>
    /// <returns>
    ///   The image if found, otherwise <c>null</c>.
    /// </returns>
    IImage? GetImageByID(Guid imageID, bool primaryImage = false);

    /// <summary>
    ///   Get an image by it's local identifier for legacy compatibility.
    /// </summary>
    /// <param name="localImageID">
    ///   The local image identifier of the image.
    /// </param>
    /// <param name="primaryImage">
    ///   Optional. Set to <c>true</c> to retrieve the primary image if the
    ///   image is part of a linked image list.
    /// </param>
    /// <returns>
    ///   The image if found, otherwise <c>null</c>.
    /// </returns>
    [Obsolete("Use the Universally Unique Identifier instead.")]
    IImage? GetImageByID(int localImageID, bool primaryImage = false);

    /// <summary>
    ///   Get an image by its source and remote resource identifier. This is
    ///   useful for safely checking if an image already exists before
    ///   attempting to add a new one for the provider.
    /// </summary>
    /// <param name="source">
    ///   The image source (e.g. AniDB, TMDB, AniList, User, etc.).
    /// </param>
    /// <param name="resourceID">
    ///   The remote resource identifier relative to the source.
    /// </param>
    /// <param name="primaryImage">
    ///   Optional. Set to <c>true</c> to retrieve the primary image if the
    ///   image is part of a linked image list.
    /// </param>
    /// <returns>
    ///   The image if found, otherwise <c>null</c>.
    /// </returns>
    IImage? GetImageBySourceAndRemoteResourceID(DataSource source, string resourceID, bool primaryImage = false);

    /// <summary>
    ///   Get the first available shoko series for an image, if any. This is
    ///   useful for determining which series an image belongs to when browsing
    ///   images in the UI. Returns the series with the earliest release date if
    ///   the image is linked to multiple series.
    /// </summary>
    /// <param name="image">
    ///   The image to attempt to find a shoko series for.
    /// </param>
    /// <returns>
    ///   The first available series, or <c>null</c> if not linked to any
    ///   series.
    /// </returns>
    IShokoSeries? GetFirstSeriesForImage(IImage image);

    #region Images | Add

    /// <summary>
    ///   The list of allowed MIME types for images.
    /// </summary>
    IReadOnlyList<string> AllowedMimeTypes { get; }

    /// <summary>
    ///   Add a new image from provider data.
    /// </summary>
    /// <param name="imageData">
    ///   The image data containing metadata from the provider.
    /// </param>
    /// <exception cref="MissingImageSourceTemplateUrlException">
    ///   Thrown when attempting to add an image for a source that has no url
    ///   template set.
    /// </exception>
    /// <exception cref="ImageDataExistsException">
    ///   Thrown when attempting to add image data for a resource which already
    ///   exists.
    /// </exception>
    /// <exception cref="UnsupportedImageTypeException">
    ///   Thrown if the resource ID contains a file extension that maps to a MIME
    ///   type not in the allowed image types list.
    /// </exception>
    /// <returns>
    ///   The newly created image.
    /// </returns>
    IImage AddImage(ImageData imageData);

    /// <summary>
    ///   Upload a new user submitted image from a stream.
    /// </summary>
    /// <param name="imageStream">
    ///   A stream containing the image data. May be data URL encoded w/content
    ///   type embedded.
    /// </param>
    /// <param name="contentType">
    ///   Optional. The MIME type of the image (e.g., <c>"image/jpeg"</c>,
    ///   <c>"image/png"</c>, etc.). Used to cross-reference against the
    ///   detected content type of the image stream.
    /// </param>
    /// <param name="userSubmitted">
    ///   Optional. Whether the image was submitted by the user. Defaults to
    ///   <c>true</c>. Set to <c>false</c> if the image is a locally generated
    ///   image, e.g. an extracted thumbnail, etc.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   Thrown when the image stream is empty, the content type is
    ///   invalid, or the image data is not a valid image.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the image stream is <c>null</c>.
    /// </exception>
    /// <exception cref="UnsupportedImageTypeException">
    ///   Thrown when the content type or detected image format is not in the
    ///   allowed image types list.
    /// </exception>
    /// <returns>
    ///   The newly created image.
    /// </returns>
    IImage UploadImage(Stream imageStream, string? contentType = null, bool userSubmitted = true);

    /// <summary>
    ///   Upload a new user submitted image from a byte array.
    /// </summary>
    /// <param name="imageByteArray">
    ///   The image data as a byte array. May be data URL encoded w/content type
    ///   embedded.
    /// </param>
    /// <param name="contentType">
    ///   Optional. The MIME type of the image (e.g., <c>"image/jpeg"</c>,
    ///   <c>"image/png"</c>, etc.). Used to cross-reference against the
    ///   detected content type of the image stream.
    /// </param>
    /// <param name="userSubmitted">
    ///   Optional. Whether the image was submitted by the user. Defaults to
    ///   <c>true</c>. Set to <c>false</c> if the image is a locally generated
    ///   image, e.g. an extracted thumbnail, etc.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   Thrown when the image byte array is empty, the content type is
    ///   invalid, or the image data is not a valid image.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the image byte array is <c>null</c>.
    /// </exception>
    /// <exception cref="UnsupportedImageTypeException">
    ///   Thrown when the content type or detected image format is not in the
    ///   allowed image types list.
    /// </exception>
    /// <returns>
    ///   The newly created image.
    /// </returns>
    IImage UploadImage(byte[] imageByteArray, string? contentType = null, bool userSubmitted = true);

    #endregion

    #region Images | Update

    /// <summary>
    ///   Enable or disable an image. This is a convenience method that wraps
    ///   <seealso cref="UpdateImage"/> to just set the enabled state.
    /// </summary>
    /// <param name="image">
    ///   The image to enable or disable.
    /// </param>
    /// <param name="isEnabled">
    ///   Whether the image should be enabled.
    /// </param>
    /// <returns>
    ///   The updated image.
    /// </returns>
    IImage EnableImage(IImage image, bool isEnabled);

    /// <summary>
    ///   Sets the primary image linked to the image. This is a convenience
    ///   method that wraps <seealso cref="UpdateImage"/> to just set the
    ///   primary image.
    /// </summary>
    /// <param name="image">
    ///    The image to set as the primary image for the image. Set to
    ///   <c>null</c> or the same image to unset the primary image.
    /// </param>
    /// <param name="primaryImage"></param>
    /// <returns></returns>
    IImage SetPrimaryImage(IImage image, IImage? primaryImage);

    /// <summary>
    ///   Update an image with new metadata. This allows partial updates where
    ///   only specified fields are modified based on which properties are set
    ///   in the image update data.
    /// </summary>
    /// <param name="image">
    ///   The image to update.
    /// </param>
    /// <param name="imageUpdateData">
    ///   The update data containing the fields to update.
    /// </param>
    /// <returns>
    ///   The updated image.
    /// </returns>
    IImage UpdateImage(IImage image, ImageUpdateData imageUpdateData);

    #endregion

    #region Image | Download

    /// <summary>
    ///   Checks if an image is available at the remote provider.
    /// </summary>
    /// <param name="image">
    ///   The image to check.
    /// </param>
    /// <exception cref="HttpRequestException">
    ///   Thrown when an error occurs while attempting to check if the image is
    ///   available at the remote provider.
    /// </exception>
    /// <returns>
    ///   <c>true</c> if the image is available at the remote provider,
    ///   <c>false</c> otherwise.
    /// </returns>
    Task<bool> CheckIfAvailableAtRemote(IImage image);

    /// <summary>
    ///   Download an image immediately. This will download the image from the
    ///   remote provider to local storage.
    /// </summary>
    /// <param name="image">
    ///   The image to download.
    /// </param>
    /// <param name="force">
    ///   Optional. If set to <c>true</c>, will re-download even if the image
    ///   already exists locally.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the image was downloaded, <c>false</c> if it was
    ///   already available.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when the image is not available at the remote provider.
    /// </exception>
    /// <exception cref="HttpRequestException">
    ///   Thrown when an error occurs while downloading the image from the provider.
    /// </exception>
    /// <exception cref="UnsupportedImageTypeException">
    ///   Thrown when the downloaded bytes are not a recognized image format.
    /// </exception>
    /// <exception cref="IOException">
    ///   Thrown when an error occurs while writing the image file to disk.
    /// </exception>
    Task<bool> DownloadImage(IImage image, bool force = false);

    /// <summary>
    ///   Schedule the download of a single image to be performed by a
    ///   background job.
    /// </summary>
    /// <param name="image">
    ///   The image to schedule for download.
    /// </param>
    /// <param name="force">
    ///   Optional. If set to <c>true</c>, will re-download even if the image
    ///   already exists locally.
    /// </param>
    Task ScheduleDownloadOfImage(IImage image, bool force = false);

    /// <summary>
    ///   Schedule download of all desired images linked to an entity matching
    ///   the specified criteria.If <paramref name="force"/> is not set, it
    ///   will only images not available locally.
    /// </summary>
    /// <param name="entity">
    ///   The entity to schedule auto-downloads for.
    /// </param>
    /// <param name="imageSource">
    ///   Optional. Filter to a specific image source. If set to <c>null</c>,
    ///   downloads all available images regardless of image source.
    /// </param>
    /// <param name="imageType">
    ///   Optional. Filter to a specific image type. If set to <c>null</c>,
    ///   downloads all available images regardless of image type.
    /// </param>
    /// <param name="xrefSource">
    ///   Optional. Filter to a specific cross-reference source. If set to
    ///   <c>null</c>, downloads all available images regardless of
    ///   cross-reference source.
    /// </param>
    /// <param name="force">
    ///   Optional. If set to <c>true</c>, will re-download even if the images
    ///   already exists locally.
    /// </param>
    Task ScheduleAutoDownloadsForEntity(
        IWithImages entity,
        DataSource? imageSource = null,
        ImageEntityType? imageType = null,
        DataSource? xrefSource = null,
        bool force = false
    );

    /// <summary>
    ///   Schedule download of all desired images across all sources matching
    ///   the specified criteria. If <paramref name="force"/> is not set, it
    ///   will only images not available locally.
    /// </summary>
    /// <param name="imageSource">
    ///   Optional. Filter to a specific image source. If set to <c>null</c>,
    ///   downloads all available images regardless of image source.
    /// </param>
    /// <param name="imageType">
    ///   Optional. Filter to a specific image type. If set to <c>null</c>,
    ///   downloads all available images regardless of image type.
    /// </param>
    /// <param name="xrefSource">
    ///   Optional. Filter to a specific cross-reference source. If set to
    ///   <c>null</c>, downloads all available images regardless of
    ///   cross-reference source.
    /// </param>
    /// <param name="force">
    ///   Optional. If set to <c>true</c>, will re-download even if the images
    ///   already exists locally.
    /// </param>
    Task ScheduleAllAutoDownloads(
        DataSource? imageSource = null,
        ImageEntityType? imageType = null,
        DataSource? xrefSource = null,
        bool force = false
    );

    #endregion

    #region Image | Purge

    /// <summary>
    ///   Get all orphaned images — images that have no cross-references and
    ///   haven't been updated in the specified number of days.
    /// </summary>
    /// <param name="daysOld">
    ///   Optional. Number of days an image must be unused before being
    ///   considered orphaned. Set to <c>0</c> to include all images regardless
    ///   of age. Defaults to <c>7</c> days.
    /// </param>
    /// <param name="imageSource">
    ///   Optional. Filter to a specific image source. If set to <c>null</c>,
    ///   returns all orphaned images regardless of image source.
    /// </param>
    /// <returns>
    ///   An enumerable of orphaned images matching the filter criteria.
    /// </returns>
    IEnumerable<IImage> GetOrphanedImages(int daysOld = 7, DataSource? imageSource = null);

    /// <summary>
    ///   Attempts to purge an image from disk if it is no longer linked to any
    ///   cross-references.
    /// </summary>
    /// <param name="image">
    ///   The image to attempt to purge.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the image was purged, <c>false</c> if it could not be
    ///   purged.
    /// </returns>
    Task<bool> PurgeImage(IImage image);

    /// <summary>
    ///   Schedule a background job to attempt to purge an image from disk if it
    ///   is no longer linked to any cross-references.
    /// </summary>
    /// <param name="image">
    ///   The image to attempt to purge.
    /// </param>
    Task SchedulePurgeOfImage(IImage image);

    /// <summary>
    ///   Check for broken cross-references to remove and purge orphaned images
    ///   that have no cross-references and haven't been updated in the
    ///   specified number of days.
    /// </summary>
    /// <param name="daysOld">
    ///   Optional. Number of days an image must be unused before being purged.
    ///   Set to <c>0</c> to purge all images immediately. Defaults to <c>7</c>
    ///   days.
    /// </param>
    /// <param name="imageSource">
    ///   Optional. Filter to a specific image source. If set to <c>null</c>,
    ///   purges all available images regardless of image source.
    /// </param>
    /// <returns>
    ///   The number of images that were purged.
    /// </returns>
    Task<int> PurgeOrphanedImages(int daysOld = 7, DataSource? imageSource = null);

    /// <summary>
    ///   Schedule a background job to check for broken cross-references and
    ///   purge orphaned images.
    /// </summary>
    /// <param name="daysOld">
    ///   Optional. Number of days an image must be unused before being purged.
    ///   Set to <c>0</c> to purge all images immediately. Defaults to <c>7</c>
    ///   days.
    /// </param>
    /// <param name="imageSource">
    ///   Optional. Filter to a specific image source. If set to <c>null</c>,
    ///   purges all available images regardless of image source.
    /// </param>
    Task SchedulePurgeOfOrphanedImages(int daysOld = 7, DataSource? imageSource = null);

    /// <summary>
    ///   Validate local image cache integrity. Invalid images that are both
    ///   enabled and desired are scheduled for forced re-download. Invalid
    ///   images that are disabled or undesired are cleaned locally without
    ///   re-download.
    /// </summary>
    /// <returns>
    ///   The number of images queued for forced re-download.
    /// </returns>
    Task<int> ValidateAllImages();

    /// <summary>
    ///   Schedule a background validation pass for all images in the cache.
    /// </summary>
    /// <param name="prioritize">
    ///   Optional. Set to <c>true</c> to enqueue with high priority.
    /// </param>
    Task ScheduleValidateAllImages(bool prioritize = true);

    #endregion

    #endregion

    #region Cross References

    /// <summary>
    ///   Dispatched when a new image cross-reference is added.
    /// </summary>
    event EventHandler<ImageCrossReferenceEventArgs>? ImageCrossReferenceAdded;

    /// <summary>
    ///   Dispatched when an existing image cross-reference is updated.
    /// </summary>
    event EventHandler<ImageCrossReferenceEventArgs>? ImageCrossReferenceUpdated;

    /// <summary>
    ///   Dispatched when an image cross-reference is removed.
    /// </summary>
    event EventHandler<ImageCrossReferenceEventArgs>? ImageCrossReferenceRemoved;

    /// <summary>
    ///   Get all image cross-references, optionally filtered by various
    ///   criteria.
    /// </summary>
    /// <param name="imageSource">
    ///   Optional. Filter for image source (e.g. AniDB, TMDB, AniList, User,
    ///   etc.).
    /// </param>
    /// <param name="imageType">
    ///   Optional. Filter for image type (e.g. Group, Series, Episode, etc.).
    /// </param>
    /// <param name="xrefSource">
    ///   Optional. Filter for cross-reference source (e.g. AniDB, TMDB,
    ///   AniList, User, etc.).
    /// </param>
    /// <param name="entitySource">
    ///   Optional. Filter for entity source (e.g. AniDB, TMDB, AniList, User,
    ///   etc.).
    /// </param>
    /// <param name="entityType">
    ///   Optional. Filter for entity type (e.g. Group, Series, Episode, etc.).
    /// </param>
    /// <param name="isEnabled">
    ///   Optional. Filter by enabled state. Pass <c>true</c> to get only
    ///   enabled, <c>false</c> to get only disabled, or <c>null</c> to get
    ///   both. Defaults to <c>null</c>.
    /// </param>
    /// <param name="isDesired">
    ///   Optional. Filter by desired state. Pass <c>true</c> to get only
    ///   desired, <c>false</c> to get only undesired, or <c>null</c> to get
    ///   both. Defaults to <c>null</c>.
    /// </param>
    /// <param name="isAvailable">
    ///   Optional. Filter by available state. Pass <c>true</c> to get only
    ///   available, <c>false</c> to get only unavailable, or <c>null</c> to
    ///   get both. Defaults to <c>null</c>.
    /// </param>
    /// <param name="primaryImage">
    ///   Optional. Set to <c>true</c> to retrieve the primary image if the
    ///   image is part of a linked image list.
    /// </param>
    /// <returns>
    ///   An enumerable of all cross-references matching the filter criteria.
    /// </returns>
    IEnumerable<IImageCrossReference> GetAllImageCrossReferences(
        DataSource? imageSource = null,
        ImageEntityType? imageType = null,
        DataSource? xrefSource = null,
        DataSource? entitySource = null,
        DataEntityType? entityType = null,
        bool? isEnabled = null,
        bool? isDesired = null,
        bool? isAvailable = null,
        bool? primaryImage = null
    );

    /// <summary>
    ///   Get a specific image cross-reference by its local identifier.
    /// </summary>
    /// <param name="crossReferenceID">
    ///   The local identifier for the cross-reference.
    /// </param>
    /// <returns>
    ///   The cross-reference if found, otherwise <c>null</c>.
    /// </returns>
    IImageCrossReference? GetImageCrossReferenceByID(int crossReferenceID);

    /// <summary>
    ///   Get a random image cross-reference matching the specified criteria.
    /// </summary>
    /// <param name="imageSource">
    ///   Optional. Filter by image source (e.g. AniDB, TMDB, AniList, User,
    ///   etc.).
    /// </param>
    /// <param name="imageType">
    ///   Optional. Filter by image type (e.g. Primary, Backdrop, Banner, etc.).
    /// </param>
    /// <param name="xrefSource">
    ///   Optional. Filter by cross-reference source (e.g. AniDB, TMDB, AniList,
    ///   User, etc.).
    /// </param>
    /// <param name="entitySource">
    ///   Optional. Filter for entity source (e.g. AniDB, TMDB, AniList, User,
    ///   etc.).
    /// </param>
    /// <param name="entityType">
    ///   Optional. Filter for entity type (e.g. Primary, Backdrop, Banner,
    ///   etc.).
    /// </param>
    /// <param name="isEnabled">
    ///   Optional. Filter by enabled state. Pass <c>true</c> to get only
    ///   enabled, <c>false</c> to get only disabled, or <c>null</c> to get
    ///   both. Defaults to <c>null</c>.
    /// </param>
    /// <param name="isDesired">
    ///   Optional. Filter by desired state. Pass <c>true</c> to get only
    ///   desired, <c>false</c> to get only undesired, or <c>null</c> to get
    ///   both. Defaults to <c>null</c>.
    /// </param>
    /// <param name="isAvailable">
    ///   Optional. Filter by available state. Pass <c>true</c> to get only
    ///   available, <c>false</c> to get only unavailable, or <c>null</c> to
    ///   get both. Defaults to <c>null</c>.
    /// </param>
    /// <param name="primaryImage">
    ///   Optional. Set to <c>true</c> to retrieve the primary image if the
    ///   image is part of a linked image list.
    /// </param>
    /// <returns>
    ///   A random matching cross-reference, or <c>null</c> if none found.
    /// </returns>
    IImageCrossReference? GetRandomImageCrossReference(
        DataSource imageSource,
        ImageEntityType imageType,
        DataSource? xrefSource = null,
        DataSource? entitySource = null,
        DataEntityType? entityType = null,
        bool? isEnabled = null,
        bool? isDesired = null,
        bool? isAvailable = null,
        bool? primaryImage = null
    );

    /// <summary>
    ///   Get all cross-references for the specific entity, optionally filtered
    ///   by various criteria.
    /// </summary>
    /// <param name="entity">
    ///   The entity to get cross-references for.
    /// </param>
    /// <param name="imageSource">
    ///   Optional. Filter by image source (e.g. AniDB, TMDB, AniList, User,
    ///   etc.).
    /// </param>
    /// <param name="imageType">
    ///   Optional. Filter by image type (e.g. Primary, Backdrop, Banner, etc.).
    /// </param>
    /// <param name="xrefSource">
    ///   Optional. Filter by cross-reference source (e.g. AniDB, TMDB, AniList,
    ///   User, etc.).
    /// </param>
    /// <param name="isEnabled">
    ///   Optional. Filter by enabled state. Pass <c>true</c> to get only
    ///   enabled, <c>false</c> to get only disabled, or <c>null</c> to get
    ///   both. Defaults to <c>null</c>.
    /// </param>
    /// <param name="isDesired">
    ///   Optional. Filter by desired state. Pass <c>true</c> to get only
    ///   desired, <c>false</c> to get only undesired, or <c>null</c> to get
    ///   both. Defaults to <c>null</c>.
    /// </param>
    /// <param name="isAvailable">
    ///   Optional. Filter by available state. Pass <c>true</c> to get only
    ///   available, <c>false</c> to get only unavailable, or <c>null</c> to
    ///   get both. Defaults to <c>null</c>.
    /// </param>
    /// <param name="primaryImage">
    ///   Optional. Set to <c>true</c> to retrieve the primary image if the
    ///   image is part of a linked image list.
    /// </param>
    /// <param name="linkedEntityImages">
    ///   Optional. Set to <c>false</c> to only retrieve cross-references for
    ///   the entity's own images. Set to <c>true</c> to also retrieve
    ///   cross-references for images from other entities linked to the entity.
    ///   Set to <c>null</c> to let the service decide based on the entity.
    ///   Defaults to <c>null</c>.
    /// </param>
    /// <returns>
    ///   A readonly list of cross-references for the entity.
    /// </returns>
    IReadOnlyList<IImageCrossReference> GetImageCrossReferencesForEntity(
        IWithImages entity,
        DataSource? imageSource = null,
        ImageEntityType? imageType = null,
        DataSource? xrefSource = null,
        bool? isEnabled = null,
        bool? isDesired = null,
        bool? isAvailable = null,
        bool? primaryImage = null,
        bool? linkedEntityImages = null
    );

    #region Cross References | Add

    /// <summary>
    ///   Add a new cross-reference linking an image to an entity.
    /// </summary>
    /// <param name="entity">
    ///   The entity to link the image to.
    /// </param>
    /// <param name="image">
    ///   The image to link to the entity.
    /// </param>
    /// <param name="imageCrossReferenceData">
    ///   The cross-reference data defining the relationship between the two.
    /// </param>
    /// <exception cref="ImageCrossReferenceExistsException">
    ///   Thrown when attempting to add a cross-reference for an image and
    ///   entity when one already exists.
    /// </exception>
    /// <returns>
    ///   The newly created cross-reference.
    /// </returns>
    IImageCrossReference AddImageCrossReference(IWithImages entity, IImage image, ImageCrossReferenceData imageCrossReferenceData);

    #endregion

    #region Cross References | Update

    /// <summary>
    ///   Set an image as the preferred image for an entity for the given image
    ///   type. This will automatically unset IsPreferred for all other images
    ///   of the same type for the entity. Will add the cross-reference if it
    ///   doesn't already exist.
    /// </summary>
    /// <param name="entity">
    ///   The entity to set the preferred image for.
    /// </param>
    /// <param name="imageType">
    ///   The image type to set the preferred image for.
    /// </param>
    /// <param name="image">
    ///   The image to set as preferred.
    /// </param>
    /// <returns>
    ///   The newly added or updated cross-reference.
    /// </returns>
    IImageCrossReference SetPreferredImageForEntity(IWithImages entity, ImageEntityType imageType, IImage image);

    /// <summary>
    ///   Set an existing cross-reference as the preferred image. This will
    ///   automatically unset IsPreferred for all other images of the same type
    ///   for that entity.
    /// </summary>
    /// <param name="imageCrossReference">
    ///   The cross-reference to set as preferred.
    /// </param>
    /// <returns>
    ///   The updated cross-reference.
    /// </returns>
    IImageCrossReference SetPreferredImageForEntity(IImageCrossReference imageCrossReference);

    /// <summary>
    ///   Unset the cross-reference as the preferred image for an entity.
    /// </summary>
    /// <param name="imageCrossReference">
    ///   The cross-reference to unset preferred image for.
    /// </param>
    /// <returns>
    ///   Returns <c>true</c> if the operation succeeded, otherwise
    ///   <c>false</c>.
    /// </returns>
    bool UnsetPreferredImageForEntity(IImageCrossReference imageCrossReference);

    /// <summary>
    ///   Unset all preferred images across all image types on an entity.
    /// </summary>
    /// <param name="entity">
    ///   The entity to unset preferred images for.
    /// </param>
    /// <returns>
    ///   Returns <c>true</c> if the operation succeeded, otherwise
    ///   <c>false</c>.
    /// </returns>
    bool UnsetAllPreferredImagesForEntity(IWithImages entity);

    /// <summary>
    ///   Update an existing image cross-reference with new properties. This
    ///   allows partial updates where only specified fields are modified based
    ///   on which properties are set.
    /// </summary>
    /// <param name="imageCrossReference">
    ///   The cross-reference to update.
    /// </param>
    /// <param name="imageCrossReferenceUpdateData">
    ///   The update data containing the fields to update.
    /// </param>
    /// <returns>
    ///   The updated cross-reference.
    /// </returns>
    IImageCrossReference UpdateImageCrossReference(IImageCrossReference imageCrossReference, ImageCrossReferenceUpdateData imageCrossReferenceUpdateData);

    #endregion

    #region Cross References | Remove

    /// <summary>
    ///   Remove a cross-reference between an image and an entity. This does not delete
    ///   the image itself - only the association is removed.
    /// </summary>
    /// <param name="imageCrossReference">
    ///   The cross-reference to remove.
    /// </param>
    /// <returns>
    ///   Returns <c>true</c> if the cross-reference was removed, <c>false</c>
    ///   if not found.
    /// </returns>
    bool RemoveImageCrossReference(IImageCrossReference imageCrossReference);

    #endregion

    #endregion

    #region Helpers

    /// <summary>
    ///   The namespace for image identifiers.
    /// </summary>
    public static Guid ImageIdentifierNamespace { get; private set; } = UuidUtility.GetV5("ImageIdentifierNamespace", UuidUtility.PublicUuidNamespaces.OID);

    /// <summary>
    ///   Get the ID for the given source and resource identifier.
    /// </summary>
    /// <param name="imageSource">
    ///   The image source (e.g. AniDB, TMDB, AniList, User, etc.).
    /// </param>
    /// <param name="resourceID">
    ///   The remote resource identifier relative to the source.
    /// </param>
    /// <returns></returns>
    public static Guid GetIDForImageSourceAndResourceID(DataSource imageSource, string resourceID)
        => UuidUtility.GetV5($"ImageSource={imageSource},ResourceID={resourceID}", ImageIdentifierNamespace);

    /// <summary>
    ///   Try to get the ID and other metadata for the given entity.
    /// </summary>
    /// <param name="entity">
    ///   The entity to get the ID and other metadata for.
    /// </param>
    /// <param name="entitySource">
    ///   The source of the entity.
    /// </param>
    /// <param name="entityType">
    ///   The type of the entity.
    /// </param>
    /// <param name="entityID">
    ///   The ID of the entity.
    /// </param>
    /// <param name="entitySeasonNumber">
    ///   The season number of the entity, if applicable.
    /// </param>
    /// <param name="entityEpisodeNumber">
    ///   The episode number of the entity, if applicable.
    /// </param>
    /// <param name="releasedAt">
    ///   The release date of the entity, if applicable.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the ID and other metadata was found, otherwise
    ///   <c>false</c>.
    /// </returns>
    bool TryGetMetadataForEntity(
        IWithImages entity,
        out DataSource entitySource,
        out DataEntityType entityType,
        [NotNullWhen(true)] out string? entityID,
        out int? entitySeasonNumber,
        out int? entityEpisodeNumber,
        out DateOnly? releasedAt
    );

    /// <summary>
    ///   Resolve an entity from its source, type, and stringified identifier.
    ///   This is the inverse of <see cref="TryGetMetadataForEntity"/> and is
    ///   useful for looking up entities when only their metadata triplet is
    ///   available (e.g. from API route parameters or cross-reference data).
    /// </summary>
    /// <param name="entitySource">
    ///   The source of the entity (e.g. Shoko, AniDB, TMDB).
    /// </param>
    /// <param name="entityType">
    ///   The type of the entity (e.g. Series, Episode, Group).
    /// </param>
    /// <param name="entityID">
    ///   The stringified identifier of the entity, source- and type-specific.
    /// </param>
    /// <returns>
    ///   The resolved entity, or <c>null</c> if not found.
    /// </returns>
    IWithImages? GetEntityForImage(DataSource entitySource, DataEntityType entityType, string entityID);

    #endregion
}
