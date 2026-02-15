#nullable enable
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Repositories.Direct.TMDB.Optional;

public class TMDB_CollectionRepository : BaseDirectRepository<TMDB_Collection, int>
{
    public TMDB_Collection? GetByTmdbCollectionID(int collectionId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Collection>()
                .Where(a => a.TmdbCollectionID == collectionId)
                .Take(1)
                .SingleOrDefault();
        });
    }

    public TMDB_CollectionRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
