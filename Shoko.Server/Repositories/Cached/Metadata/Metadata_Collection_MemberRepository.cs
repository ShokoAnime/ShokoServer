using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   Every plugin source's collection members in the store.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_Collection_MemberRepository(DatabaseFactory databaseFactory) : MetadataStoreRepository<Metadata_Collection_Member>(databaseFactory)
{
    private PocoIndex<int, Metadata_Collection_Member, (MetadataSource, string)>? _collectionIDs;

    private PocoIndex<int, Metadata_Collection_Member, (MetadataSource, MetadataEntityType, string)>? _memberIDs;

    /// <inheritdoc />
    public override void PopulateIndexes()
    {
        _collectionIDs = Cache.CreateIndex(row => (row.Source, row.CollectionID));
        _memberIDs = Cache.CreateIndex(row => (row.Source, row.MemberType, row.MemberID));
    }

    /// <inheritdoc />
    /// <remarks>
    ///   A series linked to a movie may be named by the movie's collection,
    ///   so the member and the collection are forgotten when it joins or
    ///   leaves.
    /// </remarks>
    protected override IEnumerable<MetadataGuid> TextEntriesOf(Metadata_Collection_Member entity, bool removed)
        => [entity.MemberGuid, new(entity.Source, MetadataEntityType.Collection, entity.CollectionID)];

    /// <summary>
    ///   Every member of one collection.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="collectionID">The source's ID for the collection.</param>
    /// <returns>The members, in the order given.</returns>
    public IReadOnlyList<Metadata_Collection_Member> GetByCollectionID(MetadataSource source, string collectionID)
        => string.IsNullOrEmpty(collectionID)
            ? []
            : [.. _collectionIDs!.GetMultiple((source, collectionID)).OrderBy(row => row.Ordering).ThenBy(row => row.Metadata_Collection_MemberID)];

    /// <summary>
    ///   Every row naming one series or movie as a member.
    /// </summary>
    /// <param name="member">The series or movie.</param>
    /// <returns>The rows, in no particular order.</returns>
    public IReadOnlyList<Metadata_Collection_Member> GetByMember(MetadataGuid member)
        => _memberIDs!.GetMultiple((member.Source, member.EntityType, member.ID));
}
