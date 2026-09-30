using System.Collections.Generic;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   Every source's studios on entries in the store.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_Studio_EntryRepository(DatabaseFactory databaseFactory) : MetadataEntryRowRepository<Metadata_Studio_Entry>(databaseFactory)
{
    private PocoIndex<int, Metadata_Studio_Entry, int>? _studioIDs;

    /// <inheritdoc />
    public override void PopulateIndexes()
    {
        base.PopulateIndexes();
        _studioIDs = Cache.CreateIndex(row => row.StudioID);
    }

    /// <summary>
    ///   Every row naming one studio.
    /// </summary>
    /// <param name="studioID">The store's ID for it.</param>
    /// <returns>The rows, in no particular order.</returns>
    public IReadOnlyList<Metadata_Studio_Entry> GetByStudioID(int studioID)
        => studioID is 0 ? [] : _studioIDs!.GetMultiple(studioID);
}
