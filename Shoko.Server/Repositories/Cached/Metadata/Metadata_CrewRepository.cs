using System.Collections.Generic;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   Every source's crew credits in the store.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_CrewRepository(DatabaseFactory databaseFactory) : MetadataEntryRowRepository<Metadata_Crew>(databaseFactory)
{
    private PocoIndex<int, Metadata_Crew, int>? _creatorIDs;

    /// <inheritdoc />
    public override void PopulateIndexes()
    {
        base.PopulateIndexes();
        _creatorIDs = Cache.CreateIndex(row => row.CreatorID);
    }

    /// <summary>
    ///   Every row naming one creator.
    /// </summary>
    /// <param name="creatorID">The store's ID for it.</param>
    /// <returns>The rows, in no particular order.</returns>
    public IReadOnlyList<Metadata_Crew> GetByCreatorID(int creatorID)
        => creatorID is 0 ? [] : _creatorIDs!.GetMultiple(creatorID);
}
