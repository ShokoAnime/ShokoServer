using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Image.Exceptions;
using Shoko.Abstractions.Metadata.Image.Options;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Utilities;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Extensions;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.AniDB.UDP;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Image;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Services;

public class ImageManager(
    ILogger<ImageManager> logger,
    IApplicationPaths applicationPaths,
    ISettingsProvider settingsProvider,
    IQueueScheduler schedulerFactory,
    Lazy<IMetadataService> metadataService,
    Lazy<AniDBUDPConnectionHandler> udpConnectionHandler,
    IHttpClientFactory httpClientFactory,
    ConfigurationProvider<ServerSettings> configurationProvider,
    ShokoImageRepository imageRepository,
    ShokoImage_EntityRepository xrefRepository
) : IImageManager, IImageFileStore
{
    #region Image Sources

    private Dictionary<MetadataSource, string?>? _cachedUrls = null;

    /// <summary>
    /// The default templates plugins registered for their sources, used when
    /// the user has not set one.
    /// </summary>
    private readonly ConcurrentDictionary<MetadataSource, string> _registeredTemplates = new();

    /// <inheritdoc/>
    public IReadOnlyDictionary<MetadataSource, string?> GetTemplateUrls()
    {
        if (_cachedUrls is not null)
            return _cachedUrls;
        lock (applicationPaths)
        {
            if (_cachedUrls is not null)
                return _cachedUrls;
            var userRegisteredTemplates = configurationProvider.Load().Image.ImageTemplateUrls
                .DistinctBy(template => template.ImageSource)
                .ToDictionary(template => template.ImageSource, template => template.TemplateUrl);
            var dict = new Dictionary<MetadataSource, string?>();
            foreach (var dataSource in MetadataSource.All)
            {
                if (dataSource.IsLocal)
                    continue;
                if (userRegisteredTemplates.TryGetValue(dataSource, out var templateUrl) && templateUrl is { Length: > 0 })
                    dict.Add(dataSource, templateUrl);
                else if (dataSource == MetadataSource.AniDB)
                    dict.Add(dataSource, DefaultAnidbUrlTemplate());
                else
                    dict.Add(dataSource, _registeredTemplates.TryGetValue(dataSource, out var registered) ? registered : null);
            }
            return _cachedUrls = dict;
        }
    }

    private string DefaultAnidbUrlTemplate()
    {
        // Setting override.
        var setting = settingsProvider.GetSettings().AniDb.ImageCdnUrl;
        if (!string.IsNullOrWhiteSpace(setting) && !string.Equals(setting, Constants.AnidbCdnUrl) && (setting.StartsWith("http://") || setting.StartsWith("https://")))
        {
            // Setting as a URL template.
            if (setting.Contains("{0}"))
                return setting;

            // Setting as a base URL.
            if (setting.EndsWith("/", StringComparison.Ordinal))
                setting = setting[..^1];
            return string.Format(Constants.URLS.AniDB_Images, setting);
        }

        // UDP API provided override.
        if (udpConnectionHandler.Value is { } handler)
            return handler.ImageServerUrl;

        // Static fallback.
        return string.Format(Constants.URLS.AniDB_Images, Constants.AnidbCdnUrl);
    }

    /// <inheritdoc/>
    public void RegisterTemplateUrl(MetadataSource imageSource, string templateUrl)
    {
        ArgumentNullException.ThrowIfNull(imageSource);
        ArgumentNullException.ThrowIfNull(templateUrl);
        if (imageSource.IsCore)
            throw new InvalidOperationException($"The core keeps the template URL for {imageSource.Value} itself; {nameof(imageSource)} cannot be a core source.");

        ValidateTemplateUrl(templateUrl);
        lock (applicationPaths)
        {
            _registeredTemplates[imageSource] = templateUrl;
            _cachedUrls = null;
        }
    }

    /// <summary>
    ///   Checks that a template URL is an absolute http or https URL with a
    ///   <c>{0}</c> in it.
    /// </summary>
    /// <param name="templateUrl">The template URL.</param>
    /// <exception cref="ArgumentException">The URL is not valid.</exception>
    private static void ValidateTemplateUrl(string templateUrl)
    {
        if (!Uri.TryCreate(templateUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException($"{nameof(templateUrl)} must be a valid http:// or https:// URL.", nameof(templateUrl));
        if (!templateUrl.Contains("{0}"))
            throw new ArgumentException($"{nameof(templateUrl)} must contain {{0}}.", nameof(templateUrl));
    }

    /// <inheritdoc/>
    public string? GetTemplateUrlForSource(MetadataSource imageSource)
    {
        var urls = GetTemplateUrls();
        return urls.TryGetValue(imageSource, out var url) ? url : null;
    }

    /// <inheritdoc/>
    public void SetTemplateUrlForSource(MetadataSource imageSource, string? templateUrl)
    {
        if (imageSource.IsLocal)
            throw new InvalidOperationException($"{nameof(imageSource)} cannot be a local source (Shoko, User or Locally Generated).");

        if (templateUrl is not null)
        {
            ValidateTemplateUrl(templateUrl);

            lock (applicationPaths)
            {
                var config = configurationProvider.Load();
                config.Image.ImageTemplateUrls.RemoveAll(template => template.ImageSource == imageSource);
                config.Image.ImageTemplateUrls.Add(new ImageTemplateUrlConfiguration()
                {
                    ImageSource = imageSource,
                    TemplateUrl = templateUrl,
                });
                configurationProvider.Save(config);
                if (_cachedUrls is not null)
                    _cachedUrls[imageSource] = templateUrl;
            }
        }
        else
        {
            lock (applicationPaths)
            {
                var config = configurationProvider.Load();
                config.Image.ImageTemplateUrls.RemoveAll(template => template.ImageSource == imageSource);
                configurationProvider.Save(config);

                // Worked out again, so the source falls back to its default.
                _cachedUrls = null;
            }
        }
    }

    #endregion

    #region Images

    /// <inheritdoc/>
    public event EventHandler<ImageEventArgs>? ImageAdded;

    /// <inheritdoc/>
    public event EventHandler<ImageEventArgs>? ImageUpdated;

    /// <inheritdoc/>
    public event EventHandler<ImageEventArgs>? ImageDownloaded;

    /// <inheritdoc/>
    public event EventHandler<ImageEventArgs>? ImageRemoved;

    /// <inheritdoc/>
    public IEnumerable<IImage> GetAllImages(ImageFilteringOptions? options = null)
    {
        var imageSource = options?.ImageSource;
        var imageType = options?.ImageType;
        var xrefSource = options?.XrefSource;
        var isEnabled = options?.IsEnabled;
        var isDesired = options?.IsDesired;
        var isPreferred = options?.IsPreferred;
        var isAvailable = options?.IsAvailable;
        var isPrimaryAvailable = options?.IsPrimaryAvailable;
        var isPrimaryImage = options?.IsPrimaryImage;
        IEnumerable<IImage> images = imageRepository.GetAll();
        if (
            imageSource is not null ||
            isAvailable is not null ||
            isPrimaryAvailable is not null ||
            isPrimaryImage is not null
        )
            images = images.Where(image =>
                (imageSource is null || image.Source == imageSource) &&
                (isAvailable is null || image.IsAvailable == isAvailable.Value) &&
                (isPrimaryAvailable is null || image.IsPrimaryAvailable == isPrimaryAvailable) &&
                (isPrimaryImage is null || image.PrimaryID == image.ID == isPrimaryImage.Value)
            );
        if (
            imageType is not null ||
            xrefSource is not null ||
            isEnabled is not null ||
            isDesired is not null ||
            isPreferred is not null
        )
        {
            images = images
                .Where(image => xrefRepository.GetByImageID(image.ID) is { Count: > 0 } xrefs && xrefs
                    .Any(xref =>
                        (imageType is null || xref.ImageType == imageType) &&
                        (xrefSource is null || xref.Source == xrefSource) &&
                        (isEnabled is null || xref.IsEnabled == isEnabled) &&
                        (isDesired is null || xref.IsDesired == isDesired) &&
                        (isPreferred is null || xref.IsPreferred == isPreferred)
                    )
                );
        }

        if (options?.AsPrimaryImage is not null)
            images = images
                .Select(image => image.ID == image.PrimaryID ? image : imageRepository.GetByID(image.PrimaryID))
                .WhereNotNull();

        return images;
    }

    /// <inheritdoc/>
    public IReadOnlyList<IImage> GetImagesForEntity(
        IWithImages entity,
        ImageFilteringOptions? options = null
    )
    {
        var entityID = entity.ID;

        var imageSource = options?.ImageSource;
        var imageType = options?.ImageType;
        var xrefSource = options?.XrefSource;
        var isEnabled = options?.IsEnabled;
        var isDesired = options?.IsDesired;
        var isPreferred = options?.IsPreferred;
        var isAvailable = options?.IsAvailable;
        var isPrimaryImage = options?.IsPrimaryImage;
        var primaryImage = options?.AsPrimaryImage ?? false;
        var isPrimaryAvailable = options?.IsPrimaryAvailable;
        var linkedEntityImages = options?.LinkedEntityImages;
        Func<IEnumerable<IImageCrossReference>, IEnumerable<IImageCrossReference>> filter =
            imageSource is not null ||
            imageType is not null ||
            xrefSource is not null ||
            isEnabled is not null ||
            isDesired is not null ||
            isPreferred is not null ||
            isAvailable is not null ||
            isPrimaryImage is not null ||
            isPrimaryAvailable is not null
                ? xrefs => xrefs
                    .Where(xref =>
                        (imageSource is null || xref.ImageSource == imageSource) &&
                        (imageType is null || xref.ImageType == imageType) &&
                        (xrefSource is null || xref.Source == xrefSource) &&
                        (isEnabled is null || xref.IsEnabled == isEnabled) &&
                        (isDesired is null || xref.IsDesired == isDesired) &&
                        (isPreferred is null || xref.IsPreferred == isPreferred) &&
                        (isAvailable is null || xref.IsAvailable == isAvailable) &&
                        (isPrimaryImage is null || xref.PrimaryImageID == xref.ImageID == isPrimaryImage) &&
                        (isPrimaryAvailable is null || xref.IsPrimaryAvailable == isPrimaryAvailable)
                    )
                : xrefs => xrefs;

        linkedEntityImages ??= entity is IShokoGroup or IShokoSeries or ISeason<IShokoSeries, IShokoEpisode> or IShokoEpisode;
        if (linkedEntityImages.Value)
        {
            var xrefs = new List<IEnumerable<IImageCrossReference>>();
            var visitedEntities = new HashSet<MetadataGuid>();
            void AddEntryXrefs(IMetadata entry)
            {
                // The same entity can be reachable through more than one link, and we only want its images once.
                if (!visitedEntities.Add(entry.ID))
                    return;

                xrefs.Add(filter(xrefRepository.GetByEntity(entry.ID)).ToList());
            }

            void AddSeriesXrefs(IShokoSeries series)
            {
                AddEntryXrefs(series);
                foreach (var s in series.LinkedSeries)
                    AddEntryXrefs(s);
                foreach (var s in series.LinkedSeasons)
                    AddEntryXrefs(s);
                foreach (var m in series.LinkedMovies)
                    AddEntryXrefs(m);
            }

            AddEntryXrefs(entity);

            switch (entity)
            {
                case IShokoGroup group:
                {
                    AddSeriesXrefs(group.MainSeries);

                    // The main series can be without images of its own, e.g. an AniDB entry that has no poster, so fall back to
                    // the other series in the group instead of leaving the group without any images at all.
                    if (xrefs.All(list => !list.Any()))
                        foreach (var otherSeries in group.AllSeries)
                            AddSeriesXrefs(otherSeries);
                    break;
                }
                case IShokoSeries series:
                {
                    AddSeriesXrefs(series);
                    break;
                }
                case ISeason<IShokoSeries, IShokoEpisode> season:
                {
                    foreach (var s in season.LinkedSeasons)
                        AddEntryXrefs(s);
                    break;
                }
                case IShokoEpisode episode:
                {
                    foreach (var s in episode.LinkedEpisodes)
                        AddEntryXrefs(s);
                    foreach (var m in episode.LinkedMovies)
                        AddEntryXrefs(m);
                    break;
                }
            }

            return xrefs
                .SelectMany(list => list)
                .Select(xref => (xref, image: GetImageByID(xref.ImageID, primaryImage)!, linkedXref: xref.EntityID != entityID))
                .Where(tuple => tuple.image is not null)
                .OrderBy(tuple => tuple.xref.ImageType)
                .ThenBy(tuple => tuple.linkedXref)
                .ThenByDescending(tuple => IsLocallyMadeEntry(tuple.xref.EntityID.Source))
                // By source, kind and then the ID as text, as before the IDs became guids.
                .ThenBy(tuple => tuple.xref.EntityID.Source)
                .ThenBy(tuple => tuple.xref.EntityID.EntityType)
                .ThenBy(tuple => tuple.xref.EntityID.ID)
                .ThenBy(tuple => tuple.xref.Ordering)
                .ThenBy(tuple => tuple.xref.Source)
                .DistinctBy(tuple => (tuple.image.ID, tuple.xref.ImageType))
                .Select(tuple => ImageStub.Wrap(tuple.image, tuple.xref, tuple.linkedXref))
                .ToList();
        }

        return filter(xrefRepository.GetByEntity(entityID))
            .Select(xref => (xref, image: GetImageByID(xref.ImageID, primaryImage)!))
            .Where(tuple => tuple.image is not null)
            .OrderBy(tuple => tuple.xref.ImageType)
            .ThenBy(tuple => tuple.xref.Ordering)
            .ThenBy(tuple => tuple.xref.Source)
            .DistinctBy(tuple => (tuple.image.ID, tuple.xref.ImageType))
            .Select(tuple => ImageStub.Wrap(tuple.image, tuple.xref))
            .ToList();
    }

    /// <inheritdoc/>
    public IImage? GetImageByID(Guid imageID, bool primaryImage = false)
    {
        var image = imageRepository.GetByID(imageID);
        if (image is not null && primaryImage && image.ID != image.PrimaryID)
            image = imageRepository.GetByID(image.PrimaryID);
        return image;
    }

    /// <inheritdoc/>
    [Obsolete("Use the Universally Unique Identifier instead.")]
    public IImage? GetImageByID(int localImageID, bool primaryImage = false)
    {
        var image = imageRepository.GetByLocalID(localImageID);
        if (image is not null && primaryImage && image.ID != image.PrimaryID)
            image = imageRepository.GetByID(image.PrimaryID);
        return image;
    }

    /// <inheritdoc/>
    public IImage? GetImageBySourceAndRemoteResourceID(MetadataSource source, string resourceID, bool primaryImage = false)
        => GetImageByID(IImageManager.GetIDForImageSourceAndResourceID(source, resourceID), primaryImage);

    /// <inheritdoc/>
    public IShokoSeries? GetFirstSeriesForImage(IImage image)
        => xrefRepository.GetByImageID(image.ID)
            .Where(xref => xref.IsEnabled && (xref.EntityType == MetadataEntityType.Movie || xref.EntityType == MetadataEntityType.Series
                || xref.EntityType == MetadataEntityType.Season || xref.EntityType == MetadataEntityType.Episode))
            .DistinctBy(xref => (xref.EntitySource, xref.EntityType, xref.EntityID))
            .SelectMany(xref => xref.GetEntity() switch
            {
                ISeries series => series.ShokoSeries,
                IMovie movie => movie.ShokoSeries,
                ISeason season => season.Series?.ShokoSeries ?? [],
                IEpisode episode => episode.Series?.ShokoSeries ?? [],
                _ => [],
            })
            .FirstOrDefault();

    #region Images | Add

    public IReadOnlyList<string> AllowedMimeTypes { get; private set; } =
    [
        "image/jpeg",
        "image/png",
        "image/bmp",
        "image/gif",
        "image/tiff",
        "image/webp",
        "image/svg+xml",
    ];

    /// <inheritdoc/>
    public IImage AddImage(ImageData imageData)
    {
        if (GetTemplateUrlForSource(imageData.Source) is null)
            throw new MissingImageSourceTemplateUrlException()
            {
                ImageSource = imageData.Source,
            };

        var id = IImageManager.GetIDForImageSourceAndResourceID(imageData.Source, imageData.ResourceID);
        if (imageRepository.GetByID(id) is not null)
            throw new ImageDataExistsException()
            {
                ImageSource = imageData.Source,
                ImageResourceID = imageData.ResourceID,
            };

        var contentType = GetContentTypeFromResourceID(imageData.Source, imageData.ResourceID) ?? ContentTypeHelper.UnknownMimeType;
        var image = new ShokoImage()
        {
            ID = id,
            PrimaryID = id,
            Height = imageData.Height,
            Width = imageData.Width,
            CountryCode = imageData.CountryCode,
            LanguageCode = imageData.LanguageCode,
            ContentType = contentType,
            Source = imageData.Source,
            ResourceID = imageData.ResourceID,
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
        };

        // The image data may already exist on disk (e.g. re-added after a purge), so reflect that.
        image.RefreshAvailability();

        imageRepository.Save(image);

        _ = Task.Run(() => ImageAdded?.Invoke(this, new() { Image = image }));

        return image;
    }

    /// <inheritdoc/>
    public IImage UploadImage(Stream imageStream, string? contentType = null, bool userSubmitted = true)
        => UploadImage(imageStream, contentType, userSubmitted ? MetadataSource.User : MetadataSource.Generated);

    /// <inheritdoc/>
    public IImage UploadImage(byte[] imageByteArray, string? contentType = null, bool userSubmitted = true)
        => UploadImage(imageByteArray, contentType, userSubmitted ? MetadataSource.User : MetadataSource.Generated);

    /// <inheritdoc/>
    public IImage UploadImage(Stream imageStream, string? contentType, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        EnsureUploadSource(source);
        return UploadImage(imageStream.ToByteArray(), contentType, source);
    }

    /// <inheritdoc/>
    public IImage UploadImage(byte[] imageByteArray, string? contentType, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(imageByteArray);
        EnsureUploadSource(source);

        TryConvertFromDataURL(ref imageByteArray, ref contentType);

        if (contentType is not null)
        {
            contentType = contentType?.ToLower().Replace("jpg", "jpeg") ?? string.Empty;
            if (contentType is not { Length: > 8 } || contentType[0..6] is not "image/")
                throw new ArgumentException("The provided content-type is not valid.", nameof(contentType));

            if (!AllowedMimeTypes.Contains(contentType))
                throw new UnsupportedImageTypeException()
                {
                    ImageSource = source,
                    ImageResourceID = string.Empty,
                    FileExtension = string.Empty,
                    DetectedMimeType = contentType,
                };
        }

        var md5 = Convert.ToHexString(MD5.HashData(imageByteArray));
        var id = IImageManager.GetIDForImageSourceAndResourceID(source, md5);
        if (imageRepository.GetByID(id) is { } existingImage)
        {
            if (contentType is not null && existingImage.ContentType != contentType)
                throw new ArgumentException("The provided content-type does not match the actual image format.", nameof(contentType));

            return existingImage;
        }

        // Only data recognized as a known image format reaches a decoder.
        if (GetImageMimeType(imageByteArray) is not { } expectedContentType
            || !TryReadImage(imageByteArray, out var width, out var height))
            throw new ArgumentException("The provided image data is not valid.", nameof(imageByteArray));

        if (contentType is not null && expectedContentType != contentType)
            throw new ArgumentException("The provided content-type does not match the actual image format.", nameof(contentType));

        if (contentType is null && !AllowedMimeTypes.Contains(expectedContentType))
            throw new UnsupportedImageTypeException()
            {
                ImageSource = source,
                ImageResourceID = md5,
                FileExtension = string.Empty,
                DetectedMimeType = expectedContentType,
            };

        var image = new ShokoImage()
        {
            ID = id,
            PrimaryID = id,
            Source = source,
            ResourceID = md5,
            Height = height,
            Width = width,
            ContentType = expectedContentType,
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
        };

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(image.LocalPath)!);
            using var stream = File.Create(image.LocalPath);
            stream.Write(imageByteArray);
        }
        catch (Exception ex)
        {
            if (File.Exists(image.LocalPath))
            {
                try
                {
                    File.Delete(image.LocalPath);
                }
                catch
                {
                    // ignored
                }
            }

            throw new ArgumentException("The provided image data is not valid.", nameof(imageByteArray), ex);
        }

        // The file was just written to disk above.
        image.IsAvailable = true;

        imageRepository.Save(image);

        _ = Task.Run(() => ImageAdded?.Invoke(this, new() { Image = image }));

        return image;
    }

    /// <summary>
    ///   Checks that images can be uploaded under a source: it must be a
    ///   registered local source, as a remote one's images are fetched.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <exception cref="ArgumentNullException">The source is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The source is not registered, or not local.</exception>
    private static void EnsureUploadSource(MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.IsRegistered)
            throw new ArgumentException($"Images cannot be uploaded under \"{source.Value}\": no plugin registered it.", nameof(source));
        if (!source.IsLocal)
            throw new ArgumentException($"Images cannot be uploaded under \"{source.Value}\": it is a remote source, whose images are fetched.", nameof(source));
    }

    /// <summary>
    ///   Eagerly detect content type from a resource ID by treating it as a URL
    ///   path, stripping query parameters, and looking up the file extension
    ///   against the allowed MIME types.
    /// </summary>
    /// <returns>
    ///   The MIME type if the extension maps to an allowed image type, or
    ///   <c>null</c> if no extension could be detected in the resource ID.
    /// </returns>
    /// <exception cref="UnsupportedImageTypeException">
    ///   Thrown if the resource ID contains a file extension that maps to a MIME
    ///   type not in <see cref="AllowedMimeTypes"/>.
    /// </exception>
    public string? GetContentTypeFromResourceID(MetadataSource source, string resourceID)
    {
        if (string.IsNullOrEmpty(resourceID))
            return null;

        // Strip query parameters (treat as URL path)
        var queryIndex = resourceID.IndexOf('?');
        var path = queryIndex >= 0 ? resourceID[..queryIndex] : resourceID;

        // Check for file extension
        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext))
            return null;

        // Look up MIME from extension
        if (!ContentTypeHelper.TryGetContentType(resourceID, out var mime))
            return null;

        // Validate against allowed image MIME types
        if (!AllowedMimeTypes.Contains(mime))
            throw new UnsupportedImageTypeException()
            {
                ImageSource = source,
                ImageResourceID = resourceID,
                FileExtension = ext,
                DetectedMimeType = mime,
            };

        return mime;
    }

    #endregion

    #region Images | Update

    /// <inheritdoc/>
    public IImage EnableImage(IImage image, bool isEnabled)
        => UpdateImage(image, new() { IsEnabled = isEnabled });

    /// <inheritdoc/>
    public IImage SetPrimaryImage(IImage image, IImage? primaryImage)
        => UpdateImage(image, new() { PrimaryImage = primaryImage });

    /// <inheritdoc/>
    public IImage UpdateImage(IImage image, ImageUpdateData imageUpdateData)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(imageUpdateData);
        if (imageRepository.GetByID(image.ID) is not ShokoImage localImage)
            throw new ArgumentException("Invalid image given to UpdateImage", nameof(image));

        var now = DateTime.UtcNow;
        var imagesToSave = new HashSet<ShokoImage>();
        var xrefsToSave = new HashSet<ShokoImage_Entity>();
        if (imageUpdateData.IsEnabled.HasValue)
        {
            var xrefs = xrefRepository.GetByImageID(localImage.ID).Where(xref => xref.IsEnabled != imageUpdateData.IsEnabled.Value).ToList();
            foreach (var xref in xrefs)
            {
                xref.IsEnabled = imageUpdateData.IsEnabled.Value;
                xrefsToSave.Add(xref);
            }
        }

        if (localImage.Update(imageUpdateData) || xrefsToSave.Count > 0)
        {
            localImage.LastUpdatedAt = now;
            imagesToSave.Add(localImage);
        }

        var shouldUpdatePrimaryImage = imageUpdateData.PrimaryImage is not null || localImage.PrimaryID != localImage.ID;
        if (shouldUpdatePrimaryImage)
        {
            var previousPrimaryID = localImage.PrimaryID;
            var nextPrimaryID = imageUpdateData.PrimaryImage switch
            {
                null => localImage.ID,
                { ID: var primaryID } when primaryID == localImage.ID => localImage.ID,
                { ID: var primaryID } => imageRepository.GetByID(primaryID)?.PrimaryID ?? primaryID,
            };
            if (previousPrimaryID != nextPrimaryID)
            {
                localImage.PrimaryID = nextPrimaryID;
                localImage.LastUpdatedAt = now;
                imagesToSave.Add(localImage);

                // Keep linked image groups and xref primary pointers in sync with the new canonical primary id.
                if (previousPrimaryID == localImage.ID)
                {
                    var linkedImages = imageRepository.GetByPrimaryImageID(previousPrimaryID)
                        .Where(linkedImage => linkedImage.ID != localImage.ID && linkedImage.PrimaryID != nextPrimaryID)
                        .ToList();
                    foreach (var linkedImage in linkedImages)
                    {
                        linkedImage.PrimaryID = nextPrimaryID;
                        imagesToSave.Add(linkedImage);
                    }
                }

                var xrefsToUpdate = previousPrimaryID == localImage.ID
                    ? xrefRepository.GetByImageID(localImage.ID)
                        .Where(xref => xref.PrimaryImageID != nextPrimaryID)
                        .Concat(
                            xrefRepository.GetByPrimaryImageID(previousPrimaryID)
                                .Where(xref => xref.ImageID != localImage.ID && xref.PrimaryImageID != nextPrimaryID)
                        )
                        .DistinctBy(xref => xref.ID)
                        .ToList()
                    : xrefRepository.GetByImageID(localImage.ID)
                        .Where(xref => xref.PrimaryImageID != nextPrimaryID)
                        .ToList();
                foreach (var xref in xrefsToUpdate)
                {
                    xref.PrimaryImageID = nextPrimaryID;
                    xref.LastUpdatedAt = now;
                    xrefsToSave.Add(xref);
                }
            }
        }

        imageRepository.Save(imagesToSave);
        xrefRepository.Save(xrefsToSave);

        foreach (var imageToSave in imagesToSave)
            Task.Run(() => ImageUpdated?.Invoke(this, new() { Image = imageToSave }));
        foreach (var xrefToSave in xrefsToSave)
        {
            Task.Run(() => ImageCrossReferenceUpdated?.Invoke(this, new() { ImageCrossReference = xrefToSave }));
            EmitEventForRelatedEntry(xrefToSave, UpdateReason.ImageUpdated);
        }

        return localImage;
    }

    #endregion

    #region Image | Download

    private static readonly TimeSpan[] _retryTimeSpans = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];

    private static readonly AsyncRetryPolicy _retryPolicy = Policy
        .Handle<HttpRequestException>()
        .Or<TaskCanceledException>()
        .WaitAndRetryAsync(_retryTimeSpans, (exception, timeSpan) =>
        {
            if (timeSpan == _retryTimeSpans[3] || exception is HttpRequestException hre && hre.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
                throw exception;
        });

    /// <inheritdoc/>
    public async Task<bool> CheckIfAvailableAtRemote(IImage image)
    {
        var template = GetTemplateUrlForSource(image.Source);
        if (template is null)
            return false;

        var remoteUrl = string.Format(template, image.ResourceID);
        try
        {
            using var client = httpClientFactory.CreateClient("Default");
            using var stream = await _retryPolicy.ExecuteAsync(async () => await client.GetStreamAsync(remoteUrl)).ConfigureAwait(false);
            var bytes = new byte[SniffLength];
            var read = await stream.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false).ConfigureAwait(false);
            stream.Close();
            return GetImageMimeType(bytes.AsSpan(0, read)) is not null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve resource at url: {RemoteURL}", remoteUrl);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DownloadImage(IImage image, bool force = false)
    {
        var template = GetTemplateUrlForSource(image.Source);
        if (template is null)
        {
            logger.LogWarning("Unable to find template url to use for {Source}. (Image={ImageID})", image.Source, image.ID);
            return false;
        }

        if (imageRepository.GetByID(image.ID) is not { } shokoImage)
        {
            logger.LogWarning("Unable to find image to update in database. (Image={ImageID})", image.ID);
            return false;
        }

        // Recompute from disk (this path is cold — about to do network I/O), so a stale
        // cached flag self-heals in both directions instead of waiting for a validation pass.
        var wasAvailable = shokoImage.IsAvailable;
        var previouslyDownloaded = shokoImage.RefreshAvailability();
        if (!force && previouslyDownloaded)
        {
            logger.LogDebug("Image already in cache. (Image={ImageID})", image.ID);
            if (wasAvailable != previouslyDownloaded)
            {
                shokoImage.LastUpdatedAt = DateTime.UtcNow;
                imageRepository.Save(shokoImage);
                _ = Task.Run(() => ImageUpdated?.Invoke(this, new() { Image = shokoImage }));
            }
            return true;
        }

        var downloaded = false;
        var remoteUrl = string.Format(template, image.ResourceID);
        var originalContentType = shokoImage.ContentType;
        try
        {
            using var client = httpClientFactory.CreateClient("Default");
            var byteArray = await _retryPolicy.ExecuteAsync(async () => await client.GetByteArrayAsync(remoteUrl)).ConfigureAwait(false);
            if (GetImageMimeType(byteArray) is not { } mimeType)
                throw new UnsupportedImageTypeException()
                {
                    ImageSource = image.Source,
                    ImageResourceID = image.ResourceID,
                    FileExtension = Path.GetExtension(image.ResourceID) ?? string.Empty,
                    DetectedMimeType = "unknown",
                };

            if (!TryReadImage(byteArray, out var width, out var height))
                throw new HttpRequestException(
                    $"Invalid or disallowed image data format at remote resource: {remoteUrl}",
                    null,
                    HttpStatusCode.ExpectationFailed
                );

            // Set the content type _before_ accessing the local path, so the local path will have the correct extension.
            shokoImage.ContentType = mimeType;

            Directory.CreateDirectory(Path.GetDirectoryName(image.LocalPath)!);
            if (File.Exists(image.LocalPath))
                File.Delete(image.LocalPath);
            File.WriteAllBytes(image.LocalPath, byteArray);

            logger.LogInformation("Image downloaded to cache: {DownloadUrl} (Image={ImageID})", remoteUrl, image.ID);

            // Update metadata after successfully storing the file.
            shokoImage.Width = width;
            shokoImage.Height = height;
            shokoImage.IsAvailable = true;

            return downloaded = true;
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.ExpectationFailed)
        {
            logger.LogWarning("Image failed to download because the remote resource does not exist, is unavailable, or is not invalid/disallowed: {DownloadUrl} (Image={ImageID})", remoteUrl, image.ID);
            throw;
        }
        catch (Exception e)
        {
            shokoImage.ContentType = originalContentType;
            logger.LogWarning("Image failed to download due to an unexpected error: {DownloadUrl} - {Message} (Image={ImageID})", remoteUrl, e.Message, image.ID);
            throw;
        }
        finally
        {
            // Emit updated event if not downloaded, because the metadata changed.
            shokoImage.DownloadAttempts++;
            // On failure the file may have been deleted (forced re-download) or never written, so recompute.
            if (!downloaded)
                shokoImage.RefreshAvailability();
            shokoImage.LastUpdatedAt = DateTime.UtcNow;
            imageRepository.Save(shokoImage);
            if (downloaded)
            {
                _ = Task.Run(() => ImageDownloaded?.Invoke(this, new() { Image = image }));

                EmitEventForRelatedEntities(image, !previouslyDownloaded ? UpdateReason.ImageAdded : UpdateReason.ImageUpdated);
            }
            else
            {
                _ = Task.Run(() => ImageUpdated?.Invoke(this, new() { Image = image }));
            }
        }
    }

    /// <inheritdoc/>
    public async Task ScheduleDownloadOfImage(IImage image, bool force = false)
    {
        if (!force && image.IsAvailable)
            return;
        await schedulerFactory.EnqueueWithPriority<DownloadImageJob>(
            c => (c.Source, c.ResourceID, c.ForceDownload) = (image.Source, image.ResourceID, force),
            JobPriorities.ForImage(image.CrossReference?.EntityID.EntityType, image.Type, isNew: !force && image.DownloadAttempts is 0, prioritize: false)
        ).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IImage StoreFile(IImage image, byte[] file)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(file);
        if (imageRepository.GetByID(image.ID) is not { } shokoImage)
            throw new ArgumentException("The image is not stored.", nameof(image));

        if (GetImageMimeType(file) is not { } mimeType || !AllowedMimeTypes.Contains(mimeType))
            throw new UnsupportedImageTypeException()
            {
                ImageSource = image.Source,
                ImageResourceID = image.ResourceID,
                FileExtension = Path.GetExtension(image.ResourceID) ?? string.Empty,
                DetectedMimeType = "unknown",
            };

        if (!TryReadImage(file, out var width, out var height))
            throw new ArgumentException("The provided image data is not valid.", nameof(file));

        // Set the content type _before_ reading the local path, so the path gets the right extension.
        var previouslyHeld = shokoImage.RefreshAvailability();
        var originalContentType = shokoImage.ContentType;
        shokoImage.ContentType = mimeType;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(shokoImage.LocalPath)!);
            File.WriteAllBytes(shokoImage.LocalPath, file);
        }
        catch
        {
            shokoImage.ContentType = originalContentType;
            throw;
        }

        shokoImage.Width = width;
        shokoImage.Height = height;
        shokoImage.IsAvailable = true;
        shokoImage.LastUpdatedAt = DateTime.UtcNow;
        imageRepository.Save(shokoImage);
        logger.LogInformation("Image file stored from a file in hand. (Image={ImageID})", image.ID);

        _ = Task.Run(() => ImageDownloaded?.Invoke(this, new() { Image = shokoImage }));
        EmitEventForRelatedEntities(shokoImage, previouslyHeld ? UpdateReason.ImageUpdated : UpdateReason.ImageAdded);
        return shokoImage;
    }

    /// <inheritdoc/>
    public async Task ScheduleAutoDownloadsForEntity(
        IWithImages entity,
        MetadataSource? imageSource = null,
        ImageEntityType? imageType = null,
        MetadataSource? xrefSource = null,
        bool force = false
    )
    {
        var images = GetImagesForEntity(entity, new() { ImageSource = imageSource, ImageType = imageType, XrefSource = xrefSource, IsEnabled = true, IsDesired = true });
        foreach (var image in images)
        {
            if (!force && (image.IsAvailable || image.DownloadAttempts > 3))
                continue;

            await schedulerFactory.EnqueueWithPriority<DownloadImageJob>(
                c => (c.Source, c.ResourceID, c.ForceDownload) = (image.Source, image.ResourceID, force),
                JobPriorities.ForImage(entity.EntityType, image.Type, isNew: !force && image.DownloadAttempts is 0, prioritize: false)
            ).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task ScheduleAllAutoDownloads(
        MetadataSource? imageSource = null,
        ImageEntityType? imageType = null,
        MetadataSource? xrefSource = null,
        bool force = false,
        IProgress<decimal>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var options = new ImageFilteringOptions
        {
            ImageSource = imageSource,
            ImageType = imageType,
            XrefSource = xrefSource,
            IsEnabled = true,
            IsDesired = true,
        };
        var images = GetAllImages(options).ToList();
        var items = new ItemProgress(progress, images.Count);
        items.Report(0);
        foreach (var image in images)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (force || (!image.IsAvailable && image.DownloadAttempts <= 3))
            {
                await schedulerFactory.EnqueueWithPriority<DownloadImageJob>(
                    c => (c.Source, c.ResourceID, c.ForceDownload) = (image.Source, image.ResourceID, force),
                    JobPriorities.ForImage(image.CrossReference?.EntityID.EntityType, image.Type, isNew: false, prioritize: false)
                ).ConfigureAwait(false);
            }

            items.Increment();
        }
    }

    #endregion

    #region Image | Purge

    /// <inheritdoc/>
    public IEnumerable<IImage> GetOrphanedImages(int daysOld = 7, MetadataSource? imageSource = null)
    {
        var threshold = DateTime.UtcNow.AddDays(-daysOld);
        var images = imageRepository.GetOrphanedImages(threshold);
        if (imageSource is not null)
            images = images.Where(image => image.Source == imageSource).ToList();
        return images;
    }

    /// <inheritdoc/>
    public async Task<bool> PurgeImage(IImage image)
    {
        var updated = false;
        var xrefsToFix = xrefRepository.GetByPrimaryImageID(image.ID)
            .Where(xref => xref.ImageID != xref.PrimaryImageID)
            .ToList();
        if (xrefsToFix is { Count: > 0 })
        {
            updated = true;
            foreach (var xref in xrefsToFix)
                xref.PrimaryImageID = xref.ImageID;
            xrefRepository.Save(xrefsToFix);
            foreach (var xref in xrefsToFix)
            {
                _ = Task.Run(() => ImageCrossReferenceUpdated?.Invoke(this, new() { ImageCrossReference = xref }));
            }
        }

        var imagesToFix = imageRepository.GetByPrimaryImageID(image.ID)
            .Where(image => image.ID != image.PrimaryID)
            .ToList();
        if (imagesToFix is { Count: > 0 })
        {
            updated = true;
            foreach (var imageToFix in imagesToFix)
                imageToFix.PrimaryID = imageToFix.ID;
            imageRepository.Save(imagesToFix);
            foreach (var imageToFix in imagesToFix)
            {
                _ = Task.Run(() => ImageUpdated?.Invoke(this, new() { Image = imageToFix }));
            }
        }

        if (xrefRepository.GetByImageID(image.ID) is { Count: > 0 } xrefs)
        {
            updated = true;
            xrefRepository.Delete(xrefs);
            foreach (var xref in xrefs)
            {
                _ = Task.Run(() => ImageCrossReferenceRemoved?.Invoke(this, new() { ImageCrossReference = xref }));
            }
        }

        if (imageRepository.GetByID(image.ID) is { } localImage)
        {
            updated = true;
            imageRepository.Delete(localImage);
        }

        // The row goes first, so a file that cannot be deleted is left as a
        // stray file at worst, never as a row pointing at a missing file.
        var imagesPath = applicationPaths.ImagesPath;
        if (DeleteHeldFiles(imagesPath, ShokoImage.GetFolder(imagesPath, image.Source, image.ID), image.ID, logger) > 0)
            updated = true;

        _ = Task.Run(() => ImageRemoved?.Invoke(this, new() { Image = image }));

        return updated;
    }

    /// <summary>
    ///   Deletes the files held for an image: every file in its folder named
    ///   by its ID, whatever the extension, as a new download can change the
    ///   format. A missing file or folder is fine, a file that cannot be
    ///   deleted is logged and left, and a folder outside the images folder
    ///   is never touched.
    /// </summary>
    /// <param name="imagesPath">The images folder.</param>
    /// <param name="folder">The image's folder, from <see cref="ShokoImage.GetFolder"/>.</param>
    /// <param name="imageID">The image's ID.</param>
    /// <param name="logger">The logger for files left behind.</param>
    /// <returns>The number of files deleted.</returns>
    internal static int DeleteHeldFiles(string imagesPath, string folder, Guid imageID, ILogger logger)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(imagesPath)) + Path.DirectorySeparatorChar;
        var fullFolder = Path.GetFullPath(folder);
        if (!fullFolder.StartsWith(root, comparison))
        {
            logger.LogWarning("Refusing to delete the files of image {ImageID} in {Folder}, outside the images folder.", imageID, fullFolder);
            return 0;
        }

        if (!Directory.Exists(fullFolder))
            return 0;

        var id = imageID.ToString("N");
        var deleted = 0;
        foreach (var file in Directory.EnumerateFiles(fullFolder, id + "*"))
        {
            var name = Path.GetFileName(file);
            if (!string.Equals(name, id, comparison) && !name.StartsWith(id + ".", comparison))
                continue;

            try
            {
                File.Delete(file);
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not delete file {Path} of purged image {ImageID}.", file, imageID);
            }
        }

        return deleted;
    }

    /// <inheritdoc/>
    public async Task SchedulePurgeOfImage(IImage image)
    {
        await schedulerFactory.StartJob<PurgeImageJob>(c => (c.Source, c.ResourceID) = (image.Source, image.ResourceID)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> PurgeOrphanedImages(
        int daysOld = 7,
        MetadataSource? imageSource = null,
        IProgress<decimal>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var count = 0;
        var images = GetOrphanedImages(daysOld, imageSource).ToList();
        var items = new ItemProgress(progress, images.Count);
        items.Report(0);
        foreach (var image in images)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await PurgeImage(image).ConfigureAwait(false))
                count++;

            items.Increment();
        }

        return count;
    }

    /// <inheritdoc/>
    public async Task SchedulePurgeOfOrphanedImages(int daysOld = 7, MetadataSource? imageSource = null)
    {
        await schedulerFactory.StartJob<PurgeOrphanedImagesJob>(c => (c.DaysOld, c.ImageSource) = (daysOld, imageSource)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<int> ValidateAllImages()
        => ValidateAllImages(null, CancellationToken.None);

    /// <summary>
    ///   Validate local image cache integrity, as <see cref="ValidateAllImages()"/> does, reporting
    ///   progress and stopping when cancelled.
    /// </summary>
    /// <param name="progress">
    ///   Optional. Told how far the validation is, as a percentage from 0 to 100.
    /// </param>
    /// <param name="token">
    ///   Stops the validation between two images.
    /// </param>
    /// <returns>
    ///   The number of images queued for forced re-download.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///   <paramref name="token"/> was cancelled.
    /// </exception>
    public async Task<int> ValidateAllImages(IProgress<decimal>? progress, CancellationToken token)
    {
        var scanned = 0;
        var invalid = 0;
        var queuedForRedownload = 0;

        logger.LogInformation("Validating local image cache integrity.");
        var images = GetAllImages().ToList();
        progress?.Report(0);
        foreach (var image in images)
        {
            token.ThrowIfCancellationRequested();
            progress?.Report(100m * scanned / images.Count);
            if (scanned++ % 1000 == 0)
                logger.LogInformation("Image validation in progress. Scanned={Scanned}, Invalid={Invalid}, QueuedForRedownload={QueuedForRedownload}", scanned, invalid, queuedForRedownload);

            if (image is not ShokoImage shokoImage)
                continue;

            var wasAvailable = shokoImage.IsAvailable;
            // Recompute the cached flag from disk (File.Exists + magic-byte check).
            var available = shokoImage.RefreshAvailability();

            // Deep-validate the file contents and drop it if it's corrupt.
            if (available)
            {
                try
                {
                    // Only files recognized as a known image format are read by ImageMagick.
                    _ = GetImageMimeType(shokoImage.LocalPath) ?? throw new InvalidDataException("The file is not a known image.");
                    _ = new MagickImageInfo(shokoImage.LocalPath);
                }
                catch
                {
                    logger.LogWarning("Found invalid image. (Image={ImageID}, Source={Source}, ResourceID={ResourceID})", shokoImage.ID, shokoImage.Source, shokoImage.ResourceID);
                    invalid++;
                    if (File.Exists(shokoImage.LocalPath))
                        try { File.Delete(shokoImage.LocalPath); } catch { }
                    available = shokoImage.RefreshAvailability();
                }
            }

            // Persist a corrected flag and notify if the on-disk state differed from the cache.
            if (wasAvailable != shokoImage.IsAvailable)
            {
                shokoImage.LastUpdatedAt = DateTime.UtcNow;
                imageRepository.Save(shokoImage);
                _ = Task.Run(() => ImageUpdated?.Invoke(this, new() { Image = shokoImage }));
            }

            if (!available && shokoImage.IsEnabled && shokoImage.IsDesired)
            {
                await ScheduleDownloadOfImage(shokoImage, force: true).ConfigureAwait(false);
                queuedForRedownload++;
            }
        }

        progress?.Report(100);
        logger.LogInformation(
            "Image validation complete. Scanned={Scanned}, Invalid={Invalid}, QueuedForRedownload={QueuedForRedownload}",
            scanned,
            invalid,
            queuedForRedownload
        );
        return queuedForRedownload;
    }

    /// <inheritdoc/>
    public async Task ScheduleValidateAllImages(bool prioritize = true)
    {
        await schedulerFactory.StartJob<ValidateAllImagesJob>(prioritize: prioritize).ConfigureAwait(false);
    }

    #endregion

    #endregion

    #region Cross References

    /// <inheritdoc/>
    public event EventHandler<ImageCrossReferenceEventArgs>? ImageCrossReferenceAdded;

    /// <inheritdoc/>
    public event EventHandler<ImageCrossReferenceEventArgs>? ImageCrossReferenceUpdated;

    /// <inheritdoc/>
    public event EventHandler<ImageCrossReferenceEventArgs>? ImageCrossReferenceRemoved;

    /// <inheritdoc/>
    public IEnumerable<IImageCrossReference> GetAllImageCrossReferences(ImageCrossReferenceFilteringOptions? options = null)
    {
        var imageSource = options?.ImageSource;
        var imageType = options?.ImageType;
        var xrefSource = options?.XrefSource;
        var entitySource = options?.EntitySource;
        var entityType = options?.EntityType;
        var isEnabled = options?.IsEnabled;
        var isDesired = options?.IsDesired;
        var isPreferred = options?.IsPreferred;
        var isAvailable = options?.IsAvailable;
        var isPrimaryImage = options?.IsPrimaryImage;
        var isPrimaryAvailable = options?.IsPrimaryAvailable;
        IEnumerable<IImageCrossReference> xrefs = xrefRepository.GetAll();
        if (
            imageSource is not null ||
            imageType is not null ||
            xrefSource is not null ||
            entitySource is not null ||
            entityType is not null ||
            isEnabled is not null ||
            isDesired is not null ||
            isPreferred is not null ||
            isAvailable is not null ||
            isPrimaryImage is not null ||
            isPrimaryAvailable is not null
        )
        {
            xrefs = xrefs
                .Where(xref =>
                    (imageSource is null || xref.ImageSource == imageSource) &&
                    (imageType is null || xref.ImageType == imageType) &&
                    (xrefSource is null || xref.Source == xrefSource) &&
                    (entitySource is null || xref.EntityID.Source == entitySource) &&
                    (entityType is null || xref.EntityID.EntityType == entityType) &&
                    (isEnabled is null || xref.IsEnabled == isEnabled) &&
                    (isDesired is null || xref.IsDesired == isDesired) &&
                    (isPreferred is null || xref.IsPreferred == isPreferred) &&
                    (isAvailable is null || xref.IsAvailable == isAvailable) &&
                    (isPrimaryImage is null || xref.PrimaryImageID == xref.ImageID == isPrimaryImage) &&
                    (isPrimaryAvailable is null || xref.IsPrimaryAvailable == isPrimaryAvailable)
                );
        }
        return xrefs
            .OrderBy(xref => xref.ImageType)
            .ThenBy(xref => xref.Ordering);
    }

    /// <inheritdoc/>
    public IImageCrossReference? GetImageCrossReferenceByID(int crossReferenceID)
        => xrefRepository.GetByID(crossReferenceID);

    /// <inheritdoc/>
    public IImageCrossReference? GetRandomImageCrossReference(
        MetadataSource imageSource,
        ImageEntityType imageType,
        RandomImageCrossReferenceFilteringOptions? options = null
    )
        => xrefRepository.GetAll()
            .Where(xref =>
                (xref.ImageSource == imageSource) &&
                (xref.ImageType == imageType) &&
                (options?.XrefSource is null || xref.Source == options.XrefSource) &&
                (options?.EntitySource is null || xref.EntitySource == options.EntitySource) &&
                (options?.EntityType is null || xref.EntityType == options.EntityType) &&
                (options?.IsEnabled is null || xref.IsEnabled == options.IsEnabled) &&
                (options?.IsDesired is null || xref.IsDesired == options.IsDesired) &&
                (options?.IsPreferred is null || xref.IsPreferred == options.IsPreferred) &&
                (options?.IsAvailable is null || xref.IsAvailable == options.IsAvailable) &&
                (options?.IsPrimaryImage is null || xref.PrimaryImageID == xref.ImageID == options.IsPrimaryImage) &&
                (options?.IsPrimaryAvailable is null || xref.IsPrimaryAvailable == options.IsPrimaryAvailable)
            )
            .OrderByDescending(xref => xref.LastUpdatedAt)
            .GetRandomElement(Random.Shared);

    /// <inheritdoc/>
    public IReadOnlyList<IImageCrossReference> GetImageCrossReferencesForEntity(
        IWithImages entity,
        ImageCrossReferenceFilteringOptions? options = null
    )
    {
        var entityID = entity.ID;

        var imageSource = options?.ImageSource;
        var imageType = options?.ImageType;
        var xrefSource = options?.XrefSource;
        var isEnabled = options?.IsEnabled;
        var isDesired = options?.IsDesired;
        var isPreferred = options?.IsPreferred;
        var isAvailable = options?.IsAvailable;
        var isPrimaryImage = options?.IsPrimaryImage;
        var isPrimaryAvailable = options?.IsPrimaryAvailable;
        var linkedEntityImages = options?.LinkedEntityImages;
        Func<IEnumerable<IImageCrossReference>, IEnumerable<IImageCrossReference>> filter =
            imageSource is not null ||
            imageType is not null ||
            xrefSource is not null ||
            isEnabled is not null ||
            isDesired is not null ||
            isPreferred is not null ||
            isAvailable is not null ||
            isPrimaryImage is not null ||
            isPrimaryAvailable is not null
                ? xrefs => xrefs
                    .Where(xref =>
                        (imageSource is null || xref.ImageSource == imageSource) &&
                        (imageType is null || xref.ImageType == imageType) &&
                        (xrefSource is null || xref.Source == xrefSource) &&
                        (isEnabled is null || xref.IsEnabled == isEnabled) &&
                        (isDesired is null || xref.IsDesired == isDesired) &&
                        (isPreferred is null || xref.IsPreferred == isPreferred) &&
                        (isAvailable is null || xref.IsAvailable == isAvailable) &&
                        (isPrimaryImage is null || xref.PrimaryImageID == xref.ImageID == isPrimaryImage) &&
                        (isPrimaryAvailable is null || xref.IsPrimaryAvailable == isPrimaryAvailable)
                    )
                : xrefs => xrefs;

        linkedEntityImages ??= entity is IShokoGroup or IShokoSeries or ISeason<IShokoSeries, IShokoEpisode> or IShokoEpisode;
        if (linkedEntityImages.Value)
        {
            var xrefs = new List<IEnumerable<IImageCrossReference>>();
            var visitedEntities = new HashSet<MetadataGuid>();
            void AddEntryXrefs(IMetadata entry)
            {
                // The same entity can be reachable through more than one link, and we only want its images once.
                if (!visitedEntities.Add(entry.ID))
                    return;

                xrefs.Add(filter(xrefRepository.GetByEntity(entry.ID)).ToList());
            }

            void AddSeriesXrefs(IShokoSeries series)
            {
                AddEntryXrefs(series);
                foreach (var s in series.LinkedSeries)
                    AddEntryXrefs(s);
                foreach (var s in series.LinkedSeasons)
                    AddEntryXrefs(s);
                foreach (var m in series.LinkedMovies)
                    AddEntryXrefs(m);
            }

            AddEntryXrefs(entity);

            switch (entity)
            {
                case IShokoGroup group:
                {
                    AddSeriesXrefs(group.MainSeries);

                    // The main series can be without images of its own, e.g. an AniDB entry that has no poster, so fall back to
                    // the other series in the group instead of leaving the group without any images at all.
                    if (xrefs.All(list => !list.Any()))
                        foreach (var otherSeries in group.AllSeries)
                            AddSeriesXrefs(otherSeries);
                    break;
                }
                case IShokoSeries series:
                {
                    AddSeriesXrefs(series);
                    break;
                }
                case ISeason<IShokoSeries, IShokoEpisode> season:
                {
                    foreach (var s in season.LinkedSeasons)
                        AddEntryXrefs(s);
                    break;
                }
                case IShokoEpisode episode:
                {
                    foreach (var s in episode.LinkedEpisodes)
                        AddEntryXrefs(s);
                    foreach (var m in episode.LinkedMovies)
                        AddEntryXrefs(m);
                    break;
                }
            }

            return xrefs
                .SelectMany(list => list)
                .OrderBy(xref => xref.ImageType)
                .ThenBy(xref => xref.EntityID != entityID)
                .ThenByDescending(xref => IsLocallyMadeEntry(xref.EntityID.Source))
                // By source, kind and then the ID as text, as before the IDs became guids.
                .ThenBy(xref => xref.EntityID.Source)
                .ThenBy(xref => xref.EntityID.EntityType)
                .ThenBy(xref => xref.EntityID.ID)
                .ThenBy(xref => xref.Ordering)
                .ThenBy(xref => xref.Source)
                .ToList();
        }

        return filter(xrefRepository.GetByEntity(entityID))
            .OrderBy(xref => xref.ImageType)
            .ThenBy(xref => xref.Ordering)
            .ToList();
    }

    #region Cross References | Add

    /// <inheritdoc/>
    public IImageCrossReference AddImageCrossReference(IWithImages entity, IImage image, ImageCrossReferenceData imageCrossReferenceData)
    {
        if (imageRepository.GetByID(image.ID) is not { } localImage)
            throw new ArgumentException("Invalid image given to AddImageCrossReference", nameof(image));

        var xrefs = xrefRepository.GetByEntity(entity.ID);
        var existing = xrefs
            .FirstOrDefault(xref => xref.ImageID == image.ID && xref.ImageType == imageCrossReferenceData.ImageType && xref.Source == imageCrossReferenceData.Source);
        if (existing is not null)
            throw new ImageCrossReferenceExistsException()
            {
                CrossReference = existing,
                Image = image,
                Entity = entity,
            };

        var xref = new ShokoImage_Entity(image, entity, imageCrossReferenceData, xrefs.Count);
        localImage.LastUpdatedAt = xref.LastUpdatedAt;

        xrefRepository.Save(xref);
        imageRepository.Save(localImage);

        if (imageCrossReferenceData.IsPreferred is true)
        {
            var siblingXrefs = xrefRepository
                .GetByEntity(xref.EntitySource, xref.EntityType, xref.EntityID)
                .Where(x => x.ID != xref.ID && x.ImageType == xref.ImageType && x.IsPreferred)
                .ToList();
            foreach (var siblingXref in siblingXrefs)
            {
                siblingXref.Update(new() { IsPreferred = false }, entity: null);
                xrefRepository.Save(siblingXref);
                if (imageRepository.GetByID(siblingXref.ImageID) is { } siblingImage)
                {
                    siblingImage.LastUpdatedAt = siblingXref.LastUpdatedAt;
                    imageRepository.Save(siblingImage);
                    Task.Run(() => ImageUpdated?.Invoke(this, new() { Image = siblingImage }));
                }

                Task.Run(() => ImageCrossReferenceUpdated?.Invoke(this, new() { ImageCrossReference = siblingXref }));
                EmitEventForRelatedEntry(siblingXref, UpdateReason.ImageUpdated);
            }
        }

        Task.Run(() => ImageUpdated?.Invoke(this, new() { Image = localImage }));
        Task.Run(() => ImageCrossReferenceAdded?.Invoke(this, new() { ImageCrossReference = xref }));

        EmitEventForRelatedEntry(xref, UpdateReason.ImageAdded);

        return xref;
    }

    #endregion

    #region Cross References | Update

    /// <inheritdoc/>
    public IImageCrossReference SetPreferredImageForEntity(IWithImages entity, ImageEntityType imageType, IImage image)
        => GetImageCrossReferencesForEntity(entity, new() { ImageType = imageType, LinkedEntityImages = false }).FirstOrDefault(xref => xref.ImageID == image.ID) is { } xref
            ? xref.IsPreferred && xref.IsEnabled && xref.IsDesired ? xref : UpdateImageCrossReference(xref, new() { IsPreferred = true, IsEnabled = true, IsDesired = true })
            : AddImageCrossReference(entity, image, new() { ImageType = imageType, IsPreferred = true, IsEnabled = true, IsDesired = true });

    /// <inheritdoc/>
    public IImageCrossReference SetPreferredImageForEntity(IImageCrossReference imageCrossReference)
        => imageCrossReference.IsPreferred && imageCrossReference.IsEnabled && imageCrossReference.IsDesired ? imageCrossReference : UpdateImageCrossReference(imageCrossReference, new() { IsPreferred = true, IsEnabled = true, IsDesired = true });

    /// <inheritdoc/>
    public bool UnsetPreferredImageForEntity(IImageCrossReference imageCrossReference)
        => !imageCrossReference.IsPreferred || UpdateImageCrossReference(imageCrossReference, new() { IsPreferred = false }) is { IsPreferred: false };

    /// <inheritdoc/>
    public bool UnsetAllPreferredImagesForEntity(IWithImages entity)
    {
        var xrefs = GetImageCrossReferencesForEntity(entity, new() { LinkedEntityImages = false });
        if (xrefs.Count is 0)
            return true;

        var unset = true;
        foreach (var xref in xrefs)
        {
            if (!UnsetPreferredImageForEntity(xref))
                unset = false;
        }
        return unset;
    }

    /// <inheritdoc/>
    public IImageCrossReference UpdateImageCrossReference(IImageCrossReference imageCrossReference, ImageCrossReferenceUpdateData imageCrossReferenceUpdateData)
    {
        if (xrefRepository.GetByID(imageCrossReference.ID) is not ShokoImage_Entity localCrossReference)
            throw new ArgumentException("Invalid image cross-reference given to UpdateImageCrossReference", nameof(imageCrossReference));

        var updated = localCrossReference.Update(imageCrossReferenceUpdateData, entity: null);
        if (imageCrossReferenceUpdateData.IsPreferred is true)
        {
            var siblingXrefs = xrefRepository
                .GetByEntity(localCrossReference.EntitySource, localCrossReference.EntityType, localCrossReference.EntityID)
                .Where(xref => xref.ID != localCrossReference.ID && xref.ImageType == localCrossReference.ImageType && xref.IsPreferred)
                .ToList();
            foreach (var siblingXref in siblingXrefs)
            {
                siblingXref.Update(new() { IsPreferred = false }, entity: null);
                xrefRepository.Save(siblingXref);
                if (imageRepository.GetByID(siblingXref.ImageID) is { } siblingImage)
                {
                    siblingImage.LastUpdatedAt = siblingXref.LastUpdatedAt;
                    imageRepository.Save(siblingImage);
                    Task.Run(() => ImageUpdated?.Invoke(this, new() { Image = siblingImage }));
                }

                Task.Run(() => ImageCrossReferenceUpdated?.Invoke(this, new() { ImageCrossReference = siblingXref }));
                EmitEventForRelatedEntry(siblingXref, UpdateReason.ImageUpdated);
            }
        }

        if (!updated)
            return localCrossReference;

        xrefRepository.Save(localCrossReference);

        if (imageRepository.GetByID(localCrossReference.ImageID) is { } localImage)
        {
            localImage.LastUpdatedAt = localCrossReference.LastUpdatedAt;
            imageRepository.Save(localImage);
            Task.Run(() => ImageUpdated?.Invoke(this, new() { Image = localImage }));
        }

        Task.Run(() => ImageCrossReferenceUpdated?.Invoke(this, new() { ImageCrossReference = localCrossReference }));
        EmitEventForRelatedEntry(localCrossReference, UpdateReason.ImageUpdated);

        return localCrossReference;
    }

    #endregion

    #region Cross References | Remove

    /// <inheritdoc/>
    public bool RemoveImageCrossReference(IImageCrossReference imageCrossReference)
    {
        if (xrefRepository.GetByID(imageCrossReference.ID) is not { } localCrossReference)
            return false;

        xrefRepository.Delete(localCrossReference);

        if (imageRepository.GetByID(imageCrossReference.ImageID) is { } localImage)
        {
            localImage.LastUpdatedAt = DateTime.UtcNow;
            imageRepository.Save(localImage);
            Task.Run(() => ImageUpdated?.Invoke(this, new() { Image = localImage }));
        }

        Task.Run(() => ImageCrossReferenceRemoved?.Invoke(this, new() { ImageCrossReference = localCrossReference }));

        EmitEventForRelatedEntry(localCrossReference, UpdateReason.ImageRemoved);

        return true;
    }

    #endregion

    #endregion

    #region Helpers

    /// <summary>
    ///   Whether an entry of a source is made on this server by a user or a
    ///   plugin, like a user's ordering, so its images rank before those of
    ///   the linked remote entries. The server's own entries, of
    ///   <see cref="MetadataSource.Shoko"/>, keep their place after them.
    /// </summary>
    /// <param name="source">The entry's source.</param>
    /// <returns><c>true</c> if the entry is made locally.</returns>
    private static bool IsLocallyMadeEntry(MetadataSource source)
        => source.IsLocal && source != MetadataSource.Shoko;

    /// <inheritdoc/>
    public bool IsLinkedCrossReference(IWithImages entity, IImageCrossReference xref)
        => xref.EntityID == entity.ID;

    /// <inheritdoc/>
    public IWithImages? GetEntityForImage(MetadataGuid entityID)
    {
        ArgumentNullException.ThrowIfNull(entityID);

        // The orderings of the core's sources other than the users' keep what
        // their source gives them, so images are never linked to them.
        if (entityID.EntityType == MetadataEntityType.Ordering && entityID.Source.IsCore && entityID.Source != MetadataSource.User)
            return null;

        // A default ordering is never stored, so nothing would remove an image link
        // to it once its series is gone.
        return metadataService.Value.GetEntry(entityID) is IWithImages withImages and not IOrdering { IsDefault: true } ? withImages : null;
    }

    public static bool IsImageValid(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        try
        {
            return GetImageMimeType(path) is not null;
        }
        catch
        {
            return false;
        }
    }

    private void EmitEventForRelatedEntities(IImage image, UpdateReason reason)
    {
        foreach (var xref in image.GetCrossReferences(isEnabled: true))
            EmitEventForRelatedEntry(xref, reason);
    }

    private void EmitEventForRelatedEntry(IImageCrossReference xref, UpdateReason reason)
    {
        switch (xref.GetEntity())
        {
            case IMovie movie:
                ShokoEventHandler.Instance.OnMovieUpdated(movie, reason);
                break;

            case ISeries show:
                ShokoEventHandler.Instance.OnSeriesUpdated(show, reason);
                break;

            case ISeason season:
            {
                if (season.Series is not { } series)
                    return;

                ShokoEventHandler.Instance.OnSeasonUpdated(series, season, reason);
                break;
            }

            case IEpisode episode:
            {
                if (episode.Series is not { } series)
                    return;

                ShokoEventHandler.Instance.OnEpisodeUpdated(series, episode, reason);
                break;
            }
        }
    }

    /// <summary>
    ///   How many bytes from the start of a file are enough to recognize its
    ///   image format.
    /// </summary>
    private const int SniffLength = 4096;

    /// <summary>
    ///   Finds an image's media type from the start of its file, by the format
    ///   ImageMagick recognizes from its signature.
    /// </summary>
    /// <param name="data">
    ///   The start of the file; <see cref="SniffLength"/> bytes are enough.
    /// </param>
    /// <returns>
    ///   The media type, or <c>null</c> when the data is not a known image.
    /// </returns>
    public static string? GetImageMimeType(ReadOnlySpan<byte> data)
        => !data.IsEmpty && MagickFormatInfo.Create(data.ToArray()) is { } info ? GetMimeType(info.Format) : null;

    /// <summary>
    ///   The media type of an image format the image system takes.
    /// </summary>
    /// <param name="format">The format, as ImageMagick names it.</param>
    /// <returns>The media type, or <c>null</c> for any other format.</returns>
    public static string? GetMimeType(MagickFormat format)
        => format switch
        {
            MagickFormat.Png or MagickFormat.Png00 or MagickFormat.Png8 or MagickFormat.Png24 or MagickFormat.Png32 or MagickFormat.Png48 or MagickFormat.Png64 => "image/png",
            MagickFormat.Jpg or MagickFormat.Jpeg => "image/jpeg",
            MagickFormat.WebP => "image/webp",
            MagickFormat.Gif or MagickFormat.Gif87 => "image/gif",
            MagickFormat.Bmp or MagickFormat.Bmp2 or MagickFormat.Bmp3 => "image/bmp",
            MagickFormat.Tif or MagickFormat.Tiff or MagickFormat.Tiff64 => "image/tiff",
            MagickFormat.Svg or MagickFormat.Svgz => "image/svg+xml",
            _ => null,
        };

    /// <summary>
    ///   Finds the media type of an image file from its first bytes.
    /// </summary>
    /// <param name="path">The file's path.</param>
    /// <returns>
    ///   The media type, or <c>null</c> when the file is not a known image.
    /// </returns>
    /// <exception cref="IOException">The file could not be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The file may not be read.</exception>
    private static string? GetImageMimeType(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        Span<byte> buffer = stackalloc byte[SniffLength];
        var read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
        return GetImageMimeType(buffer[..read]);
    }

    /// <summary>
    ///   Checks an image's data and reads its size through ImageMagick.
    /// </summary>
    /// <param name="data">The image's data.</param>
    /// <param name="width">The width in pixels, if known.</param>
    /// <param name="height">The height in pixels, if known.</param>
    /// <returns><c>true</c> if the data is a valid image.</returns>
    internal static bool TryReadImage(byte[] data, out int? width, out int? height)
    {
        try
        {
            var info = new MagickImageInfo(data);
            width = (int)info.Width;
            height = (int)info.Height;
            return true;
        }
        catch (MagickException)
        {
            width = null;
            height = null;
            return false;
        }
    }

    private static readonly string[] _dataUrlSeparators = [":", ";", ","];

    public static void TryConvertFromDataURL(
        ref byte[] imageByteArray,
        ref string? contentType
    )
    {
        if (imageByteArray.Length < 16 || imageByteArray[0] != 'd' || imageByteArray[1] != 'a' || imageByteArray[2] != 't' || imageByteArray[3] != 'a' || imageByteArray[4] != ':')
            return;

        var parts = Encoding.UTF8.GetString(imageByteArray).Split(_dataUrlSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || parts[0] != "data")
            throw new ArgumentException("Invalid data URL format.");

        try
        {
            imageByteArray = Convert.FromBase64String(parts[3]);
            contentType = parts[1];
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Base64 data is not in a correct format.", ex);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Unexpected error when converting data URL to byte array.", ex);
        }
    }

    #endregion
}
