using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   Every plugin source's episodes in the store.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_EpisodeRepository(DatabaseFactory databaseFactory) : MetadataSourceRowRepository<Metadata_Episode>(databaseFactory)
{
    private PocoIndex<int, Metadata_Episode, (MetadataSource, string)>? _seriesIDs;

    private PocoIndex<int, Metadata_Episode, (MetadataSource, string)>? _seasonIDs;

    /// <inheritdoc />
    protected override MetadataSource SourceOf(Metadata_Episode row)
        => row.Source;

    /// <inheritdoc />
    protected override string ProviderIDOf(Metadata_Episode row)
        => row.ProviderID;

    /// <inheritdoc />
    public override void PopulateIndexes()
    {
        base.PopulateIndexes();
        _seriesIDs = Cache.CreateIndex(row => (row.Source, row.SeriesID));
        _seasonIDs = Cache.CreateIndex(row => (row.Source, row.SeasonID ?? string.Empty));
    }

    /// <summary>
    ///   Every episode of one series.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="seriesID">The source's ID for the series.</param>
    /// <returns>The episodes, in no particular order.</returns>
    public IReadOnlyList<Metadata_Episode> GetBySeriesID(MetadataSource source, string seriesID)
        => string.IsNullOrEmpty(seriesID) ? [] : _seriesIDs!.GetMultiple((source, seriesID));

    /// <summary>
    ///   Every episode in one season.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="seasonID">The source's ID for the season.</param>
    /// <returns>The episodes, in no particular order.</returns>
    public IReadOnlyList<Metadata_Episode> GetBySeasonID(MetadataSource source, string seasonID)
        => string.IsNullOrEmpty(seasonID) ? [] : _seasonIDs!.GetMultiple((source, seasonID));
}
