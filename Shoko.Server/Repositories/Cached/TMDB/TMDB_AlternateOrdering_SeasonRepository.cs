using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.TMDB;

/// <summary>
///   The episode groups of TMDB's alternate orderings, kept in memory as an
///   episode's place in one names its group.
/// </summary>
/// <param name="databaseFactory">The database.</param>
public class TMDB_AlternateOrdering_SeasonRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<TMDB_AlternateOrdering_Season, int>(databaseFactory)
{
    private PocoIndex<int, TMDB_AlternateOrdering_Season, int> _showIDs = null!;

    private PocoIndex<int, TMDB_AlternateOrdering_Season, string> _collectionIDs = null!;

    private PocoIndex<int, TMDB_AlternateOrdering_Season, string> _groupIDs = null!;

    protected override int SelectKey(TMDB_AlternateOrdering_Season entity)
        => entity.TMDB_AlternateOrdering_SeasonID;

    public override void PopulateIndexes()
    {
        _showIDs = Cache.CreateIndex(a => a.TmdbShowID);
        _collectionIDs = Cache.CreateIndex(a => a.TmdbEpisodeGroupCollectionID);
        _groupIDs = Cache.CreateIndex(a => a.TmdbEpisodeGroupID);
    }

    public virtual IReadOnlyList<TMDB_AlternateOrdering_Season> GetByTmdbShowID(int showId)
        => _showIDs.GetMultiple(showId)
            .OrderBy(a => a.TmdbEpisodeGroupCollectionID)
            .ThenBy(e => e.SeasonNumber == 0)
            .ThenBy(e => e.SeasonNumber)
            .ToList();

    public virtual IReadOnlyList<TMDB_AlternateOrdering_Season> GetByTmdbEpisodeGroupCollectionID(string collectionId)
        => _collectionIDs.GetMultiple(collectionId)
            .OrderBy(e => e.SeasonNumber == 0)
            .ThenBy(e => e.SeasonNumber)
            .ToList();

    public virtual TMDB_AlternateOrdering_Season? GetByTmdbEpisodeGroupID(string groupId)
        => _groupIDs.GetOne(groupId);

    /// <summary>
    ///   Every show ID the alternate ordering seasons name.
    /// </summary>
    /// <returns>The IDs, each once.</returns>
    public IReadOnlyList<int> GetAllTmdbShowIDs()
        => [.. GetAll().Select(a => a.TmdbShowID).Distinct()];
}
