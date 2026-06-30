using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Server.Repositories;
using Shoko.Server.Services;
using Shoko.Server.Utilities;

namespace Shoko.Server.Models.Shoko;

/// <summary>
/// Unified image model. One row per unique image regardless of source.
/// This model replaces TMDB_Image and Image_Base as the universal image entity.
/// </summary>
public class ShokoImage : IImage
{
    #region Properties

    /// <summary>
    ///   Locally incrementing ID for APIv1-APIv3 backwards compatibility.
    /// </summary>
    public int LocalID { get; set; }

    /// <inheritdoc/>
    public Guid ID { get; set; }

    /// <inheritdoc/>
    public Guid PrimaryID { get; set; }

    /// <inheritdoc/>
    public IReadOnlyList<Guid> LinkedIDs => GetLinkedImages(false).Select(x => x.ID).ToList();

    /// <inheritdoc/>
    [Required, MaxLength(64)]
    public string ResourceID { get; set; } = string.Empty;

    /// <inheritdoc/>
    [DeniedValues(DataSource.None)]
    public DataSource Source { get; set; }

    /// <inheritdoc/>
    [MaxLength(8)]
    public string? LanguageCode { get; set; }

    /// <inheritdoc/>
    [MaxLength(8)]
    public string? CountryCode { get; set; }

    /// <summary>
    /// Image width in pixels.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Image height in pixels.
    /// </summary>
    public int? Height { get; set; }

    /// <inheritdoc/>
    public string ContentType { get; set; } = ContentTypeHelper.UnknownMimeType;

    /// <summary>
    /// When the Image record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the Image was last updated (downloaded, xref created/updated, metadata changed).
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region IImage Implementation

    /// <inheritdoc/>
    ImageEntityType IImage.Type => ImageEntityType.None;

    /// <inheritdoc/>
    public bool IsDesired => RepoFactory.ShokoImage_Entity.GetByImageID(ID).Any(xref => xref.IsEnabled && xref.IsDesired);

    /// <inheritdoc/>
    public bool IsEnabled => RepoFactory.ShokoImage_Entity.GetByImageID(ID).Any(xref => xref.IsEnabled);

    /// <inheritdoc/>
    bool IImage.IsPreferred => false;

    /// <inheritdoc/>
    bool IImage.IsLocked => Source is not DataSource.User;

    /// <inheritdoc/>
    public bool IsAvailable { get; set; }

    /// <inheritdoc/>
    public bool IsPrimaryAvailable
        => PrimaryID == ID ? IsAvailable : GetPrimaryImage()?.IsAvailable ?? false;

    /// <inheritdoc/>
    public byte DownloadAttempts { get; set; }

    /// <inheritdoc/>
    double? IImage.Rating => null;

    /// <inheritdoc/>
    int? IImage.RatingVotes => null;

    /// <inheritdoc/>
    TitleLanguage IImage.Language
    {
        get
        {
            if (LanguageCode is null)
                return TitleLanguage.None;

            return LanguageCode.GetTitleLanguage();
        }
    }

    /// <inheritdoc/>
    public string LocalPath
    {
        get
        {
            var id = ID.ToString("N");
            var ext = GetExtensionForMimeType(ContentType);
            return Path.Join(ApplicationPaths.Instance.ImagesPath, Source.ToString(), id[..2], id + ext);
        }
    }

    #endregion

    #region Methods

    public ShokoImage? GetPrimaryImage() => PrimaryID == ID ? this : RepoFactory.ShokoImage.GetByID(PrimaryID);

    public IReadOnlyList<ShokoImage> GetLinkedImages(bool includePrimaryImage = true)
        => RepoFactory.ShokoImage.GetByPrimaryImageID(PrimaryID) is { Count: > 0 } images
            ? includePrimaryImage ? images : images.Where(image => image.ID != PrimaryID).ToList()
            : [];

    public bool Update(ImageUpdateData? data)
    {
        if (data is null)
            return false;

        var updated = false;
        if (data.HasSizeSet)
        {
            var newWidth = data.HasSize ? data.Width : null;
            var newHeight = data.HasSize ? data.Height : null;
            if (Width != newWidth || Height != newHeight)
            {
                Width = newWidth;
                Height = newHeight;
                updated = true;
            }
        }

        if (data.HasLanguageCodeSet && !string.Equals(LanguageCode, data.LanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            LanguageCode = data.LanguageCode;
            updated = true;
        }

        if (data.HasCountryCodeSet && !string.Equals(CountryCode, data.CountryCode, StringComparison.OrdinalIgnoreCase))
        {
            CountryCode = data.CountryCode;
            updated = true;
        }

        if (updated)
            LastUpdatedAt = DateTime.UtcNow;

        return updated;
    }

    /// <summary>
    ///   Recompute <see cref="IsAvailable"/> from the local file system and store
    ///   the result. Returns the new value.
    /// </summary>
    public bool RefreshAvailability()
    {
        var localPath = LocalPath;
        return IsAvailable = !string.IsNullOrEmpty(localPath) && File.Exists(localPath) && ImageManager.IsImageValid(localPath);
    }

    /// <inheritdoc/>
    public Stream? GetStream()
    {
        var localPath = LocalPath;
        if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
            return new FileStream(localPath, FileMode.Open, FileAccess.Read);

        return null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ShokoImage_Entity> GetCrossReferences(
        ImageEntityType? imageType = null,
        DataSource? xrefSource = null,
        DataSource? entitySource = null,
        DataEntityType? entityType = null,
        bool? isEnabled = null,
        bool? isDesired = null,
        bool? isAvailable = null,
        bool? primaryImage = null,
        bool includeLinkedImages = false
    )
    {
        // When includeLinkedImages is true, aggregate cross-references across the whole linked
        // image group; otherwise only this image's own cross-references.
        var xrefs = includeLinkedImages
            ? GetLinkedImages(includePrimaryImage: true).SelectMany(image => RepoFactory.ShokoImage_Entity.GetByImageID(image.ID))
            : RepoFactory.ShokoImage_Entity.GetByImageID(ID);
        if (imageType is not null || xrefSource is not null || entitySource is not null || entityType is not null || isEnabled is not null || isDesired is not null || isAvailable is not null || primaryImage is not null)
            xrefs = xrefs.Where(xref =>
                (imageType is null || xref.ImageType == imageType.Value) &&
                (xrefSource is null || xref.Source == xrefSource.Value) &&
                (entitySource is null || xref.EntitySource == entitySource.Value) &&
                (entityType is null || xref.EntityType == entityType.Value) &&
                (isEnabled is null || xref.IsEnabled == isEnabled.Value) &&
                (isDesired is null || xref.IsDesired == isDesired.Value) &&
                (isAvailable is null || xref.IsAvailable == isAvailable.Value) &&
                (primaryImage is null || xref.PrimaryImageID == xref.ImageID == primaryImage.Value)
            );
        if (xrefs is IReadOnlyList<ShokoImage_Entity> list)
            return list;
        return xrefs.ToList();
    }

    public override int GetHashCode() => ID.GetHashCode();

    public override bool Equals(object? other)
    {
        if (other is null || other is not IImage image)
            return false;
        return Equals(image);
    }

    public bool Equals(IImage? other)
    {
        if (other is null)
            return false;
        return other.ID == ID;
    }

    #endregion

    #region IImage Implementation

    IImageCrossReference? IImage.CrossReference => null;

    /// <inheritdoc/>
    IImage? IImage.GetPrimaryImage() => GetPrimaryImage();

    /// <inheritdoc/>
    IReadOnlyList<IImage> IImage.GetLinkedImages(bool includePrimaryImage) => GetLinkedImages(includePrimaryImage);

    /// <inheritdoc/>
    IReadOnlyList<IImageCrossReference> IImage.GetCrossReferences(
        ImageEntityType? imageType,
        DataSource? xrefSource,
        DataSource? entitySource,
        DataEntityType? entityType,
        bool? isEnabled,
        bool? isDesired,
        bool? isAvailable,
        bool? primaryImage,
        bool includeLinkedImages
    ) => GetCrossReferences(imageType, xrefSource, entitySource, entityType, isEnabled, isDesired, isAvailable, primaryImage, includeLinkedImages);

    #endregion

    #region Static Helpers

    public static string GetExtensionForMimeType(string contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return ".bin";

        var ext = contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            "image/bmp" => ".bmp",
            "image/tiff" => ".tif",
            "image/svg+xml" => ".svg",
            "image/svg" => ".svg",
            "image/avif" => ".avif",
            _ => null,
        };
        if (ext is not null)
            return ext;

        if (ContentTypeHelper.TryGetExtensionForMimeType(contentType, out var mimeExt))
            return mimeExt;

        return ".bin";
    }

    #endregion
}
