using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   Every plugin source's seasons in the store.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_SeasonRepository(DatabaseFactory databaseFactory) : MetadataSourceRowRepository<Metadata_Season>(databaseFactory)
{
    private PocoIndex<int, Metadata_Season, (MetadataSource, string)>? _seriesIDs;

    /// <inheritdoc />
    protected override MetadataSource SourceOf(Metadata_Season row)
        => row.Source;

    /// <inheritdoc />
    protected override string ProviderIDOf(Metadata_Season row)
        => row.ProviderID;

    /// <inheritdoc />
    public override void PopulateIndexes()
    {
        base.PopulateIndexes();
        _seriesIDs = Cache.CreateIndex(row => (row.Source, row.SeriesID));
    }

    /// <summary>
    ///   Every season of one series.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="seriesID">The source's ID for the series.</param>
    /// <returns>The seasons, in no particular order.</returns>
    public IReadOnlyList<Metadata_Season> GetBySeriesID(MetadataSource source, string seriesID)
        => string.IsNullOrEmpty(seriesID) ? [] : _seriesIDs!.GetMultiple((source, seriesID));
}
