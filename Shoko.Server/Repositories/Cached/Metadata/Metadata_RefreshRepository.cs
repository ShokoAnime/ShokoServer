using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   When each plugin source's series, movies and collections were last
///   refreshed.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_RefreshRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<Metadata_Refresh, int>(databaseFactory)
{
    private PocoIndex<int, Metadata_Refresh, (MetadataSource, MetadataEntityType, string)>? _entries;

    /// <inheritdoc />
    protected override int SelectKey(Metadata_Refresh entity)
        => entity.Metadata_RefreshID;

    /// <inheritdoc />
    public override void PopulateIndexes()
    {
        _entries = Cache.CreateIndex(row => (row.Source, row.EntityType, row.ProviderID));
    }

    /// <summary>
    ///   Looks up the row of one entry.
    /// </summary>
    /// <param name="entry">The series, movie or collection.</param>
    /// <returns>The row, or <c>null</c> when the entry was never refreshed.</returns>
    public Metadata_Refresh? GetByEntry(MetadataGuid entry)
        => _entries!.GetOne((entry.Source, entry.EntityType, entry.ID));
}
