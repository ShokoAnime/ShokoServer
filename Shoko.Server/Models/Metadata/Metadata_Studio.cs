using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Services.MetadataStorage;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   A studio a source keeps in the studio store.
/// </summary>
public class Metadata_Studio : IStudio, IMetadataStoreRow<Metadata_Studio>, IInlineTextSource
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
    ///   When the source last wrote the studio.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>
    ///   When the last entry naming the studio let go of it, or <c>null</c>
    ///   while one names it.
    /// </summary>
    public DateTime? LastOrphanedAt { get; set; }

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

    #region IWithImages Implementation

    /// <summary>
    ///   The first primary image the studio's own source gave it.
    /// </summary>
    public IImageCrossReference? DefaultPrimaryImageCrossReference
        => ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = Source, ImageType = ImageEntityType.Primary }).FirstOrDefault();

    #endregion

    #region IStudio Implementation

    // A studio's part is only known per entry.
    StudioType IStudio.StudioType => StudioType.None;

    IEnumerable<IMovie> IStudio.MovieWorks => GetWorks().OfType<IMovie>();

    IEnumerable<ISeries> IStudio.SeriesWorks => GetWorks().OfType<ISeries>();

    IEnumerable<IMetadata> IStudio.Works => GetWorks();

    #endregion
}
