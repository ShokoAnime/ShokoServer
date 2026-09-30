using System.Collections.Generic;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   Every source's networks on entries in the store.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_Network_EntryRepository(DatabaseFactory databaseFactory) : MetadataEntryRowRepository<Metadata_Network_Entry>(databaseFactory)
{
    private PocoIndex<int, Metadata_Network_Entry, int>? _networkIDs;

    /// <inheritdoc />
    public override void PopulateIndexes()
    {
        base.PopulateIndexes();
        _networkIDs = Cache.CreateIndex(row => row.NetworkID);
    }

    /// <summary>
    ///   Every row naming one network.
    /// </summary>
    /// <param name="networkID">The store's ID for it.</param>
    /// <returns>The rows, in no particular order.</returns>
    public IReadOnlyList<Metadata_Network_Entry> GetByNetworkID(int networkID)
        => networkID is 0 ? [] : _networkIDs!.GetMultiple(networkID);
}
