using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Repositories.Direct.TMDB.Optional;

public class TMDB_AlternateOrderingRepository(DatabaseFactory databaseFactory) : BaseDirectRepository<TMDB_AlternateOrdering, int>(databaseFactory)
{
    public virtual IReadOnlyList<TMDB_AlternateOrdering> GetByTmdbShowID(int showId)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session
            .Query<TMDB_AlternateOrdering>()
            .Where(a => a.TmdbShowID == showId)
            .ToList();
    }

    public virtual TMDB_AlternateOrdering? GetByTmdbEpisodeGroupCollectionID(string episodeGroupCollectionId)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session
            .Query<TMDB_AlternateOrdering>()
            .Where(a => a.TmdbEpisodeGroupCollectionID == episodeGroupCollectionId)
            .Take(1)
            .SingleOrDefault();
    }

    public TMDB_AlternateOrdering? GetByEpisodeGroupCollectionAndShowIDs(string collectionId, int showId)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session
            .Query<TMDB_AlternateOrdering>()
            .Where(a => a.TmdbEpisodeGroupCollectionID == collectionId && a.TmdbShowID == showId)
            .Take(1)
            .SingleOrDefault();
    }

    /// <summary>
    ///   Every show ID the alternate orderings name.
    /// </summary>
    /// <returns>The IDs, each once.</returns>
    public IReadOnlyList<int> GetAllTmdbShowIDs()
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session
            .Query<TMDB_AlternateOrdering>()
            .Select(a => a.TmdbShowID)
            .Distinct()
            .ToList();
    }
}
