using System;
using System.Collections.Generic;
using System.IO;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;

namespace Shoko.Abstractions.Metadata.Stub;

/// <summary>
///   A stub implementation of the <see cref="IImage"/> interface wrapping an
///   <see cref="IImage"/> instance and an <see cref="IImageCrossReference"/>
///   instance.
/// </summary>
public class ImageStub(IImage image, IImageCrossReference? xref = null, bool linkedXref = false, ImageEntityType? type = null, bool? isPreferred = null) : IImage
{
    /// <inheritdoc />
    public Guid ID => image.ID;

    /// <inheritdoc />
    public Guid PrimaryID => image.PrimaryID;

    /// <inheritdoc />
    public IReadOnlyList<Guid> LinkedIDs => image.LinkedIDs;

    /// <inheritdoc />
    [Obsolete("Only for backwards compatibility with older APIs. Use the universally unique identifier instead.")]
    public int LocalID => image.LocalID;

    /// <inheritdoc />
    public string ResourceID => image.ResourceID;

    /// <inheritdoc />
    public DataSource Source => image.Source;

    /// <summary>
    ///   The wrapped image.
    /// </summary>
    public IImage Image => image;

    /// <inheritdoc />
    public IImageCrossReference? CrossReference => xref ?? image.CrossReference;

    /// <inheritdoc />
    public ImageEntityType Type => type ?? xref?.ImageType ?? image.Type;

    /// <inheritdoc />
    public string ContentType => image.ContentType;

    /// <inheritdoc />
    public bool IsEnabled => image.IsEnabled;

    /// <inheritdoc />
    public bool IsDesired => image.IsDesired;

    /// <inheritdoc />
    public bool IsPreferred => isPreferred ?? (!linkedXref && (xref?.IsPreferred ?? image.IsPreferred));

    /// <inheritdoc />
    public bool IsLocked => image.Source is not DataSource.User;

    /// <inheritdoc />
    public bool IsAvailable => image.IsAvailable;

    /// <inheritdoc />
    public bool IsPrimaryAvailable => image.IsPrimaryAvailable;

    /// <inheritdoc />
    public byte DownloadAttempts => image.DownloadAttempts;

    /// <inheritdoc />
    public int? Width => image.Width;

    /// <inheritdoc />
    public int? Height => image.Height;

    /// <inheritdoc />
    public double? Rating => xref is null ? image.Rating : xref.Rating;

    /// <inheritdoc />
    public int? RatingVotes => xref is null ? image.RatingVotes : xref.RatingVotes;

    /// <inheritdoc />
    public string? LanguageCode => image.LanguageCode;

    /// <inheritdoc />
    public string? CountryCode => image.CountryCode;

    /// <inheritdoc />
    public TitleLanguage Language => image.Language;

    /// <inheritdoc />
    public string LocalPath => image.LocalPath;

    /// <inheritdoc />
    public DateTime CreatedAt => image.CreatedAt;

    /// <inheritdoc />
    public DateTime LastUpdatedAt => image.LastUpdatedAt;

    /// <inheritdoc />
    public bool Equals(IImage? other)
        => image.Equals(other);

    /// <inheritdoc />
    public IReadOnlyList<IImageCrossReference> GetCrossReferences(
        ImageEntityType? imageType = null,
        DataSource? xrefSource = null,
        DataSource? entitySource = null,
        DataEntityType? entityType = null,
        bool? isEnabled = null,
        bool? isDesired = null
    )
        => image.GetCrossReferences(imageType, xrefSource, entitySource, entityType, isEnabled, isDesired);

    /// <inheritdoc />
    public IReadOnlyList<IImage> GetLinkedImages(bool includePrimaryImage = true)
        => image.GetLinkedImages(includePrimaryImage);

    /// <inheritdoc />
    public IImage? GetPrimaryImage()
        => image.PrimaryID == image.ID
            ? this
            : image.GetPrimaryImage() is { } primaryImage
                ? new ImageStub(primaryImage, xref, linkedXref)
                : null;

    /// <inheritdoc />
    public Stream? GetStream()
        => image.GetStream();
}
