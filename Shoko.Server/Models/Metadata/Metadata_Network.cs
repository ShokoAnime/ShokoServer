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
///   A network a source keeps in the studio store.
/// </summary>
public class Metadata_Network : INetwork, IMetadataStoreRow<Metadata_Network>, IInlineTextSource
{
    #region Database Columns

    /// <summary>
    ///   The store's own ID for the network, the same across sources.
    /// </summary>
    public int Metadata_NetworkID { get; set; }

    /// <summary>
    ///   The source the network belongs to.
    /// </summary>
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    ///   The source's own ID for the network.
    /// </summary>
    public string ProviderID { get; set; } = string.Empty;

    /// <summary>
    ///   The network's name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///   When the source last wrote the network.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>
    ///   When the last entry naming the network let go of it, or <c>null</c>
    ///   while one names it.
    /// </summary>
    public DateTime? LastOrphanedAt { get; set; }

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_Network>.RowID
    {
        get => Metadata_NetworkID;
        set => Metadata_NetworkID = value;
    }

    Metadata_Network IMetadataStoreRow<Metadata_Network>.Clone()
        => (Metadata_Network)MemberwiseClone();

    #endregion

    #region Navigation

    /// <summary>
    ///   Every entry the network aired that a provider holds.
    /// </summary>
    /// <returns>The entries.</returns>
    public IEnumerable<IMetadata> GetWorks()
        => MetadataEntryRow.InOrder(RepoFactory.Metadata_Network_Entry.GetByNetworkID(Metadata_NetworkID))
            .Select(link => link.EntryID)
            .Distinct()
            .Select(MetadataEntries.Resolve)
            .OfType<IMetadata>();

    #endregion

    #region IMetadata Implementation

    MetadataGuid IMetadata.ID => new(Source, MetadataEntityType.Network, ProviderID);

    #endregion

    #region IInlineTextSource Implementation

    ITitle? IInlineTextSource.InlineTitle => InlineText.Title(Source, Name, TitleLanguage.Unknown, "unk");

    IText? IInlineTextSource.InlineOverview => null;

    #endregion

    #region IWithImages Implementation

    /// <summary>
    ///   The first primary image the network's own source gave it.
    /// </summary>
    public IImageCrossReference? DefaultPrimaryImageCrossReference
        => ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = Source, ImageType = ImageEntityType.Primary }).FirstOrDefault();

    #endregion
}
