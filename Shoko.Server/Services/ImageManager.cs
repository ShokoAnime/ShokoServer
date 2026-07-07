using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Metadata.Tmdb.CrossReferences;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Video;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.AniDB.Embedded;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.Shoko.Embedded;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Repositories.Direct.TMDB;
using Shoko.Server.Repositories.Direct.TMDB.Optional;
using Shoko.Server.Scheduling.Jobs.Image;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Services;

public partial class ImageManager(
    ILogger<ImageManager> logger,
    IApplicationPaths applicationPaths,
    ISettingsProvider settingsProvider,
    IQueueScheduler schedulerFactory,
    IServiceProvider services,
    IHttpClientFactory httpClientFactory,
    ConfigurationProvider<ServerSettings> configurationProvider,
    ShokoImageRepository imageRepository,
    ShokoImage_EntityRepository xrefRepository,
    AnimeGroupRepository _animeGroups,
    AnimeSeriesRepository _animeSeries,
    AnimeEpisodeRepository _animeEpisodes,
    VideoLocalRepository _videoLocals,
    JMMUserRepository _jmmUsers,
    AniDB_AnimeRepository _anidbAnimes,
    AniDB_EpisodeRepository _anidbEpisodes,
    AniDB_CreatorRepository _anidbCreators,
    AniDB_CharacterRepository _anidbCharacters,
    TMDB_CollectionRepository _tmdbCollections,
    TMDB_MovieRepository _tmdbMovies,
    TMDB_ShowRepository _tmdbShows,
    TMDB_AlternateOrdering_SeasonRepository _tmdbAlternateOrderingSeasons,
    TMDB_SeasonRepository _tmdbSeasons,
    TMDB_EpisodeRepository _tmdbEpisodes,
    TMDB_PersonRepository _tmdbPersons,
    TMDB_CompanyRepository _tmdbCompanies,
    TMDB_NetworkRepository _tmdbNetworks
) : IImageManager
{
    private static IUDPConnectionHandler? _udpConnectionHandler = null;

    #region Image Sources

    private Dictionary<DataSource, string?>? _cachedUrls = null;

    /// <inheritdoc/>
    public IReadOnlyDictionary<DataSource, string?> GetTemplateUrls()
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
            var dict = new Dictionary<DataSource, string?>();
            foreach (var dataSource in Enum.GetValues<DataSource>())
            {
                if (dataSource.IsLocal)
                    continue;
                if (userRegisteredTemplates.TryGetValue(dataSource, out var templateUrl) && templateUrl is { Length: > 0 })
                    dict.Add(dataSource, templateUrl);
                else if (dataSource is DataSource.AniDB)
                    dict.Add(dataSource, DefaultAnidbUrlTemplate());
                else if (dataSource is DataSource.TMDB)
                    dict.Add(dataSource, DefaultTmdbUrlTemplate());
                else if (dataSource is DataSource.AniList)
                    // TODO: Add Anilist image template url.
                    dict.Add(dataSource, null);
                else
                    dict.Add(dataSource, null);
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
        _udpConnectionHandler ??= services?.GetRequiredService<IUDPConnectionHandler>();
        if (_udpConnectionHandler is not null)
            return _udpConnectionHandler.ImageServerUrl;

        // Static fallback.
        return string.Format(Constants.URLS.AniDB_Images, Constants.AnidbCdnUrl);
    }

    private string DefaultTmdbUrlTemplate()
    {
        // Setting override.
        var setting = settingsProvider.GetSettings().TMDB.ImageCdnUrl;
        if (!string.IsNullOrWhiteSpace(setting) && !string.Equals(setting, TmdbMetadataService.ImageServerUrl) && (setting.StartsWith("http://") || setting.StartsWith("https://")))
        {
            // Setting as a URL template.
            if (setting.Contains("{0}"))
                return setting;

            // Setting as a base URL.
            if (!setting.EndsWith("/", StringComparison.Ordinal))
                setting += "/";
            return $"{setting}original/{{0}}";
        }

        // Static fallback.
        return $"{TmdbMetadataService.ImageServerUrl}original/{{0}}";
    }

    /// <inheritdoc/>
    public string? GetTemplateUrlForSource(DataSource imageSource)
    {
        var urls = GetTemplateUrls();
        return urls.TryGetValue(imageSource, out var url) ? url : null;
    }

    /// <inheritdoc/>
    public void SetTemplateUrlForSource(DataSource imageSource, string? templateUrl)
    {
        if (imageSource.IsLocal)
            throw new InvalidOperationException($"{nameof(imageSource)} cannot be User, None or Shoko.");

        if (templateUrl is not null)
        {
            var urlErrors = new List<string>();
            if (!Uri.TryCreate(templateUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttp || uri.Scheme != Uri.UriSchemeHttps)
                throw new ArgumentException($"{nameof(templateUrl)} must be a valid http:// or https:// URL.", nameof(templateUrl));
            if (!templateUrl.Contains("{0}"))
                throw new ArgumentException($"{nameof(templateUrl)} must contain {{0}}.", nameof(templateUrl));

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
                if (_cachedUrls is not null)
                    _cachedUrls.Remove(imageSource);
            }
        }
    }

    #endregion

    #region Image Cross Reference Resolvers

    private List<IImageCrossReferenceResolver> _resolvers = [];

    /// <inheritdoc/>
    public IReadOnlyList<IImageCrossReferenceResolver> ImageCrossReferenceResolvers => _resolvers;

    /// <inheritdoc/>
    public void AddParts(IEnumerable<IImageCrossReferenceResolver> resolvers)
    {
        _resolvers = resolvers.ToList();
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
        if (!TryGetMetadataForEntity(entity, out var entitySource, out var entityType, out var entityID, out _, out _, out _))
            throw new ArgumentException(nameof(entity), "Invalid entity given to GetImagesForEntity");

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

        linkedEntityImages ??= entity is IShokoGroup or IShokoSeries or IShokoSeason or IShokoEpisode;
        if (linkedEntityImages.Value)
        {
            var xrefs = new List<IEnumerable<IImageCrossReference>>()
            {
                filter(xrefRepository.GetByEntity(entitySource, entityType, entityID)),
            };

            switch (entity)
            {
                case IShokoGroup group:
                {
                    var series = group.MainSeries;
                    xrefs.Add(filter(xrefRepository.GetByEntity(series.Source, series.EntityType, series.ID.ToString())));
                    foreach (var s in series.LinkedSeries)
                        xrefs.Add(filter(xrefRepository.GetByEntity(s.Source, s.EntityType, s.ID.ToString())));
                    foreach (var s in series.TmdbSeasons)
                        xrefs.Add(filter(xrefRepository.GetByEntity(s.Source, s.EntityType, s.ID)));
                    foreach (var m in series.LinkedMovies)
                        xrefs.Add(filter(xrefRepository.GetByEntity(m.Source, m.EntityType, m.ID.ToString())));
                    break;
                }
                case IShokoSeries series:
                {
                    foreach (var s in series.LinkedSeries)
                        xrefs.Add(filter(xrefRepository.GetByEntity(s.Source, s.EntityType, s.ID.ToString())));
                    foreach (var s in series.TmdbSeasons)
                        xrefs.Add(filter(xrefRepository.GetByEntity(s.Source, s.EntityType, s.ID)));
                    foreach (var m in series.LinkedMovies)
                        xrefs.Add(filter(xrefRepository.GetByEntity(m.Source, m.EntityType, m.ID.ToString())));
                    break;
                }
                case IShokoSeason season:
                {
                    foreach (var s in season.LinkedSeasons)
                        xrefs.Add(filter(xrefRepository.GetByEntity(s.Source, s.EntityType, s.ID)));
                    break;
                }
                case IShokoEpisode episode:
                {
                    foreach (var s in episode.LinkedEpisodes)
                        xrefs.Add(filter(xrefRepository.GetByEntity(s.Source, s.EntityType, s.ID.ToString())));
                    foreach (var m in episode.LinkedMovies)
                        xrefs.Add(filter(xrefRepository.GetByEntity(m.Source, m.EntityType, m.ID.ToString())));
                    break;
                }
            }

            return xrefs
                .SelectMany(list => list)
                .Select(xref => (xref, image: GetImageByID(xref.ImageID, primaryImage)!, linkedXref: (xref.EntitySource, xref.EntityType, xref.EntityID) != (entitySource, entityType, entityID)))
                .Where(tuple => tuple.image is not null)
                .OrderBy(tuple => tuple.xref.ImageType)
                .ThenBy(tuple => tuple.linkedXref)
                .ThenByDescending(tuple => tuple.xref.EntitySource is DataSource.User or DataSource.LocallyGenerated)
                .ThenBy(tuple => tuple.xref.EntitySource)
                .ThenBy(tuple => tuple.xref.EntityType)
                .ThenBy(tuple => tuple.xref.EntityID)
                .ThenBy(tuple => tuple.xref.Ordering)
                .ThenBy(tuple => tuple.xref.Source)
                .DistinctBy(tuple => (tuple.image.ID, tuple.xref.ImageType))
                .Select(tuple => ImageStub.Wrap(tuple.image, tuple.xref, tuple.linkedXref))
                .ToList();
        }

        return filter(xrefRepository.GetByEntity(entitySource, entityType, entityID))
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
    public IImage? GetImageBySourceAndRemoteResourceID(DataSource source, string resourceID, bool primaryImage = false)
        => GetImageByID(IImageManager.GetIDForImageSourceAndResourceID(source, resourceID), primaryImage);

    /// <inheritdoc/>
    public IShokoSeries? GetFirstSeriesForImage(IImage image)
        => xrefRepository.GetByImageID(image.ID)
            .Where(xref => xref is
            {
                IsEnabled: true,
                EntityType: DataEntityType.Movie or DataEntityType.Series or DataEntityType.Season or DataEntityType.Episode,
            })
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

    public IReadOnlyList<string> AllowedMimeTypes { get; private set; } = ["image/jpeg", "image/png", "image/bmp", "image/gif", "image/tiff", "image/webp"];

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
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        return UploadImage(imageStream.ToByteArray(), contentType, userSubmitted);
    }

    /// <inheritdoc/>
    public IImage UploadImage(byte[] imageByteArray, string? contentType = null, bool userSubmitted = true)
    {
        ArgumentNullException.ThrowIfNull(imageByteArray);

        TryConvertFromDataURL(ref imageByteArray, ref contentType);

        var source = userSubmitted ? DataSource.User : DataSource.LocallyGenerated;
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

        MagickImageInfo info;
        try
        {
            info = new(imageByteArray);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("The provided image data is not valid.", nameof(imageByteArray), ex);
        }

        var expectedContentType = "image/" + info.Format.ToString().ToLower().Replace("jpg", "jpeg");
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
            Height = (int)info.Height,
            Width = (int)info.Width,
            ContentType = expectedContentType,
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
        };

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(image.LocalPath)!);
            File.OpenWrite(image.LocalPath).Write(imageByteArray);
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
    public string? GetContentTypeFromResourceID(DataSource source, string resourceID)
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
            var bytes = new byte[12];
            stream.ReadExactly(bytes);
            stream.Close();
            return GetImageFormat(bytes) is not null;
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
            if (GetImageFormat(byteArray) is not { } imageFormat)
                throw new UnsupportedImageTypeException()
                {
                    ImageSource = image.Source,
                    ImageResourceID = image.ResourceID,
                    FileExtension = Path.GetExtension(image.ResourceID) ?? string.Empty,
                    DetectedMimeType = "unknown",
                };

            MagickImageInfo info;
            try
            {
                info = new(byteArray);
            }
            catch (MagickException e)
            {
                throw new HttpRequestException($"Invalid or disallowed image data format at remote resource: {remoteUrl}", e, HttpStatusCode.ExpectationFailed);
            }

            // Set the content type _before_ accessing the local path, so the local path will have the correct extension.
            shokoImage.ContentType = $"image/{imageFormat}";

            Directory.CreateDirectory(Path.GetDirectoryName(image.LocalPath)!);
            if (File.Exists(image.LocalPath))
                File.Delete(image.LocalPath);
            File.WriteAllBytes(image.LocalPath, byteArray);

            logger.LogInformation("Image downloaded to cache: {DownloadUrl} (Image={ImageID})", remoteUrl, image.ID);

            // Update metadata after successfully storing the file.
            shokoImage.Width = (int)info.Width;
            shokoImage.Height = (int)info.Height;
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
        await schedulerFactory.StartJob<DownloadImageJob>(c => (c.Source, c.ResourceID, c.ForceDownload) = (image.Source, image.ResourceID, force)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ScheduleAutoDownloadsForEntity(
        IWithImages entity,
        DataSource? imageSource = null,
        ImageEntityType? imageType = null,
        DataSource? xrefSource = null,
        bool force = false
    )
    {
        var images = GetImagesForEntity(entity, new() { ImageSource = imageSource, ImageType = imageType, XrefSource = xrefSource, IsEnabled = true, IsDesired = true });
        foreach (var image in images)
        {
            if (!force && (image.IsAvailable || image.DownloadAttempts > 3))
                continue;

            await schedulerFactory.StartJob<DownloadImageJob>(c => (c.Source, c.ResourceID, c.ForceDownload) = (image.Source, image.ResourceID, force)).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task ScheduleAllAutoDownloads(
        DataSource? imageSource = null,
        ImageEntityType? imageType = null,
        DataSource? xrefSource = null,
        bool force = false
    )
    {
        var images = GetAllImages(new() { ImageSource = imageSource, ImageType = imageType, XrefSource = xrefSource, IsEnabled = true, IsDesired = true });
        foreach (var image in images)
        {
            if (!force && (image.IsAvailable || image.DownloadAttempts > 3))
                continue;

            await schedulerFactory.StartJob<DownloadImageJob>(c => (c.Source, c.ResourceID, c.ForceDownload) = (image.Source, image.ResourceID, force)).ConfigureAwait(false);
        }
    }

    #endregion

    #region Image | Purge

    /// <inheritdoc/>
    public IEnumerable<IImage> GetOrphanedImages(int daysOld = 7, DataSource? imageSource = null)
    {
        var threshold = DateTime.UtcNow.AddDays(-daysOld);
        var images = imageRepository.GetOrphanedImages(threshold);
        if (imageSource.HasValue)
            images = images.Where(image => image.Source == imageSource.Value).ToList();
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

        _ = Task.Run(() => ImageRemoved?.Invoke(this, new() { Image = image }));

        return updated;
    }

    /// <inheritdoc/>
    public async Task SchedulePurgeOfImage(IImage image)
    {
        await schedulerFactory.StartJob<PurgeImageJob>(c => (c.Source, c.ResourceID) = (image.Source, image.ResourceID)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<int> PurgeOrphanedImages(int daysOld = 7, DataSource? imageSource = null)
    {
        var count = 0;
        var threshold = DateTime.UtcNow.AddDays(-daysOld);
        var imagesToPurge = imageRepository.GetOrphanedImages(threshold);
        foreach (var image in imagesToPurge)
        {
            if (await PurgeImage(image).ConfigureAwait(false))
                count++;
        }
        return count;
    }

    /// <inheritdoc/>
    public async Task SchedulePurgeOfOrphanedImages(int daysOld = 7, DataSource? imageSource = null)
    {
        await schedulerFactory.StartJob<PurgeOrphanedImagesJob>(c => (c.DaysOld, c.ImageSource) = (daysOld, imageSource)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<int> ValidateAllImages()
    {
        var scanned = 0;
        var invalid = 0;
        var queuedForRedownload = 0;

        logger.LogInformation("Validating local image cache integrity.");
        foreach (var image in GetAllImages())
        {
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
                    new MagickImageInfo(shokoImage.LocalPath);
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
                    (entitySource is null || xref.EntitySource == entitySource) &&
                    (entityType is null || xref.EntityType == entityType) &&
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
        DataSource imageSource,
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
        if (!TryGetMetadataForEntity(entity, out var entitySource, out var entityType, out var entityID, out _, out _, out _))
            throw new ArgumentException("Invalid entity given to GetImagesForEntity", nameof(entity));

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

        linkedEntityImages ??= entity is IShokoGroup or IShokoSeries or IShokoSeason or IShokoEpisode;
        if (linkedEntityImages.Value)
        {
            var xrefs = new List<IEnumerable<IImageCrossReference>>()
            {
                filter(xrefRepository.GetByEntity(entitySource, entityType, entityID)),
            };

            switch (entity)
            {
                case IShokoGroup group:
                {
                    var series = group.MainSeries;
                    xrefs.Add(filter(xrefRepository.GetByEntity(series.Source, series.EntityType, series.ID.ToString())));
                    foreach (var s in series.LinkedSeries)
                        xrefs.Add(filter(xrefRepository.GetByEntity(s.Source, s.EntityType, s.ID.ToString())));
                    foreach (var s in series.TmdbSeasons)
                        xrefs.Add(filter(xrefRepository.GetByEntity(s.Source, s.EntityType, s.ID)));
                    foreach (var m in series.LinkedMovies)
                        xrefs.Add(filter(xrefRepository.GetByEntity(m.Source, m.EntityType, m.ID.ToString())));
                    break;
                }
                case IShokoSeries series:
                {
                    foreach (var s in series.LinkedSeries)
                        xrefs.Add(filter(xrefRepository.GetByEntity(s.Source, s.EntityType, s.ID.ToString())));
                    foreach (var s in series.TmdbSeasons)
                        xrefs.Add(filter(xrefRepository.GetByEntity(s.Source, s.EntityType, s.ID)));
                    foreach (var m in series.LinkedMovies)
                        xrefs.Add(filter(xrefRepository.GetByEntity(m.Source, m.EntityType, m.ID.ToString())));
                    break;
                }
                case IShokoSeason season:
                {
                    foreach (var s in season.LinkedSeasons)
                        xrefs.Add(filter(xrefRepository.GetByEntity(s.Source, s.EntityType, s.ID)));
                    break;
                }
                case IShokoEpisode episode:
                {
                    foreach (var s in episode.LinkedEpisodes)
                        xrefs.Add(filter(xrefRepository.GetByEntity(s.Source, s.EntityType, s.ID.ToString())));
                    foreach (var m in episode.LinkedMovies)
                        xrefs.Add(filter(xrefRepository.GetByEntity(m.Source, m.EntityType, m.ID.ToString())));
                    break;
                }
            }

            return xrefs
                .SelectMany(list => list)
                .OrderBy(xref => xref.ImageType)
                .ThenBy(xref => (xref.EntitySource, xref.EntityType, xref.EntityID) != (entitySource, entityType, entityID))
                .ThenByDescending(xref => xref.EntitySource is DataSource.User or DataSource.LocallyGenerated)
                .ThenBy(xref => xref.EntitySource)
                .ThenBy(xref => xref.EntityType)
                .ThenBy(xref => xref.EntityID)
                .ThenBy(xref => xref.Ordering)
                .ThenBy(xref => xref.Source)
                .ToList();
        }

        return filter(xrefRepository.GetByEntity(entitySource, entityType, entityID))
            .OrderBy(xref => xref.ImageType)
            .ThenBy(xref => xref.Ordering)
            .ToList();
    }

    #region Cross References | Add

    /// <inheritdoc/>
    public IImageCrossReference AddImageCrossReference(IWithImages entity, IImage image, ImageCrossReferenceData imageCrossReferenceData)
    {
        if (!TryGetMetadataForEntity(entity, out var entitySource, out var entityType, out var entityID, out _, out _, out _))
            throw new ArgumentException("Invalid entity given to AddImageCrossReference", nameof(entity));

        if (imageRepository.GetByID(image.ID) is not { } localImage)
            throw new ArgumentException("Invalid image given to AddImageCrossReference", nameof(image));

        var xrefs = xrefRepository.GetByEntity(entitySource, entityType, entityID);
        var existing = xrefs
            .FirstOrDefault(xref => xref.ImageID == image.ID && xref.ImageType == imageCrossReferenceData.ImageType && xref.Source == imageCrossReferenceData.Source);
        if (existing is not null)
            throw new ImageCrossReferenceExistsException()
            {
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

    public bool TryGetMetadataForEntity(
        IWithImages entity,
        out DataSource entitySource,
        out DataEntityType entityType,
        [NotNullWhen(true)] out string? entityID,
        out int? entitySeasonNumber,
        out int? entityEpisodeNumber,
        out DateOnly? releasedAt
    )
    {
        entitySource = entity.Source;
        entityType = entity.EntityType;
        entityID = null;
        entitySeasonNumber = null;
        entityEpisodeNumber = null;
        releasedAt = null;
        switch (entity)
        {
            case ICollection collection:
                entityID = collection.ID.ToString();
                return true;

            case IMovie movie:
                entityID = movie.ID.ToString();
                releasedAt = movie.ReleaseDate?.ToDateOnly();
                return true;

            case ISeries series:
                entityID = series.ID.ToString();
                releasedAt = series.AirDate?.IsComplete ?? false ? series.AirDate.Value.ToDateOnly() : null;
                return true;

            case ISeason season:
                entityID = season.ID;
                entitySeasonNumber = season.SeasonNumber;
                releasedAt = season.Episodes
                    .Select(o => o.AirDate)
                    .WhereNotNull()
                    .Order()
                    .FirstOrDefault();
                return true;

            case IEpisode episode:
                entityID = episode.ID.ToString();
                entitySeasonNumber = episode.SeasonNumber;
                entityEpisodeNumber = episode.EpisodeNumber;
                releasedAt = episode.AirDate;
                return true;

            case IVideo video:
                entityID = video.ID.ToString();
                releasedAt = video.ReleaseInfo is { ReleasedAt: { } videoReleasedAt } ? videoReleasedAt : null;
                return true;

            case ICreator creator:
                entityID = creator.ID.ToString();
                releasedAt = creator.BirthDay;
                return true;

            case ICharacter character:
                entityID = character.ID.ToString();
                return true;

            case IStudio studio:
                entityID = studio.ID.ToString();
                return true;

            case ITmdbNetwork tmdbNetwork:
                entityID = tmdbNetwork.ID.ToString();
                return true;

            case ITmdbShowCrossReference xref:
                entitySource = DataSource.TMDB;
                entityType = DataEntityType.Show;
                entityID = xref.TmdbShowID.ToString();
                return true;

            case ITmdbSeasonCrossReference xref:
                entitySource = DataSource.TMDB;
                entityType = DataEntityType.Season;
                entityID = xref.TmdbSeasonID.ToString();
                return true;

            case ITmdbEpisodeCrossReference xref:
                entitySource = DataSource.TMDB;
                entityType = DataEntityType.Episode;
                entityID = xref.TmdbEpisodeID.ToString();
                return true;

            case ITmdbMovieCrossReference xref:
                entitySource = DataSource.TMDB;
                entityType = DataEntityType.Movie;
                entityID = xref.TmdbMovieID.ToString();
                return true;

            case IUser user:
                entityID = user.ID.ToString();
                return true;
        }

        foreach (var resolver in _resolvers)
            if (resolver.TryGetMetadataForEntity(entity, out entitySource, out entityType, out entityID, out entitySeasonNumber, out entityEpisodeNumber, out releasedAt))
                return true;

        return false;
    }

    /// <inheritdoc/>
    public bool IsLinkedCrossReference(IWithImages entity, IImageCrossReference xref)
    {
        if (!TryGetMetadataForEntity(entity, out var entitySource, out var entityType, out var entityID, out _, out _, out _))
            return false;

        return xref.EntitySource == entitySource && xref.EntityType == entityType && xref.EntityID == entityID;
    }

    internal const int SeasonIdHexLength = 24;

    [GeneratedRegex(@"^(?:[0-9]{1,23}|[a-f0-9]{24})$")]
    internal static partial Regex SeasonIdRegex();

    /// <inheritdoc/>
    public IWithImages? GetEntityForImage(DataSource entitySource, DataEntityType entityType, string entityID) => (entitySource, entityType) switch
    {
        // Shoko
        (DataSource.Shoko, DataEntityType.Group) => !int.TryParse(entityID, out var shokoGroupID)
            ? null : _animeGroups.GetByID(shokoGroupID),

        (DataSource.Shoko, DataEntityType.Series) => !int.TryParse(entityID, out var shokoSeriesID)
            ? null : _animeSeries.GetByID(shokoSeriesID),

        (DataSource.Shoko, DataEntityType.Season) =>
            entityID.Split(':') is not { Length: 3 } parts ||
            !int.TryParse(parts[0], out var shokoSeriesID) ||
            _animeSeries.GetByID(shokoSeriesID) is not { } shokoSeries ||
            !Enum.TryParse<EpisodeType>(parts[1], true, out var episodeType) ||
            !int.TryParse(parts[2], out var seasonNumber)
                ? null : new AnimeSeason(shokoSeries, episodeType, seasonNumber),

        (DataSource.Shoko, DataEntityType.Episode) => !int.TryParse(entityID, out var shokoEpisodeID)
            ? null : _animeEpisodes.GetByID(shokoEpisodeID),

        (DataSource.Shoko, DataEntityType.Video) => !int.TryParse(entityID, out var videoID)
            ? null : _videoLocals.GetByID(videoID),

        (DataSource.Shoko, DataEntityType.User) => !int.TryParse(entityID, out var userID)
            ? null : _jmmUsers.GetByID(userID),

        // AniDB
        (DataSource.AniDB, DataEntityType.Anime) => !int.TryParse(entityID, out var anidbAnimeID)
            ? null : _anidbAnimes.GetByAnimeID(anidbAnimeID),

        (DataSource.AniDB, DataEntityType.Season) =>
            entityID.Split(':') is not { Length: 3 } parts ||
            !int.TryParse(parts[0], out var anidbAnimeID) ||
            _anidbAnimes.GetByAnimeID(anidbAnimeID) is not { } anidbAnime ||
            !Enum.TryParse<EpisodeType>(parts[1], true, out var episodeType) ||
            !int.TryParse(parts[2], out var seasonNumber)
                ? null : new AniDB_Season(anidbAnime, episodeType, seasonNumber),

        (DataSource.AniDB, DataEntityType.Episode) => !int.TryParse(entityID, out var anidbEpisodeID)
            ? null : _anidbEpisodes.GetByEpisodeID(anidbEpisodeID),

        (DataSource.AniDB, DataEntityType.Studio) =>
            !int.TryParse(entityID, out var anidbStudioID) ||
            _anidbCreators.GetByCreatorID(anidbStudioID) is not { } creator
                ? null : new AniDB_Studio(creator),

        (DataSource.AniDB, DataEntityType.Creator) => !int.TryParse(entityID, out var anidbCreatorID)
            ? null : _anidbCreators.GetByCreatorID(anidbCreatorID),

        (DataSource.AniDB, DataEntityType.Character) => !int.TryParse(entityID, out var anidbCharacterID)
            ? null : _anidbCharacters.GetByCharacterID(anidbCharacterID),

        // TMDB
        (DataSource.TMDB, DataEntityType.Collection) => !int.TryParse(entityID, out var tmdbCollectionID)
            ? null : _tmdbCollections.GetByTmdbCollectionID(tmdbCollectionID),

        (DataSource.TMDB, DataEntityType.Movie) => !int.TryParse(entityID, out var tmdbMovieID)
            ? null : _tmdbMovies.GetByTmdbMovieID(tmdbMovieID),

        (DataSource.TMDB, DataEntityType.Show) => !int.TryParse(entityID, out var tmdbShowID)
            ? null : _tmdbShows.GetByTmdbShowID(tmdbShowID),

        (DataSource.TMDB, DataEntityType.Season) => !SeasonIdRegex().IsMatch(entityID)
            ? null : entityID is { Length: SeasonIdHexLength }
                ? _tmdbAlternateOrderingSeasons.GetByTmdbEpisodeGroupID(entityID)
                : _tmdbSeasons.GetByTmdbSeasonID(int.Parse(entityID)),

        (DataSource.TMDB, DataEntityType.Episode) => !int.TryParse(entityID, out var tmdbEpisodeID)
            ? null : _tmdbEpisodes.GetByTmdbEpisodeID(tmdbEpisodeID),

        (DataSource.TMDB, DataEntityType.Person) => !int.TryParse(entityID, out var tmdbPersonID)
            ? null : _tmdbPersons.GetByTmdbPersonID(tmdbPersonID),

        (DataSource.TMDB, DataEntityType.Studio) => !int.TryParse(entityID, out var tmdbCompanyID)
            ? null : _tmdbCompanies.GetByTmdbCompanyID(tmdbCompanyID),

        (DataSource.TMDB, DataEntityType.Network) => !int.TryParse(entityID, out var tmdbNetworkID)
            ? null : _tmdbNetworks.GetByTmdbNetworkID(tmdbNetworkID),

        // Plugins
        _ => _resolvers
            .Select(r => r.GetEntity(entitySource, entityType, entityID))
            .FirstOrDefault(result => result is not null),
    };

    public static bool IsImageValid(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            var bytes = new byte[12];
            if (fs.Length < 12) return false;
            fs.ReadExactly(bytes);
            return GetImageFormat(bytes) != null;
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

    private static string? GetImageFormat(byte[] bytes)
    {
        if (bytes.Length < 12) return null;
        try
        {
            // https://en.wikipedia.org/wiki/BMP_file_format#File_structure
            var bmp = new byte[] { 66, 77 };
            // https://en.wikipedia.org/wiki/GIF#File_format
            var gif = new byte[] { 71, 73, 70 };
            // https://en.wikipedia.org/wiki/JPEG#Syntax_and_structure
            var jpeg = new byte[] { 255, 216 };
            // https://en.wikipedia.org/wiki/Portable_Network_Graphics#File_header
            var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
            // https://en.wikipedia.org/wiki/TIFF#Byte_order
            var tiff1 = new byte[] { 73, 73, 42, 0 };
            var tiff2 = new byte[] { 77, 77, 42, 0 };
            // https://developers.google.com/speed/webp/docs/riff_container#webp_file_header
            var webp1 = new byte[] { 82, 73, 70, 70 };
            var webp2 = new byte[] { 87, 69, 66, 80 };

            if (png.SequenceEqual(bytes.Take(png.Length)))
                return "png";

            if (jpeg.SequenceEqual(bytes.Take(jpeg.Length)))
                return "jpeg";

            if (webp1.SequenceEqual(bytes.Take(webp1.Length)) &&
                webp2.SequenceEqual(bytes.Skip(8).Take(webp2.Length)))
                return "webp";

            if (gif.SequenceEqual(bytes.Take(gif.Length)))
                return "gif";

            if (bmp.SequenceEqual(bytes.Take(bmp.Length)))
                return "bmp";

            if (tiff1.SequenceEqual(bytes.Take(tiff1.Length)) ||
                tiff2.SequenceEqual(bytes.Take(tiff2.Length)))
                return "tiff";
        }
        catch
        {
            // ignored
        }
        return null;
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
