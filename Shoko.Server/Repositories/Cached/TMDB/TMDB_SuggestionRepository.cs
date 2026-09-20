using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Repositories.Cached.TMDB;

public class TMDB_SuggestionRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<TMDB_Suggestion, int>(databaseFactory)
{
    private PocoIndex<int, TMDB_Suggestion, (DataEntityType Type, int ID)>? _entityIDs;

    private PocoIndex<int, TMDB_Suggestion, (DataEntityType Type, int ID)>? _suggestedEntityIDs;

    protected override int SelectKey(TMDB_Suggestion entity)
        => entity.TMDB_SuggestionID;

    public override void PopulateIndexes()
    {
        _entityIDs = Cache.CreateIndex(a => (a.TmdbEntityType, a.TmdbEntityID));
        _suggestedEntityIDs = Cache.CreateIndex(a => (a.TmdbEntityType, a.SuggestedTmdbEntityID));
    }

    /// <summary>
    /// The entries suggested for <paramref name="entityID"/>.
    /// </summary>
    /// <param name="entityType">Whether to look at shows or movies.</param>
    /// <param name="entityID">The TMDB id of the entry being looked at.</param>
    /// <param name="kind">Optional. Only one of the two lists.</param>
    /// <returns>The suggestions, best first.</returns>
    public IReadOnlyList<TMDB_Suggestion> GetByTmdbEntityID(DataEntityType entityType, int entityID, SuggestionKind? kind = null)
        => _entityIDs!.GetMultiple((entityType, entityID))
            .Where(a => kind is null || a.Kind == kind)
            .OrderBy(a => a.Kind)
            .ThenBy(a => a.Ordering)
            .ToList();

    /// <summary>
    /// The entries that suggest <paramref name="entityID"/>, which works
    /// whether or not that entry is in the collection.
    /// </summary>
    /// <param name="entityType">Whether to look at shows or movies.</param>
    /// <param name="entityID">The TMDB id of the suggested entry.</param>
    /// <param name="kind">Optional. Only one of the two lists.</param>
    /// <returns>The suggestions pointing at it, best first.</returns>
    public IReadOnlyList<TMDB_Suggestion> GetBySuggestedTmdbEntityID(DataEntityType entityType, int entityID, SuggestionKind? kind = null)
        => _suggestedEntityIDs!.GetMultiple((entityType, entityID))
            .Where(a => kind is null || a.Kind == kind)
            .OrderBy(a => a.Kind)
            .ThenBy(a => a.Ordering)
            .ToList();
}
