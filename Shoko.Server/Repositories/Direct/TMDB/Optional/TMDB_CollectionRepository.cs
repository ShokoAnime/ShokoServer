using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.Repositories.Direct;

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
