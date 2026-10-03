using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   A network one entry aired on. Read as a network, it is the stored
///   network, which is on the entry's own source except for a user's
///   ordering, whose networks may be on any source.
/// </summary>
public class Metadata_Network_Entry : MetadataEntryRow, INetwork, IMetadataStoreRow<Metadata_Network_Entry>
{
    #region Database Columns

    /// <summary>
    ///   The row's ID.
    /// </summary>
    public int Metadata_Network_EntryID { get; set; }

    /// <summary>
    ///   The store's ID for the network.
    /// </summary>
    public int NetworkID { get; set; }

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_Network_Entry>.RowID
    {
        get => Metadata_Network_EntryID;
        set => Metadata_Network_EntryID = value;
    }

    Metadata_Network_Entry IMetadataStoreRow<Metadata_Network_Entry>.Clone()
        => (Metadata_Network_Entry)MemberwiseClone();

    #endregion

    #region Navigation

    /// <summary>
    ///   The stored network.
    /// </summary>
    public Metadata_Network? Network => NetworkID > 0 ? RepoFactory.Metadata_Network.GetByID(NetworkID) : null;

    #endregion

    #region IMetadata Implementation

    // A network is only purged once no entry names it, so the row always has one.
    MetadataGuid IMetadata.ID
        => Network is { } network
            ? new(network.Source, MetadataEntityType.Network, network.ProviderID)
            : throw new InvalidOperationException($"The stored network {NetworkID} is missing.");

    MetadataSource IMetadata.Source => Network?.Source ?? Source;

    MetadataEntityType IMetadata.EntityType => MetadataEntityType.Network;

    #endregion

    #region IWithImages Implementation

    /// <summary>
    ///   The primary image the network's own source pins as its default, else
    ///   the first one it gave it.
    /// </summary>
    public IImageCrossReference? DefaultPrimaryImageCrossReference
        => Network is { } network ? MetadataStoredEntry.DefaultImage(network, ImageEntityType.Primary) : null;

    #endregion

    #region INetwork Implementation

    string INetwork.Name => Network?.Name ?? string.Empty;

    string? INetwork.CountryOfOrigin => Network?.CountryOfOrigin;

    DateTime? INetwork.LastRefreshedAt => (Network as INetwork)?.LastRefreshedAt;

    #endregion
}
