using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using MimeMapping;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Server.Repositories;
using Shoko.Server.Services;

#nullable enable
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
    public string ContentType { get; set; } = MimeUtility.UnknownMimeType;

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
    public bool IsAvailable
    {
        get
        {
            var localPath = LocalPath;
            return !string.IsNullOrEmpty(localPath) && File.Exists(localPath) && ImageManager.IsImageValid(localPath);
        }
    }

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
            return Path.Join(ApplicationPaths.Instance.ImagesPath, Source.ToString(), id[..2], id);
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

    /// <inheritdoc/>
    public Stream? GetStream()
    {
        var localPath = LocalPath;
        if (IsAvailable && !string.IsNullOrEmpty(localPath))
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
        bool? isDesired = null
    )
    {
        var xrefs = (IEnumerable<ShokoImage_Entity>)RepoFactory.ShokoImage_Entity.GetByImageID(ID);
        if (imageType.HasValue)
            xrefs = xrefs.Where(xref => xref.ImageType == imageType.Value);
        if (xrefSource.HasValue)
            xrefs = xrefs.Where(xref => xref.Source == xrefSource.Value);
        if (entitySource.HasValue)
            xrefs = xrefs.Where(xref => xref.EntitySource == entitySource.Value);
        if (entityType.HasValue)
            xrefs = xrefs.Where(xref => xref.EntityType == entityType.Value);
        if (isEnabled.HasValue)
            xrefs = xrefs.Where(xref => xref.IsEnabled == isEnabled.Value);
        if (isDesired.HasValue)
            xrefs = xrefs.Where(xref => xref.IsDesired == isDesired.Value);
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
        bool? isDesired
    ) => GetCrossReferences(imageType, xrefSource, entitySource, entityType, isEnabled, isDesired);

    #endregion
}
