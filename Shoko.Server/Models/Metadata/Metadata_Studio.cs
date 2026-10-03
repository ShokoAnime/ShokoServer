using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Repositories;
using Shoko.Server.Services.MetadataStorage;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   A studio a source keeps in the studio store.
/// </summary>
public class Metadata_Studio : IStudio, IMetadataStoreRow<Metadata_Studio>, IInlineTextSource, IMetadataStubRow, IMetadataDefaultImageSource
{
    #region Database Columns

    /// <summary>
    ///   The store's own ID for the studio, the same across sources.
    /// </summary>
    public int Metadata_StudioID { get; set; }

    /// <summary>
    ///   The source the studio belongs to.
    /// </summary>
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    ///   The source's own ID for the studio.
    /// </summary>
    public string ProviderID { get; set; } = string.Empty;

    /// <summary>
    ///   The studio's name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///   The studio's name in its original script, when it differs.
    /// </summary>
    public string? OriginalName { get; set; }

    /// <summary>
    ///   The country the studio originates from, as its source gave it, or
    ///   <c>null</c> when it did not say.
    /// </summary>
    public string? CountryOfOrigin { get; set; }

    /// <summary>
    ///   When the source last wrote the studio, or <c>null</c> for a stub
    ///   the core made for a link before the source wrote it.
    /// </summary>
    public DateTime? LastUpdatedAt { get; set; }

    /// <summary>
    ///   When the last entry naming the studio let go of it, or <c>null</c>
    ///   while one names it.
    /// </summary>
    public DateTime? LastOrphanedAt { get; set; }

    /// <summary>
    ///   When the core last asked the source to refresh the studio, found or
    ///   not, in local time, or <c>null</c> when it never did. Kept by the
    ///   entity refresh job alone; a save of the studio keeps it.
    /// </summary>
    public DateTime? LastRefreshedAt { get; set; }

    /// <summary>
    ///   What the source said of the studio that needs no column of its
    ///   own, or <c>null</c> when it said none of it.
    /// </summary>
    public Metadata_StudioExtra? ExtraData { get; set; }

    #endregion

    #region Helpers

    /// <summary>
    ///   Whether the studio is a stub: a row the core made, with only the
    ///   name a link carried, before its source wrote it. The source's
    ///   next save of the studio fills it in.
    /// </summary>
    public bool IsStub => LastUpdatedAt is null;

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_Studio>.RowID
    {
        get => Metadata_StudioID;
        set => Metadata_StudioID = value;
    }

    Metadata_Studio IMetadataStoreRow<Metadata_Studio>.Clone()
        => (Metadata_Studio)MemberwiseClone();

    #endregion

    #region Navigation

    /// <summary>
    ///   Every entry the studio worked on that a provider holds.
    /// </summary>
    /// <returns>The entries.</returns>
    public IEnumerable<IMetadata> GetWorks()
        => MetadataEntryRow.InOrder(RepoFactory.Metadata_Studio_Entry.GetByStudioID(Metadata_StudioID))
            .Select(link => link.EntryID)
            .Distinct()
            .Select(MetadataEntries.Resolve)
            .OfType<IMetadata>();

    #endregion

    #region IMetadata Implementation

    MetadataGuid IMetadata.ID => new(Source, MetadataEntityType.Studio, ProviderID);

    #endregion

    #region IInlineTextSource Implementation

    ITitle? IInlineTextSource.InlineTitle => InlineText.Title(Source, Name, TitleLanguage.Unknown, "unk");

    IText? IInlineTextSource.InlineOverview => null;

    #endregion

    #region IMetadataDefaultImageSource Implementation

    string? IMetadataDefaultImageSource.GetDefaultResourceID(ImageEntityType imageType)
        => ExtraData?.GetDefaultResourceID(imageType);

    #endregion

    #region IWithImages Implementation

    /// <summary>
    ///   The primary image the studio's own source pins as its default,
    ///   else the first one it gave it.
    /// </summary>
    public IImageCrossReference? DefaultPrimaryImageCrossReference
        => MetadataStoredEntry.DefaultImage(this, ImageEntityType.Primary);

    #endregion

    #region IStudio Implementation

    DateTime? IStudio.LastRefreshedAt => LastRefreshedAt?.ToUniversalTime();

    // A studio's part is only known per entry.
    StudioType IStudio.StudioType => StudioType.None;

    IEnumerable<IMovie> IStudio.MovieWorks => GetWorks().OfType<IMovie>();

    IEnumerable<ISeries> IStudio.SeriesWorks => GetWorks().OfType<ISeries>();

    IEnumerable<IMetadata> IStudio.Works => GetWorks();

    #endregion
}
