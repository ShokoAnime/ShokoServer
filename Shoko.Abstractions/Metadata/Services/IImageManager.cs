using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Image.Exceptions;
using Shoko.Abstractions.Metadata.Image.Options;
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
    ///   Gets the template URL in effect for every image source that is not
    ///   local: the user's own where one is set, else the default the core or
    ///   a plugin registered, else <c>null</c>.
    /// </summary>
    /// <returns>
    ///   A dictionary mapping image source to template URL.
    /// </returns>
    IReadOnlyDictionary<MetadataSource, string?> GetTemplateUrls();

    /// <summary>
    ///   Gets the template URL in effect for the image source: the user's own
    ///   where one is set, else the registered default. Replace <c>{0}</c>
    ///   with the <see cref="IImage.ResourceID"/> before use.
    /// </summary>
    /// <param name="imageSource">
    ///   The image source.
    /// </param>
    /// <returns>
    ///   The template url if set and available, otherwise <c>null</c>.
    /// </returns>
    string? GetTemplateUrlForSource(MetadataSource imageSource);

    /// <summary>
    ///   Registers the default template URL for a plugin's image source, which
    ///   is used whenever the user has not set one of their own.
    /// </summary>
    /// <remarks>
    ///   Kept in memory only, so register it on every start, before the first
    ///   <see cref="AddImage"/> for the source. Registering again replaces the
    ///   default. It never touches the user's setting, which
    ///   <see cref="SetTemplateUrlForSource"/> manages.
    /// </remarks>
    /// <param name="imageSource">
    ///   The image source. Must not be a core source, such as
    ///   <see cref="MetadataSource.AniDB"/> or <see cref="MetadataSource.TMDB"/>,
    ///   whose defaults the core keeps itself.
    /// </param>
    /// <param name="templateUrl">
    ///   The default template URL. Must be a valid URL starting with
    ///   <c>http://</c> or <c>https://</c> and contain <c>{0}</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when either argument is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   Thrown when the URL does not use the <c>http://</c> or <c>https://</c>
    ///   protocol, or it does not include the <c>{0}</c> template
    ///   substitution target.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <paramref name="imageSource"/> is a core source.
    /// </exception>
    void RegisterTemplateUrl(MetadataSource imageSource, string templateUrl);

    /// <summary>
    ///   Sets or clears the user's own template URL for an image source, which
    ///   overrides the registered default.
    /// </summary>
    /// <remarks>
    ///   The user's setting, saved in the server's configuration. A plugin
    ///   registers its default with <see cref="RegisterTemplateUrl"/> instead.
    /// </remarks>
    /// <param name="imageSource">
    ///   The image source.
    /// </param>
    /// <param name="templateUrl">
    ///   The template URL to set. If this is <c>null</c>, the user's template
    ///   is removed and the source goes back to its default. Must be a valid
    ///   URL starting with <c>http://</c> or <c>https://</c> and contain
    ///   <c>{0}</c> to be replaced.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   Thrown when the URL does not use the <c>http://</c> or <c>https://</c>
    ///   protocol, or it does not include the <c>{0}</c> template
    ///   substitution target.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when attempting to set or unset a URL for a metadata source which
    ///   is local, such as <see cref="MetadataSource.User"/> or
    ///   <see cref="MetadataSource.Shoko"/>.
    /// </exception>
    void SetTemplateUrlForSource(MetadataSource imageSource, string? templateUrl);

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
    ///   Get all images, optionally filtered using the specified
    ///   <paramref name="options"/> options.
    /// </summary>
    /// <param name="options">
    ///   Optional filtering options. See <see cref="ImageFilteringOptions"/>
    ///   for available filter fields. Omit or pass <c>null</c> to return all
    ///   images.
    /// </param>
    /// <returns>
    ///   All images matching the filter criteria.
    /// </returns>
    IEnumerable<IImage> GetAllImages(ImageFilteringOptions? options = null);

    /// <summary>
    ///   Get all images associated with the specified entity, optionally
    ///   filtered using the specified <paramref name="options"/> options.
    /// </summary>
    /// <remarks>
    ///   With the linked entries' images included, the entity's own
    ///   cross-references come first, and <see cref="IImage.IsPreferred"/> is
    ///   only set through them; an image preferred on a linked entry is what
    ///   the entity inherits while it has no preferred image of its own.
    /// </remarks>
    /// <param name="entity">
    ///   The entity to get images for.
    /// </param>
    /// <param name="options">
    ///   Optional filtering options. See <see cref="ImageFilteringOptions"/>
    ///   for available filter fields. Omit or pass <c>null</c> to return all
    ///   images for the entity.
    /// </param>
    /// <returns>
    ///   A readonly list of images associated with the entity, filtered by the
    ///   provided criteria.
    /// </returns>
    IReadOnlyList<IImage> GetImagesForEntity(
        IWithImages entity,
        ImageFilteringOptions? options = null
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
    IImage? GetImageBySourceAndRemoteResourceID(MetadataSource source, string resourceID, bool primaryImage = false);

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
    /// <remarks>
    ///   The image's source needs a template URL: the core keeps AniDB's and
    ///   TMDB's, and a plugin registers its own with
    ///   <see cref="RegisterTemplateUrl"/>. The image's
    ///   <see cref="IImage.ResourceID"/> completes that template, so it is the
    ///   rest of the remote URL and fits in 128 characters.
    /// </remarks>
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
    ///   Upload a new user submitted image from a stream. The same as
    ///   <see cref="UploadImage(Stream, string?, MetadataSource)"/> with
    ///   <see cref="MetadataSource.User"/>, or with
    ///   <see cref="MetadataSource.Generated"/> when not user submitted.
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
    ///   Upload a new user submitted image from a byte array. The same as
    ///   <see cref="UploadImage(byte[], string?, MetadataSource)"/> with
    ///   <see cref="MetadataSource.User"/>, or with
    ///   <see cref="MetadataSource.Generated"/> when not user submitted.
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

    /// <summary>
    ///   Upload a new image from a stream under a local source, such as a
    ///   plugin's own source registered as local. Uploading the same image
    ///   under the same source again gives back the image already stored.
    /// </summary>
    /// <param name="imageStream">
    ///   A stream containing the image data. May be data URL encoded w/content
    ///   type embedded.
    /// </param>
    /// <param name="contentType">
    ///   The MIME type of the image (e.g., <c>"image/jpeg"</c>), checked
    ///   against the detected type, or <c>null</c> to go by the detected
    ///   type alone.
    /// </param>
    /// <param name="source">
    ///   The registered local source to keep the image under, e.g.
    ///   <see cref="MetadataSource.User"/>,
    ///   <see cref="MetadataSource.Generated"/> or a plugin's own.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   Thrown when the source is not a registered local source, the image
    ///   stream is empty, the content type is invalid, or the image data is
    ///   not a valid image.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the image stream or the source is <c>null</c>.
    /// </exception>
    /// <exception cref="UnsupportedImageTypeException">
    ///   Thrown when the content type or detected image format is not in the
    ///   allowed image types list.
    /// </exception>
    /// <returns>
    ///   The newly created image, or the one already stored.
    /// </returns>
    IImage UploadImage(Stream imageStream, string? contentType, MetadataSource source);

    /// <summary>
    ///   Upload a new image from a byte array under a local source, such as a
    ///   plugin's own source registered as local. Uploading the same image
    ///   under the same source again gives back the image already stored.
    /// </summary>
    /// <param name="imageByteArray">
    ///   The image data as a byte array. May be data URL encoded w/content type
    ///   embedded.
    /// </param>
    /// <param name="contentType">
    ///   The MIME type of the image (e.g., <c>"image/jpeg"</c>), checked
    ///   against the detected type, or <c>null</c> to go by the detected
    ///   type alone.
    /// </param>
    /// <param name="source">
    ///   The registered local source to keep the image under, e.g.
    ///   <see cref="MetadataSource.User"/>,
    ///   <see cref="MetadataSource.Generated"/> or a plugin's own.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   Thrown when the source is not a registered local source, the image
    ///   byte array is empty, the content type is invalid, or the image data
    ///   is not a valid image.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the image byte array or the source is <c>null</c>.
    /// </exception>
    /// <exception cref="UnsupportedImageTypeException">
    ///   Thrown when the content type or detected image format is not in the
    ///   allowed image types list.
    /// </exception>
    /// <returns>
    ///   The newly created image, or the one already stored.
    /// </returns>
    IImage UploadImage(byte[] imageByteArray, string? contentType, MetadataSource source);

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
        MetadataSource? imageSource = null,
        ImageEntityType? imageType = null,
        MetadataSource? xrefSource = null,
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
        MetadataSource? imageSource = null,
        ImageEntityType? imageType = null,
        MetadataSource? xrefSource = null,
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
    IEnumerable<IImage> GetOrphanedImages(int daysOld = 7, MetadataSource? imageSource = null);

    /// <summary>
    ///   Purges an image: removes its cross-references, the image itself and
    ///   the file held for it on disk, so adding it again fetches a new copy.
    /// </summary>
    /// <param name="image">
    ///   The image to purge.
    /// </param>
    /// <returns>
    ///   <c>true</c> if anything was removed, <c>false</c> if nothing was
    ///   left to remove.
    /// </returns>
    Task<bool> PurgeImage(IImage image);

    /// <summary>
    ///   Schedule a background job to purge an image, as
    ///   <see cref="PurgeImage(IImage)"/> does.
    /// </summary>
    /// <param name="image">
    ///   The image to purge.
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
    Task<int> PurgeOrphanedImages(int daysOld = 7, MetadataSource? imageSource = null);

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
    Task SchedulePurgeOfOrphanedImages(int daysOld = 7, MetadataSource? imageSource = null);

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
    ///   Get all image cross-references, optionally filtered using the
    ///   specified <paramref name="options"/>.
    /// </summary>
    /// <param name="options">
    ///   Optional filtering options. See
    ///   <see cref="ImageCrossReferenceFilteringOptions"/> for available
    ///   filter fields. Omit or pass <c>null</c> to return all cross-
    ///   references.
    /// </param>
    /// <returns>
    ///   All cross-references matching the filter criteria.
    /// </returns>
    IEnumerable<IImageCrossReference> GetAllImageCrossReferences(ImageCrossReferenceFilteringOptions? options = null);

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
    ///   The image source to filter by (e.g. AniDB, TMDB, AniList, User, etc.).
    /// </param>
    /// <param name="imageType">
    ///   The image type to filter by (e.g. Primary, Backdrop, Banner, etc.).
    /// </param>
    /// <param name="options">
    ///   Optional filtering options. See
    ///   <see cref="RandomImageCrossReferenceFilteringOptions"/> for
    ///   available filter fields. Omit or pass <c>null</c> for no additional
    ///   filtering.
    /// </param>
    /// <returns>
    ///   A random matching cross-reference, or <c>null</c> if none found.
    /// </returns>
    IImageCrossReference? GetRandomImageCrossReference(
        MetadataSource imageSource,
        ImageEntityType imageType,
        RandomImageCrossReferenceFilteringOptions? options = null
    );

    /// <summary>
    ///   Get all cross-references for the specific entity, optionally filtered
    ///   using the specified <paramref name="options"/>.
    /// </summary>
    /// <param name="entity">
    ///   The entity to get cross-references for.
    /// </param>
    /// <param name="options">
    ///   Optional filtering options. See
    ///   <see cref="ImageCrossReferenceFilteringOptions"/> for available
    ///   filter fields. Pass <c>null</c> to return all cross-references
    ///   for the entity.
    /// </param>
    /// <returns>
    ///   A readonly list of cross-references for the entity.
    /// </returns>
    IReadOnlyList<IImageCrossReference> GetImageCrossReferencesForEntity(
        IWithImages entity,
        ImageCrossReferenceFilteringOptions? options = null
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
    /// <exception cref="ArgumentException">
    ///   Thrown when the <paramref name="image"/> is not stored.
    /// </exception>
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
    /// <remarks>
    ///   Always writes the entity's own cross-reference, adding one when the
    ///   image only comes through a linked entry, so a linked entry shared
    ///   with others is never changed.
    /// </remarks>
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
    ///   Get the ID for the given source and resource identifier, hashed from
    ///   the source's value and the resource identifier.
    /// </summary>
    /// <param name="imageSource">
    ///   The image source (e.g. AniDB, TMDB, AniList, User, etc.).
    /// </param>
    /// <param name="resourceID">
    ///   The remote resource identifier relative to the source.
    /// </param>
    /// <returns>The image ID.</returns>
    public static Guid GetIDForImageSourceAndResourceID(MetadataSource imageSource, string resourceID)
        => UuidUtility.GetV5($"ImageSource={imageSource.Value},ResourceID={resourceID}", ImageIdentifierNamespace);

    /// <summary>
    ///   Check if the given cross-reference is linked to the given entity.
    /// </summary>
    /// <param name="entity">
    ///   The entity to check.
    /// </param>
    /// <param name="xref">
    ///   The cross-reference to check.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the cross-reference is linked to the entity, otherwise
    ///   <c>false</c>.
    /// </returns>
    bool IsLinkedCrossReference(IWithImages entity, IImageCrossReference xref);

    /// <summary>
    ///   Resolve an entity from its ID, e.g. the
    ///   <see cref="IImageCrossReference.EntityID"/> of a cross-reference,
    ///   through <see cref="IMetadataService.GetEntry(MetadataGuid)"/> when the
    ///   entry it finds has images. A default ordering, and an ordering of a
    ///   core source other than <c>user</c>, is never resolved.
    /// </summary>
    /// <param name="entityID">
    ///   The ID of the entity.
    /// </param>
    /// <returns>
    ///   The resolved entity, or <c>null</c> if not found.
    /// </returns>
    IWithImages? GetEntityForImage(MetadataGuid entityID);

    #endregion
}
