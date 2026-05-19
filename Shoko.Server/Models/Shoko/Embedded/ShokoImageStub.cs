
using System;
using System.Collections.Generic;
using System.IO;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;

namespace Shoko.Server.Models.Shoko;

/// <summary>
///     A stub for an image that only contains the bare minimum information.
/// </summary>
public class ShokoImageStub(IImage image, IImageCrossReference xref, bool linkedXref = false) : IImage
{
    public Guid ID => image.ID;

    public Guid PrimaryID => image.PrimaryID;

    public IReadOnlyList<Guid> LinkedIDs => image.LinkedIDs;

    [Obsolete]
    public int LocalID => image.LocalID;

    public string ResourceID => image.ResourceID;

    public DataSource Source => image.Source;

    public ImageEntityType Type => xref.ImageType;

    public string ContentType => image.ContentType;

    public bool IsEnabled => image.IsEnabled;

    public bool IsDesired => image.IsDesired;

    public bool IsPreferred => !linkedXref && xref.IsPreferred;

    public bool IsLocked => image.Source is not DataSource.User;

    public bool IsAvailable => image.IsAvailable;

    public byte DownloadAttempts => image.DownloadAttempts;

    public uint? Width => image.Width;

    public uint? Height => image.Height;

    public double? Rating => xref.Rating;

    public int? RatingVotes => xref.RatingVotes;

    public string LanguageCode => image.LanguageCode;

    public string CountryCode => image.CountryCode;

    public TitleLanguage Language => image.Language;

    public string LocalPath => image.LocalPath;

    public DateTime CreatedAt => image.CreatedAt;

    public DateTime LastUpdatedAt => image.LastUpdatedAt;

    public bool Equals(IImage other)
        => image.Equals(other);

    public IReadOnlyList<IImageCrossReference> GetCrossReferences(
        ImageEntityType? imageType = null,
        DataSource? xrefSource = null,
        DataSource? entitySource = null,
        DataEntityType? entityType = null,
        bool? isEnabled = null,
        bool? isDesired = null
    )
        => image.GetCrossReferences(imageType, xrefSource, entitySource, entityType, isEnabled, isDesired);

    public IReadOnlyList<IImage> GetLinkedImages(bool includePrimaryImage = true)
        => image.GetLinkedImages(includePrimaryImage);

    public IImage GetPrimaryImage()
        => image.GetPrimaryImage();

    public Stream GetStream()
        => image.GetStream();
}
