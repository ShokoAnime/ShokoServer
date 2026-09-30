using System.Collections.Generic;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   Every source's tags on entries in the store.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_Tag_EntryRepository(DatabaseFactory databaseFactory) : MetadataEntryRowRepository<Metadata_Tag_Entry>(databaseFactory)
{
    private PocoIndex<int, Metadata_Tag_Entry, int>? _tagIDs;

    /// <inheritdoc />
    public override void PopulateIndexes()
    {
        base.PopulateIndexes();
        _tagIDs = Cache.CreateIndex(row => row.TagID);
    }

    /// <summary>
    ///   Every row naming one tag.
    /// </summary>
    /// <param name="tagID">The store's ID for it.</param>
    /// <returns>The rows, in no particular order.</returns>
    public IReadOnlyList<Metadata_Tag_Entry> GetByTagID(int tagID)
        => tagID is 0 ? [] : _tagIDs!.GetMultiple(tagID);
}
