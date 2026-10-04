using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   The groups of the stored orderings.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_Ordering_GroupRepository(DatabaseFactory databaseFactory) : MetadataSourceRowRepository<Metadata_Ordering_Group>(databaseFactory)
{
    private PocoIndex<int, Metadata_Ordering_Group, (MetadataSource, string)>? _orderingIDs;

    /// <inheritdoc />
    protected override MetadataSource SourceOf(Metadata_Ordering_Group row)
        => row.Source;

    /// <inheritdoc />
    protected override string ProviderIDOf(Metadata_Ordering_Group row)
        => row.ProviderID;

    /// <inheritdoc />
    /// <remarks>
    ///   The group's synthesized name follows the season number its row gives it.
    /// </remarks>
    protected override IEnumerable<MetadataGuid> TextEntriesOf(Metadata_Ordering_Group entity, bool removed)
        => [entity.ID];

    /// <inheritdoc />
    public override void PopulateIndexes()
    {
        base.PopulateIndexes();
        _orderingIDs = Cache.CreateIndex(row => (row.Source, row.OrderingID));
    }

    /// <summary>
    ///   The groups of one ordering.
    /// </summary>
    /// <param name="source">The source the ordering is stored under.</param>
    /// <param name="orderingID">The source's ID for the ordering.</param>
    /// <returns>The groups, in viewing order.</returns>
    public IReadOnlyList<Metadata_Ordering_Group> GetByOrderingID(MetadataSource source, string orderingID)
        => [.. _orderingIDs!.GetMultiple((source, orderingID)).OrderBy(row => row.Position).ThenBy(row => row.Metadata_Ordering_GroupID)];
}
