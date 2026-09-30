using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   Every source's suggestions in the store.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_SuggestionRepository(DatabaseFactory databaseFactory) : MetadataStoreRepository<Metadata_Suggestion>(databaseFactory)
{
    private PocoIndex<int, Metadata_Suggestion, (MetadataSource, MetadataEntityType, string)>? _bases;

    private PocoIndex<int, Metadata_Suggestion, (MetadataSource, MetadataEntityType, string)>? _suggested;

    /// <inheritdoc />
    public override void PopulateIndexes()
    {
        _bases = Cache.CreateIndex(row => (row.Source, row.BaseType, row.BaseID));
        _suggested = Cache.CreateIndex(row => (row.Source, row.SuggestedType, row.SuggestedID));
    }

    /// <summary>
    ///   Every suggestion an entry makes.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The suggestions, in the order they were given.</returns>
    public IReadOnlyList<Metadata_Suggestion> GetByBase(MetadataGuid entry)
        => [.. _bases!.GetMultiple((entry.Source, entry.EntityType, entry.ID)).OrderBy(row => row.Ordering).ThenBy(row => row.Metadata_SuggestionID)];

    /// <summary>
    ///   Every suggestion pointing at an entry.
    /// </summary>
    /// <param name="entry">The entry suggested.</param>
    /// <returns>The suggestions, oldest first.</returns>
    public IReadOnlyList<Metadata_Suggestion> GetBySuggested(MetadataGuid entry)
        => [.. _suggested!.GetMultiple((entry.Source, entry.EntityType, entry.ID)).OrderBy(row => row.Metadata_SuggestionID)];
}
