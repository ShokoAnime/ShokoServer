using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   A collection a plugin source keeps in the collection store.
/// </summary>
public class Metadata_Collection : IMovieCollection, IMetadataStoreRow<Metadata_Collection>, IMetadataDefaultImageSource
{
    #region Database Columns

    /// <summary>
    ///   The row's ID.
    /// </summary>
    public int Metadata_CollectionID { get; set; }

    /// <summary>
    ///   The source the collection belongs to.
    /// </summary>
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    ///   The source's own ID for the collection.
    /// </summary>
    public string ProviderID { get; set; } = string.Empty;

    /// <summary>
    ///   When the store first wrote the collection. Set once and never changed.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///   When the source last wrote the collection.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>
    ///   When the core last refreshed the collection in full without failing, in
    ///   local time, or <c>null</c> when it never did. Kept by the refresh job
    ///   alone; a save of the collection keeps it.
    /// </summary>
    public DateTime? LastRefreshedAt { get; set; }

    /// <summary>
    ///   What the source said of the collection that needs no column of its
    ///   own, or <c>null</c> when it said none of it.
    /// </summary>
    public Metadata_CollectionExtra? ExtraData { get; set; }

    #endregion

    #region Helpers

    /// <summary>
    ///   The collection's identifier.
    /// </summary>
    public MetadataGuid ID => new(Source, MetadataEntityType.Collection, ProviderID);

    /// <summary>
    ///   The series and movies the collection gathers.
    /// </summary>
    public IReadOnlyList<MetadataGuid> Members
        => [.. RepoFactory.Metadata_Collection_Member.GetByCollectionID(Source, ProviderID).Select(member => member.MemberGuid)];

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_Collection>.RowID
    {
        get => Metadata_CollectionID;
        set => Metadata_CollectionID = value;
    }

    Metadata_Collection IMetadataStoreRow<Metadata_Collection>.Clone()
        => (Metadata_Collection)MemberwiseClone();

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.Title => ((IWithTitles)this).PreferredTitle?.Value ?? ((IWithTitles)this).DefaultTitle.Value;

    ITitle IWithTitles.DefaultTitle => MetadataStoredEntry.DefaultTitle(this);

    ITitle? IWithTitles.PreferredTitle => MetadataStoredEntry.PreferredTitle(this);

    IReadOnlyList<ITitle> IWithTitles.Titles => MetadataStoredEntry.Titles(this);

    #endregion

    #region IWithOverviews Implementation

    IText? IWithOverviews.DefaultOverview => MetadataStoredEntry.DefaultOverview(this);

    IText? IWithOverviews.PreferredOverview => MetadataStoredEntry.PreferredOverview(this);

    IReadOnlyList<IText> IWithOverviews.Overviews => MetadataStoredEntry.Overviews(this);

    #endregion

    #region IMetadataDefaultImageSource Implementation

    string? IMetadataDefaultImageSource.GetDefaultResourceID(ImageEntityType imageType)
        => ExtraData?.GetDefaultResourceID(imageType);

    #endregion

    #region IWithImages Implementation

    IImageCrossReference? IWithImages.DefaultPrimaryImageCrossReference => MetadataStoredEntry.DefaultImage(this, ImageEntityType.Primary);

    IImageCrossReference? IWithImages.DefaultBackdropImageCrossReference => MetadataStoredEntry.DefaultImage(this, ImageEntityType.Backdrop);

    IImageCrossReference? IWithImages.DefaultLogoImageCrossReference => MetadataStoredEntry.DefaultImage(this, ImageEntityType.Logo);

    IImageCrossReference? IWithImages.DefaultBannerImageCrossReference => MetadataStoredEntry.DefaultImage(this, ImageEntityType.Banner);

    IImageCrossReference? IWithImages.DefaultDiscImageCrossReference => MetadataStoredEntry.DefaultImage(this, ImageEntityType.Disc);

    #endregion

    #region ICollection Implementation

    DateTime? ICollection.LastRefreshedAt => LastRefreshedAt?.ToUniversalTime();

    #endregion

    #region IMovieCollection Implementation

    IReadOnlyList<IMovie> IMovieCollection.Movies => [
        .. RepoFactory.Metadata_Collection_Member.GetByCollectionID(Source, ProviderID)
            .Where(member => member.MemberType == MetadataEntityType.Movie)
            .Select(member => RepoFactory.Metadata_Movie.GetByProviderID(member.Source, member.MemberID))
            .OfType<IMovie>(),
    ];

    #endregion
}
