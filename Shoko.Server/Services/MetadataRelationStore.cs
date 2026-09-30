using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Services.MetadataStorage;

namespace Shoko.Server.Services;

/// <summary>
///   Keeps every source's authored relations in the store's own table,
///   cached in memory, and reads them from either end.
/// </summary>
/// <param name="repository">The relations.</param>
/// <param name="writer">Writes the changes.</param>
public class MetadataRelationStore(Metadata_RelationRepository repository, MetadataRowWriter writer) : IMetadataRelationStore
{
    /// <summary>
    ///   Held around every write, so two writers never read the same state
    ///   and both add the same row.
    /// </summary>
    private readonly object _writeLock = new();

    #region Reading

    /// <inheritdoc />
    /// <remarks>
    ///   The entry's own relations come first, in the order they were given.
    ///   An entry the source already relates this one to from here is not
    ///   read back reversed as well, whatever the other end says.
    /// </remarks>
    public IReadOnlyList<IRelatedMetadata<TBase, TRelated>> GetRelations<TBase, TRelated>(MetadataGuid entry)
        where TBase : IMetadata
        where TRelated : IMetadata
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!MetadataEntries.Fits(MetadataEntries.TypeOf<TBase>(), entry.EntityType))
            return [];

        var relatedType = MetadataEntries.TypeOf<TRelated>();
        var own = repository.GetByBase(entry);
        var stated = own.Select(row => (row.RelatedType, row.RelatedID)).ToHashSet();
        var reversed = repository.GetByRelated(entry)
            .Where(row => !stated.Contains((row.BaseType, row.BaseID)))
            .Select(row => new MetadataRelation<TRelated, TBase>(row).Reversed);

        return
        [
            .. own
                .Select(row => new MetadataRelation<TBase, TRelated>(row))
                .Concat(reversed)
                .Where(relation => MetadataEntries.Fits(relatedType, relation.RelatedID.EntityType)),
        ];
    }

    #endregion

    #region Writing

    /// <inheritdoc />
    /// <remarks>
    ///   A relation given twice keeps its first place.
    /// </remarks>
    public void SetRelations(MetadataGuid entry, IEnumerable<MetadataRelationData> relations)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(relations);
        MetadataEntries.CheckWritableSource(entry.Source, nameof(entry));

        var items = MetadataRows.FirstOfEach(relations, relation => (relation.RelatedID, relation.RelationType));
        foreach (var item in items)
            MetadataEntries.CheckReference(item.RelatedID, entry.Source, null, nameof(relations));

        lock (_writeLock)
        {
            var (saving, deleting) = MetadataRows.Replace(
                repository.GetByBase(entry),
                items,
                row => (row.RelatedType, row.RelatedID, row.RelationType),
                relation => (relation.RelatedID.EntityType, relation.RelatedID.ID, relation.RelationType),
                (row, relation, position) =>
                {
                    row.Source = entry.Source;
                    row.BaseType = entry.EntityType;
                    row.BaseID = entry.ID;
                    row.RelatedType = relation.RelatedID.EntityType;
                    row.RelatedID = relation.RelatedID.ID;
                    row.RelationType = relation.RelationType;
                    row.Ordering = position;
                }
            );
            writer.Write(new MetadataRowChanges<Metadata_Relation>(repository, saving, deleting));
        }
    }

    /// <inheritdoc />
    public int RemoveRelations(MetadataGuid entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        MetadataEntries.CheckWritableSource(entry.Source, nameof(entry));

        lock (_writeLock)
        {
            var deleting = repository.GetByBase(entry);
            writer.Write(new MetadataRowChanges<Metadata_Relation>(repository, [], deleting));
            return deleting.Count;
        }
    }

    #endregion
}
