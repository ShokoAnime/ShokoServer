using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.TMDB;

/// <summary>
///   TMDB's episode group collections, the alternate orderings of its shows,
///   kept in memory as the chosen ordering of a show is checked for every
///   episode read.
/// </summary>
/// <param name="databaseFactory">The database.</param>
public class TMDB_AlternateOrderingRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<TMDB_AlternateOrdering, int>(databaseFactory)
{
    private PocoIndex<int, TMDB_AlternateOrdering, int> _showIDs = null!;

    private PocoIndex<int, TMDB_AlternateOrdering, string> _collectionIDs = null!;

    protected override int SelectKey(TMDB_AlternateOrdering entity)
        => entity.TMDB_AlternateOrderingID;

    public override void PopulateIndexes()
    {
        _showIDs = Cache.CreateIndex(a => a.TmdbShowID);
        _collectionIDs = Cache.CreateIndex(a => a.TmdbEpisodeGroupCollectionID);
    }

    public virtual IReadOnlyList<TMDB_AlternateOrdering> GetByTmdbShowID(int showId)
        => [.. _showIDs.GetMultiple(showId).OrderBy(a => a.TMDB_AlternateOrderingID)];

    public virtual TMDB_AlternateOrdering? GetByTmdbEpisodeGroupCollectionID(string episodeGroupCollectionId)
        => _collectionIDs.GetOne(episodeGroupCollectionId);

    public TMDB_AlternateOrdering? GetByEpisodeGroupCollectionAndShowIDs(string collectionId, int showId)
        => _collectionIDs.GetMultiple(collectionId).FirstOrDefault(a => a.TmdbShowID == showId);

    /// <summary>
    ///   Every show ID the alternate orderings name.
    /// </summary>
    /// <returns>The IDs, each once.</returns>
    public IReadOnlyList<int> GetAllTmdbShowIDs()
        => [.. GetAll().Select(a => a.TmdbShowID).Distinct()];
}
