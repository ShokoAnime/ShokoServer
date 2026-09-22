using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Repositories.Direct.TMDB.Optional;

public class TMDB_CollectionRepository(DatabaseFactory databaseFactory) : BaseDirectRepository<TMDB_Collection, int>(databaseFactory)
{
    public TMDB_Collection? GetByTmdbCollectionID(int collectionId)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session
            .Query<TMDB_Collection>()
            .Where(a => a.TmdbCollectionID == collectionId)
            .Take(1)
            .SingleOrDefault();
    }
}
