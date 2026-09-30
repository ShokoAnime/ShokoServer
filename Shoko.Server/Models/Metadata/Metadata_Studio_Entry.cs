using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   A studio's part in one entry. Read as a studio, it is the stored studio
///   with the part it played there.
/// </summary>
public class Metadata_Studio_Entry : MetadataEntryRow, IStudio<ISeries>, IStudio<ISeason>, IStudio<IEpisode>, IStudio<IMovie>,
    IMetadataStoreRow<Metadata_Studio_Entry>
{
    #region Database Columns

    /// <summary>
    ///   The row's ID.
    /// </summary>
    public int Metadata_Studio_EntryID { get; set; }

    /// <summary>
    ///   The store's ID for the studio.
    /// </summary>
    public int StudioID { get; set; }

    /// <summary>
    ///   What the studio did for the entry.
    /// </summary>
    public StudioType StudioType { get; set; }

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_Studio_Entry>.RowID
    {
        get => Metadata_Studio_EntryID;
        set => Metadata_Studio_EntryID = value;
    }

    Metadata_Studio_Entry IMetadataStoreRow<Metadata_Studio_Entry>.Clone()
        => (Metadata_Studio_Entry)MemberwiseClone();

    #endregion

    #region Navigation

    /// <summary>
    ///   The stored studio.
    /// </summary>
    public Metadata_Studio? Studio => StudioID > 0 ? RepoFactory.Metadata_Studio.GetByID(StudioID) : null;

    #endregion

    #region IMetadata Implementation

    // The row's source and kind name its entry; a studio is only purged once
    // no entry names it, so the row always has one.
    MetadataGuid IMetadata.ID => new(Source, MetadataEntityType.Studio, Studio?.ProviderID ?? throw new InvalidOperationException($"The stored studio {StudioID} is missing."));

    MetadataSource IMetadata.Source => Source;

    MetadataEntityType IMetadata.EntityType => MetadataEntityType.Studio;

    #endregion

    #region IWithImages Implementation

    /// <summary>
    ///   The first primary image the studio's own source gave it.
    /// </summary>
    public IImageCrossReference? DefaultPrimaryImageCrossReference
        => ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = Source, ImageType = ImageEntityType.Primary }).FirstOrDefault();

    #endregion

    #region IStudio Implementation

    string IStudio.Name => Studio?.Name ?? string.Empty;

    string? IStudio.OriginalName => Studio?.OriginalName;

    IEnumerable<IMovie> IStudio.MovieWorks => Studio?.GetWorks().OfType<IMovie>() ?? [];

    IEnumerable<ISeries> IStudio.SeriesWorks => Studio?.GetWorks().OfType<ISeries>() ?? [];

    IEnumerable<IMetadata> IStudio.Works => Studio?.GetWorks() ?? [];

    MetadataGuid IStudio<ISeries>.ParentID => EntryID;

    MetadataGuid IStudio<ISeason>.ParentID => EntryID;

    MetadataGuid IStudio<IEpisode>.ParentID => EntryID;

    MetadataGuid IStudio<IMovie>.ParentID => EntryID;

    ISeries? IStudio<ISeries>.Parent => GetEntry() as ISeries;

    ISeason? IStudio<ISeason>.Parent => GetEntry() as ISeason;

    IEpisode? IStudio<IEpisode>.Parent => GetEntry() as IEpisode;

    IMovie? IStudio<IMovie>.Parent => GetEntry() as IMovie;

    #endregion
}
