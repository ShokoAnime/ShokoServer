using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Repositories.Direct.TMDB.Optional;

public class TMDB_SuggestionRepository(DatabaseFactory databaseFactory) : BaseDirectRepository<TMDB_Suggestion, int>(databaseFactory)
{
    /// <summary>
    /// The entries suggested for <paramref name="entityID"/>.
    /// </summary>
    /// <param name="entityType">Whether to look at shows or movies.</param>
    /// <param name="entityID">The TMDB id of the entry being looked at.</param>
    /// <param name="kind">Optional. Only one of the two lists.</param>
    /// <returns>The suggestions, best first.</returns>
    public IReadOnlyList<TMDB_Suggestion> GetByTmdbEntityID(DataEntityType entityType, int entityID, SuggestionKind? kind = null)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session
            .Query<TMDB_Suggestion>()
            .Where(a => a.TmdbEntityType == entityType && a.TmdbEntityID == entityID && (kind == null || a.Kind == kind))
            .OrderBy(a => a.Kind)
            .ThenBy(a => a.Ordering)
            .ToList();
    }

    /// <summary>
    /// The entries that suggest <paramref name="entityID"/>, which works
    /// whether or not that entry is in the collection.
    /// </summary>
    /// <param name="entityType">Whether to look at shows or movies.</param>
    /// <param name="entityID">The TMDB id of the suggested entry.</param>
    /// <param name="kind">Optional. Only one of the two lists.</param>
    /// <returns>The suggestions pointing at it, best first.</returns>
    public IReadOnlyList<TMDB_Suggestion> GetBySuggestedTmdbEntityID(DataEntityType entityType, int entityID, SuggestionKind? kind = null)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session
            .Query<TMDB_Suggestion>()
            .Where(a => a.TmdbEntityType == entityType && a.SuggestedTmdbEntityID == entityID && (kind == null || a.Kind == kind))
            .OrderBy(a => a.Kind)
            .ThenBy(a => a.Ordering)
            .ToList();
    }
}
