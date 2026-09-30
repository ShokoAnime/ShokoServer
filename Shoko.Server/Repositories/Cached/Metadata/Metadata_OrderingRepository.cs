using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   The stored orderings, global and local.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_OrderingRepository(DatabaseFactory databaseFactory) : MetadataSourceRowRepository<Metadata_Ordering>(databaseFactory)
{
    private PocoIndex<int, Metadata_Ordering, (MetadataSource, string)>? _seriesIDs;

    /// <inheritdoc />
    protected override MetadataSource SourceOf(Metadata_Ordering row)
        => row.Source;

    /// <inheritdoc />
    protected override string ProviderIDOf(Metadata_Ordering row)
        => row.ProviderID;

    /// <inheritdoc />
    /// <remarks>
    ///   The ordering keeps its default name on its row.
    /// </remarks>
    protected override IEnumerable<MetadataGuid> TextEntriesOf(Metadata_Ordering entity, bool removed)
        => [entity.ID];

    /// <inheritdoc />
    public override void PopulateIndexes()
    {
        base.PopulateIndexes();
        _seriesIDs = Cache.CreateIndex(row => (row.SeriesSource, row.SeriesID));
    }

    /// <summary>
    ///   Every stored ordering of one series, whatever source keeps it.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The orderings, in no particular order.</returns>
    public IReadOnlyList<Metadata_Ordering> GetBySeries(MetadataGuid series)
        => _seriesIDs!.GetMultiple((series.Source, series.ID));
}
