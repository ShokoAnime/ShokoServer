using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   Every source's authored relations in the store.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_RelationRepository(DatabaseFactory databaseFactory) : MetadataStoreRepository<Metadata_Relation>(databaseFactory)
{
    private PocoIndex<int, Metadata_Relation, (MetadataSource, MetadataEntityType, string)>? _bases;

    private PocoIndex<int, Metadata_Relation, (MetadataSource, MetadataEntityType, string)>? _related;

    /// <inheritdoc />
    public override void PopulateIndexes()
    {
        _bases = Cache.CreateIndex(row => (row.Source, row.BaseType, row.BaseID));
        _related = Cache.CreateIndex(row => (row.Source, row.RelatedType, row.RelatedID));
    }

    /// <summary>
    ///   Every relation stated from an entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The relations, in the order they were given.</returns>
    public IReadOnlyList<Metadata_Relation> GetByBase(MetadataGuid entry)
        => [.. _bases!.GetMultiple((entry.Source, entry.EntityType, entry.ID)).OrderBy(row => row.Ordering).ThenBy(row => row.Metadata_RelationID)];

    /// <summary>
    ///   Every relation stated towards an entry from another.
    /// </summary>
    /// <param name="entry">The entry related to.</param>
    /// <returns>The relations, oldest first.</returns>
    public IReadOnlyList<Metadata_Relation> GetByRelated(MetadataGuid entry)
        => [.. _related!.GetMultiple((entry.Source, entry.EntityType, entry.ID)).OrderBy(row => row.Metadata_RelationID)];
}
