using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   The episodes' places in the groups of the stored orderings.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_Ordering_EntryRepository(DatabaseFactory databaseFactory) : MetadataStoreRepository<Metadata_Ordering_Entry>(databaseFactory)
{
    private PocoIndex<int, Metadata_Ordering_Entry, (MetadataSource, string)>? _orderingIDs;

    private PocoIndex<int, Metadata_Ordering_Entry, (MetadataSource, string)>? _episodeIDs;

    /// <inheritdoc />
    public override void PopulateIndexes()
    {
        _orderingIDs = Cache.CreateIndex(row => (row.Source, row.OrderingID));
        _episodeIDs = Cache.CreateIndex(row => (row.EpisodeSource, row.EpisodeID));
    }

    /// <summary>
    ///   The places of one ordering, across all its groups.
    /// </summary>
    /// <param name="source">The source the ordering is stored under.</param>
    /// <param name="orderingID">The source's ID for the ordering.</param>
    /// <returns>The places, by group and then by position.</returns>
    public IReadOnlyList<Metadata_Ordering_Entry> GetByOrderingID(MetadataSource source, string orderingID)
        => [.. _orderingIDs!.GetMultiple((source, orderingID)).OrderBy(row => row.GroupID, StringComparer.Ordinal).ThenBy(row => row.Position)];

    /// <summary>
    ///   Every place one episode has in the stored orderings.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>The places, in no particular order.</returns>
    public IReadOnlyList<Metadata_Ordering_Entry> GetByEpisode(MetadataGuid episode)
        => _episodeIDs!.GetMultiple((episode.Source, episode.ID));
}
