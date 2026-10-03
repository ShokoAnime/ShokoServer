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
///   A network a source keeps in the studio store.
/// </summary>
public class Metadata_Network : INetwork, IMetadataStoreRow<Metadata_Network>, IInlineTextSource, IMetadataStubRow, IMetadataDefaultImageSource
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
    ///   The country the network originates from, if the source says.
    /// </summary>
    public string? CountryOfOrigin { get; set; }

    /// <summary>
    ///   When the source last wrote the network, or <c>null</c> for a stub
    ///   the core made for a link before the source wrote it.
    /// </summary>
    public DateTime? LastUpdatedAt { get; set; }

    /// <summary>
    ///   When the last entry naming the network let go of it, or <c>null</c>
    ///   while one names it.
    /// </summary>
    public DateTime? LastOrphanedAt { get; set; }

    /// <summary>
    ///   When the core last asked the source to refresh the network, found or
    ///   not, in local time, or <c>null</c> when it never did. Kept by the
    ///   entity refresh job alone; a save of the network keeps it.
    /// </summary>
    public DateTime? LastRefreshedAt { get; set; }

    /// <summary>
    ///   What the source said of the network that needs no column of its
    ///   own, or <c>null</c> when it said none of it.
    /// </summary>
    public Metadata_NetworkExtra? ExtraData { get; set; }

    #endregion

    #region Helpers

    /// <summary>
    ///   Whether the network is a stub: a row the core made, with only the
    ///   name a link carried, or none for a user's ordering, before its source
    ///   wrote it. The source's next save of the network fills it in.
    /// </summary>
    public bool IsStub => LastUpdatedAt is null;

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

    #region IMetadataDefaultImageSource Implementation

    string? IMetadataDefaultImageSource.GetDefaultResourceID(ImageEntityType imageType)
        => ExtraData?.GetDefaultResourceID(imageType);

    #endregion

    #region IWithImages Implementation

    /// <summary>
    ///   The primary image the network's own source pins as its default,
    ///   else the first one it gave it.
    /// </summary>
    public IImageCrossReference? DefaultPrimaryImageCrossReference
        => MetadataStoredEntry.DefaultImage(this, ImageEntityType.Primary);

    #endregion

    #region INetwork Implementation

    DateTime? INetwork.LastRefreshedAt => LastRefreshedAt?.ToUniversalTime();

    #endregion
}
