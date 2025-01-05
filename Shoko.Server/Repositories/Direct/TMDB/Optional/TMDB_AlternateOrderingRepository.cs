#nullable enable
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Repositories.Direct.TMDB.Optional;

public class TMDB_AlternateOrderingRepository : BaseDirectRepository<TMDB_AlternateOrdering, int>
{
    public IReadOnlyList<TMDB_AlternateOrdering> GetByTmdbShowID(int showId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_AlternateOrdering>()
                .Where(a => a.TmdbShowID == showId)
                .ToList();
        });
    }

    public TMDB_AlternateOrdering? GetByTmdbEpisodeGroupCollectionID(string episodeGroupCollectionId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_AlternateOrdering>()
                .Where(a => a.TmdbEpisodeGroupCollectionID == episodeGroupCollectionId)
                .Take(1)
                .SingleOrDefault();
        });
    }

    public TMDB_AlternateOrdering? GetByEpisodeGroupCollectionAndShowIDs(string collectionId, int showId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_AlternateOrdering>()
                .Where(a => a.TmdbEpisodeGroupCollectionID == collectionId && a.TmdbShowID == showId)
                .Take(1)
                .SingleOrDefault();
        });
    }

    public TMDB_AlternateOrderingRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
